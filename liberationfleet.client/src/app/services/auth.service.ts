import { Injectable, Injector, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap, of, catchError, finalize, shareReplay } from 'rxjs';
import { AuthResult, User } from '../models/user.model';
import { CryptoSessionService } from './crypto/crypto-session.service';
import {
  BACKUP_WRAP_RECOVERY_KEY,
  normalizeRecoveryPhrase,
  validateRecoveryPhrase
} from './crypto/recovery-key.util';
import { CryptoApiService } from './crypto/crypto-api.service';
import { firstValueFrom } from 'rxjs';
import { AppStorageService, StorageScope } from './storage/app-storage.service';
import { SavedRecoveryPhraseService } from './saved-recovery-phrase.service';
import {
  AUTH_TOKEN_STORAGE_KEY,
  REMEMBER_LOGIN_STORAGE_KEY,
  SESSION_RECOVERY_PHRASE_STORAGE_KEY
} from './storage/storage-keys';
import { isJwtExpired, getUserIdFromToken } from '../utils/jwt.util';
import { isNativeApp } from '../utils/app-platform.util';
import { CrewService } from './crew.service';
import { FleetService } from './fleet.service';
import { ProfileService } from './profile.service';
import { EncryptedImageCacheService } from './encrypted-image-cache.service';
import { ContentPreferenceService } from './content-preference.service';
import { NotificationService } from './notification.service';
import { ProfileLocationService } from './profile-location.service';
import { PushNotificationRegistrationService } from './push-notification-registration.service';
import { NotificationHubService } from './notification-hub.service';
import { ForumListPrefetchService } from './forum-list-prefetch.service';
import { CrewDashboardPreviewService } from './crew-dashboard-preview.service';
import { clearAllForumListScrollState } from '../utils/forum-list-scroll.util';

