import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';
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
import { CrewService } from './crew.service';
import { FleetService } from './fleet.service';
import { ProfileService } from './profile.service';
import { EncryptedImageCacheService } from './encrypted-image-cache.service';
import { ContentPreferenceService } from './content-preference.service';
import { NotificationService } from './notification.service';

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
  private readonly currentUserSubject = new BehaviorSubject<User | null>(null);
  private encryptionReadyPromise: Promise<void> | null = null;
  /** False until local session/device unlock has been attempted (or is unnecessary). */
  private encryptionBootstrapSettled = false;

  public currentUser$ = this.currentUserSubject.asObservable();

  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
  private profileService = inject(ProfileService);
  private imageCache = inject(EncryptedImageCacheService);
  private contentPreferences = inject(ContentPreferenceService);
  private notificationService = inject(NotificationService);

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
      tap(response => this.establishSession(response))
    );
  }

  establishSession(response: AuthResult): void {
    if (response.token) {
      // Drop any prior session caches so membership/fleet status cannot stick
      // across accounts or survive a failed pre-login probe as "not in a crew".
      this.clearSessionCaches();
      this.setToken(response.token);
      this.currentUserSubject.next(response.user ?? null);
      this.resetEncryptionReady();
      void this.getEncryptionReady();
    }
  }

  logout(): void {
    this.removeToken();
    this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    this.cryptoSession.clearSession();
    this.currentUserSubject.next(null);
    this.resetEncryptionReady();
    this.clearSessionCaches();
  }

  private clearSessionCaches(): void {
    this.crewService.clearSessionCache();
    this.fleetService.clearSessionCache();
    this.profileService.clearSessionCache();
    this.imageCache.clear();
    this.contentPreferences.clearSessionCache();
    this.notificationService.clearSessionCache();
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
    return !!this.getToken();
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
  }

  private async ensureEncryptionReady(): Promise<void> {
    try {
      if (!this.isAuthenticated() || this.cryptoSession.isUnlocked()) {
        return;
      }

      const stored = this.storage.get(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
      if (stored) {
        try {
          await this.cryptoSession.unlockFromRecoveryPhrase(stored);
          return;
        } catch (error: unknown) {
          if (this.shouldClearRememberedRecoveryPhrase(error)) {
            this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
          }
        }
      }

      await this.tryAutoUnlockFromPersistentStorage();
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
    } catch {
      this.savedRecoveryPhrase.removePhrase(userId);
    }
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
  }

  private getTokenStorageScope(): StorageScope {
    return this.isRememberLoginEnabled() ? StorageScope.Persistent : StorageScope.Session;
  }

  private shouldClearRememberedRecoveryPhrase(error: unknown): boolean {
    const message = error instanceof Error ? error.message : '';
    return message.includes('Invalid recovery key')
      || message.includes('Incorrect unlock method');
  }

  private resetEncryptionReady(): void {
    this.encryptionReadyPromise = null;
    this.encryptionBootstrapSettled = false;
  }
}