export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
  deviceId?: string;
  deviceName?: string;
  userAgent?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiUrl = '/api/auth';
  private static readonly UNLOCK_RETRY_DELAYS_MS = [2_000, 5_000, 15_000, 30_000, 60_000];

  private readonly currentUserSubject = new BehaviorSubject<User | null>(null);
  private encryptionReadyPromise: Promise<void> | null = null;
  /** False until local session/device unlock has been attempted (or is unnecessary). */
  private encryptionBootstrapSettled = false;
  private refreshInFlight: Observable<AuthResult | null> | null = null;
  private unlockRetryTimer: ReturnType<typeof setTimeout> | null = null;
  private unlockRetryAttempt = 0;

  public currentUser$ = this.currentUserSubject.asObservable();

  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
  private profileService = inject(ProfileService);
  private imageCache = inject(EncryptedImageCacheService);
  private contentPreferences = inject(ContentPreferenceService);
  private notificationService = inject(NotificationService);
  private profileLocation = inject(ProfileLocationService);
  private pushNotifications = inject(PushNotificationRegistrationService);
  private notificationHub = inject(NotificationHubService);
  /** Lazy: avoid construction-time cycles with forum/crypto graph. */
  private injector = inject(Injector);

  constructor(
    private http: HttpClient,
    private cryptoSession: CryptoSessionService,
    private cryptoApi: CryptoApiService,
    private storage: AppStorageService,
    private savedRecoveryPhrase: SavedRecoveryPhraseService
  ) {
    this.loadToken();
  }

  login(data: LoginRequest): Observable<AuthResult> {
    return this.http.post<AuthResult>(`${this.apiUrl}/login`, data).pipe(
      tap(response => {
        if (response.success && response.token && !response.requiresMfa) {
          this.establishSession(response);
        }
      })
    );
  }

  verifyMfa(mfaChallengeToken: string, code: string): Observable<AuthResult> {
    return this.http.post<AuthResult>(`${this.apiUrl}/verify-mfa`, { mfaChallengeToken, code }).pipe(
      tap(response => this.establishSession(response))
    );
  }

  resendMfa(mfaChallengeToken: string): Observable<AuthResult> {
    return this.http.post<AuthResult>(`${this.apiUrl}/resend-mfa`, { mfaChallengeToken });
  }

  /**
   * Re-issues a JWT so continued use keeps extending the session.
   * Does not clear crypto or session caches.
   */
  refreshSession(): Observable<AuthResult> {
    return this.http.post<AuthResult>(`${this.apiUrl}/refresh`, {}).pipe(
      tap(response => this.applyRefreshedSession(response))
    );
  }

  /**
   * When remember-me is on and a valid token exists, refresh it (fire-and-forget safe).
   * Call on app open / foreground so daily use slides the expiry forward.
   */
  refreshSessionIfRemembered(): Observable<AuthResult | null> {
    if (!this.isRememberLoginEnabled()) {
      return of(null);
    }

    const token = this.getToken();
    if (!token || isJwtExpired(token)) {
      return of(null);
    }

    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }

    this.refreshInFlight = this.refreshSession().pipe(
      catchError(() => of(null)),
      finalize(() => {
        this.refreshInFlight = null;
      }),
      shareReplay({ bufferSize: 1, refCount: false })
    );

    return this.refreshInFlight;
  }

  establishSession(response: AuthResult): void {
    if (response.token) {
      // Drop any prior session caches so membership/fleet status cannot stick
      // across accounts or survive a failed pre-login probe as "not in a crew".
      // Also clear in-memory crypto keys so a prior account's identity cannot
      // decrypt (or silently corrupt) the new session's content.
      this.cryptoSession.clearSession();
      this.clearSessionCaches();
      this.setToken(response.token);
      this.currentUserSubject.next(response.user ?? null);
      this.resetEncryptionReady();
      void this.getEncryptionReady();
      void this.pushNotifications.start();
    }
  }

  /**
   * After absorbing a new account into an old one, keep the in-memory identity keys
   * (they are now the surviving account's keys) while switching the JWT user id.
   */
  async establishSessionPreservingCrypto(response: AuthResult): Promise<void> {
    if (!response.token) {
      return;
    }

    const previousUserId = this.getCurrentUserId();
    const nextUserId = response.user?.id ?? getUserIdFromToken(response.token);
    this.clearSessionCaches();
    this.setToken(response.token);
    this.currentUserSubject.next(response.user ?? null);

    if (previousUserId != null && nextUserId != null && previousUserId !== nextUserId) {
      const phrase = this.savedRecoveryPhrase.getPhrase(previousUserId);
      if (phrase) {
        this.savedRecoveryPhrase.removePhrase(previousUserId);
        this.savedRecoveryPhrase.savePhrase(nextUserId, phrase);
      }
    }

    this.resetEncryptionReady();
    // Keys already unlocked for the recovery account; mark ready without re-bootstrap wipe.
    if (this.cryptoSession.isUnlocked()) {
      this.encryptionBootstrapSettled = true;
      this.encryptionReadyPromise = Promise.resolve();
    } else {
      void this.getEncryptionReady();
    }

    await this.pushNotifications.start();
  }

  private applyRefreshedSession(response: AuthResult): void {
    if (!response.success || !response.token) {
      return;
    }

    this.setToken(response.token);
    if (response.user) {
      this.currentUserSubject.next(response.user);
    }
    // Sliding refresh used to skip this — a first PUT that 401'd never retried.
    void this.pushNotifications.start();
  }

  logout(): void {
    void this.pushNotifications.stopAndUnregister();
    void this.notificationHub.disconnect();
    this.cancelEncryptionUnlockRetry();
    this.removeToken();
    this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    this.cryptoSession.clearSession();
    this.currentUserSubject.next(null);
    this.resetEncryptionReady();
    this.clearSessionCaches();
  }

  /** Lock encryption and remove any remembered recovery key from this device. */
  lockEncryptionOnThisDevice(): void {
    this.cancelEncryptionUnlockRetry();
    this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    this.savedRecoveryPhrase.setSaveEnabled(false);
    this.cryptoSession.clearSession();
    this.resetEncryptionReady();
  }

  /**
   * Re-attempt remembered-key auto-unlock after the API may have come back
   * (app foreground, post-deploy). Keeps the saved phrase on transient failures.
   */
  retryAutoUnlockIfNeeded(): void {
    if (!this.isAuthenticated() || this.cryptoSession.isUnlocked()) {
      this.cancelEncryptionUnlockRetry();
      return;
    }

    if (!this.hasRememberedRecoveryPhrase()) {
      return;
    }

    this.unlockRetryAttempt = 0;
    this.cancelEncryptionUnlockRetry();
    this.resetEncryptionReady();
    void this.getEncryptionReady();
  }

  private clearSessionCaches(): void {
    this.crewService.clearSessionCache();
    this.fleetService.clearSessionCache();
    this.profileService.clearSessionCache();
    this.profileLocation.clear();
    this.imageCache.clear();
    this.contentPreferences.clearSessionCache();
    this.notificationService.clearSessionCache();
    // Decrypted crew/fleet forum plaintext — must never survive logout/account switch.
    this.injector.get(ForumListPrefetchService).clearSessionCache();
    this.injector.get(CrewDashboardPreviewService).clearSessionCache();
    clearAllForumListScrollState();
  }

  getToken(): string | null {
    return this.storage.get(this.getTokenStorageScope(), AUTH_TOKEN_STORAGE_KEY);
  }

  setToken(token: string): void {
    this.storage.set(this.getTokenStorageScope(), AUTH_TOKEN_STORAGE_KEY, token);
  }

  removeToken(): void {
    this.storage.remove(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY);
    this.storage.remove(StorageScope.Session, AUTH_TOKEN_STORAGE_KEY);
  }

  isRememberLoginEnabled(): boolean {
    const stored = this.storage.get(StorageScope.Persistent, REMEMBER_LOGIN_STORAGE_KEY);
    return stored !== 'false';
  }

  setRememberLoginEnabled(enabled: boolean): void {
    if (enabled) {
      this.storage.set(StorageScope.Persistent, REMEMBER_LOGIN_STORAGE_KEY, 'true');
      const token = this.storage.get(StorageScope.Session, AUTH_TOKEN_STORAGE_KEY);
      if (token) {
        this.storage.set(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY, token);
        this.storage.remove(StorageScope.Session, AUTH_TOKEN_STORAGE_KEY);
      }
      return;
    }

    this.storage.set(StorageScope.Persistent, REMEMBER_LOGIN_STORAGE_KEY, 'false');
    const token = this.storage.get(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY);
    if (token) {
      this.storage.set(StorageScope.Session, AUTH_TOKEN_STORAGE_KEY, token);
      this.storage.remove(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY);
    }
  }

  getCurrentUserId(): number | null {
    const fromUser = this.currentUserSubject.value?.id ?? null;
    if (fromUser != null) {
      return fromUser;
    }
    const token = this.getToken();
    return token ? getUserIdFromToken(token) : null;
  }

  getSessionRecoveryPhrase(): string | null {
    return this.storage.get(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) {
      return false;
    }

    if (isJwtExpired(token)) {
      this.removeToken();
      return false;
    }

    return true;
  }

  /**
   * True only after local unlock from session/device memory has been attempted and failed
   * (or no remembered key exists). Avoids flashing the unlock dialog during auto-unlock.
   */
  needsEncryptionUnlock(): boolean {
    return this.isAuthenticated()
      && this.encryptionBootstrapSettled
      && !this.cryptoSession.isUnlocked();
  }

  getEncryptionReady(): Promise<void> {
    if (!this.encryptionReadyPromise) {
      this.encryptionReadyPromise = this.ensureEncryptionReady();
    }
    return this.encryptionReadyPromise;
  }

  async setupNewAccountEncryption(recoveryPhrase: string, rememberOnDevice = true): Promise<void> {
    const normalized = normalizeRecoveryPhrase(recoveryPhrase);
    if (!(await validateRecoveryPhrase(normalized))) {
      throw new Error('Invalid recovery phrase.');
    }

    // Drop any stale on-device phrase for this user id (common after a server data
    // reset reuses ids 1, 2, …) so auto-unlock cannot race with provisioning.
    const userId = this.getCurrentUserId();
    if (userId) {
      this.savedRecoveryPhrase.removePhrase(userId);
    }

    await this.cryptoSession.provisionIdentityKeysWithRecoveryPhrase(normalized);
    this.persistRecoveryPhrase(normalized, rememberOnDevice);
    this.resetEncryptionReady();
  }

  async unlockWithRecoveryPhrase(recoveryPhrase: string, rememberOnDevice = false): Promise<void> {
    const normalized = normalizeRecoveryPhrase(recoveryPhrase);
    if (!(await validateRecoveryPhrase(normalized))) {
      throw new Error('Invalid recovery phrase.');
    }

    await this.cryptoSession.unlockFromRecoveryPhrase(normalized);
    this.persistRecoveryPhrase(normalized, rememberOnDevice);
    this.resetEncryptionReady();
  }

  /** Persist or clear the on-device recovery key without re-deriving identity keys. */
  setRememberRecoveryPhrase(recoveryPhrase: string, rememberOnDevice: boolean): void {
    const normalized = normalizeRecoveryPhrase(recoveryPhrase);
    this.persistRecoveryPhrase(normalized, rememberOnDevice);
  }

  async rotateRecoveryPhrase(recoveryPhrase: string): Promise<void> {
    const normalized = normalizeRecoveryPhrase(recoveryPhrase);
    if (!(await validateRecoveryPhrase(normalized))) {
      throw new Error('Invalid recovery phrase.');
    }

    await this.cryptoSession.rotateRecoveryPhrase(normalized);
    this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    const userId = this.getCurrentUserId();
    if (userId) {
      this.savedRecoveryPhrase.removePhrase(userId);
    }
    this.resetEncryptionReady();
  }

  async getBackupWrapVersion(): Promise<number> {
    try {
      const backup = await firstValueFrom(this.cryptoApi.getMyPrivateKeyBackup());
      return backup.keyVersion ?? BACKUP_WRAP_RECOVERY_KEY;
    } catch {
      return BACKUP_WRAP_RECOVERY_KEY;
    }
  }

  updateCurrentUser(user: User): void {
    this.currentUserSubject.next(user);
  }

  private loadToken(): void {
    const token = this.getToken();
    if (!token) {
      return;
    }

    if (isJwtExpired(token)) {
      this.removeToken();
      this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
      this.cryptoSession.clearSession();
      return;
    }

    this.currentUserSubject.next(null);
    void this.getEncryptionReady();
    // Sliding expiry: extend the JWT when remember-me is on so daily use stays signed in.
    this.refreshSessionIfRemembered().subscribe();
    // Do not wait for /app navigation — that race dropped native token uploads.
    void this.pushNotifications.start();
  }

  private async ensureEncryptionReady(): Promise<void> {
    try {
      if (!this.isAuthenticated() || this.cryptoSession.isUnlocked()) {
        this.cancelEncryptionUnlockRetry();
        return;
      }

      const stored = this.storage.get(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
      if (stored) {
        try {
          await this.cryptoSession.unlockFromRecoveryPhrase(stored);
          this.cancelEncryptionUnlockRetry();
          return;
        } catch (error: unknown) {
          if (this.shouldClearRememberedRecoveryPhrase(error)) {
            this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
          } else {
            // Keep the session phrase; API may be restarting. Still try persistent, then retry.
            this.scheduleEncryptionUnlockRetry();
          }
        }
      }

      await this.tryAutoUnlockFromPersistentStorage();
      if (this.cryptoSession.isUnlocked()) {
        this.cancelEncryptionUnlockRetry();
      }
    } finally {
      this.encryptionBootstrapSettled = true;
    }
  }

  private async tryAutoUnlockFromPersistentStorage(): Promise<void> {
    if (!this.savedRecoveryPhrase.isSaveEnabled() || this.cryptoSession.isUnlocked()) {
      return;
    }

    const userId = this.getCurrentUserId();
    if (!userId) {
      return;
    }

    const phrase = this.savedRecoveryPhrase.getPhrase(userId);
    if (!phrase) {
      return;
    }

    try {
      await this.cryptoSession.unlockFromRecoveryPhrase(phrase);
      this.storage.set(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY, phrase);
    } catch (error: unknown) {
      if (this.shouldClearRememberedRecoveryPhrase(error)) {
        this.savedRecoveryPhrase.removePhrase(userId);
        this.cancelEncryptionUnlockRetry();
        return;
      }

      // Downtime / auth blips: keep the saved phrase and retry when the API recovers.
      this.scheduleEncryptionUnlockRetry();
    }
  }

  private hasRememberedRecoveryPhrase(): boolean {
    if (this.storage.get(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY)) {
      return true;
    }

    if (!this.savedRecoveryPhrase.isSaveEnabled()) {
      return false;
    }

    const userId = this.getCurrentUserId();
    return !!userId && !!this.savedRecoveryPhrase.getPhrase(userId);
  }

  private scheduleEncryptionUnlockRetry(): void {
    if (this.unlockRetryTimer != null || this.cryptoSession.isUnlocked()) {
      return;
    }

    if (!this.isAuthenticated() || !this.hasRememberedRecoveryPhrase()) {
      return;
    }

    if (this.unlockRetryAttempt >= AuthService.UNLOCK_RETRY_DELAYS_MS.length) {
      return;
    }

    const delayMs = AuthService.UNLOCK_RETRY_DELAYS_MS[this.unlockRetryAttempt];
    this.unlockRetryAttempt += 1;
    this.unlockRetryTimer = setTimeout(() => {
      this.unlockRetryTimer = null;
      void this.runScheduledEncryptionUnlockRetry();
    }, delayMs);
  }

  private async runScheduledEncryptionUnlockRetry(): Promise<void> {
    if (!this.isAuthenticated() || this.cryptoSession.isUnlocked()) {
      this.cancelEncryptionUnlockRetry();
      return;
    }

    if (!this.hasRememberedRecoveryPhrase()) {
      return;
    }

    // Prefer waiting for a healthy API when possible, then re-run unlock.
    const healthy = await this.waitForApiHealthy();
    if (!healthy && this.unlockRetryAttempt < AuthService.UNLOCK_RETRY_DELAYS_MS.length) {
      this.scheduleEncryptionUnlockRetry();
      return;
    }

    this.resetEncryptionReady();
    await this.getEncryptionReady();
  }

  private async waitForApiHealthy(): Promise<boolean> {
    try {
      await firstValueFrom(this.http.get('/healthz', { responseType: 'text' }));
      return true;
    } catch {
      return false;
    }
  }

  private cancelEncryptionUnlockRetry(): void {
    if (this.unlockRetryTimer != null) {
      clearTimeout(this.unlockRetryTimer);
      this.unlockRetryTimer = null;
    }
    this.unlockRetryAttempt = 0;
  }

  private persistRecoveryPhrase(normalized: string, rememberOnDevice: boolean): void {
    if (rememberOnDevice) {
      this.storage.set(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY, normalized);
      const userId = this.getCurrentUserId();
      if (userId) {
        this.savedRecoveryPhrase.setSaveEnabled(true);
        this.savedRecoveryPhrase.savePhrase(userId, normalized);
      }
      return;
    }

    this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    const userId = this.getCurrentUserId();
    if (userId) {
      this.savedRecoveryPhrase.removePhrase(userId);
    }
  }

  private getTokenStorageScope(): StorageScope {
    // WKWebView / Android WebView wipe sessionStorage when the process dies.
    // Native JWT must live in localStorage or the push-token PUT 401s after a
    // background kill and never reaches DevicePushTokens.
    if (isNativeApp()) {
      return StorageScope.Persistent;
    }
    return this.isRememberLoginEnabled() ? StorageScope.Persistent : StorageScope.Session;
  }

  private shouldClearRememberedRecoveryPhrase(error: unknown): boolean {
    const message = error instanceof Error ? error.message : '';
    return message.includes('Invalid recovery key')
      || message.includes('Incorrect unlock method')
      || message.includes('No encryption backup found');
  }

  private resetEncryptionReady(): void {
    this.encryptionReadyPromise = null;
    this.encryptionBootstrapSettled = false;
  }
}
