import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { tap } from 'rxjs/operators';
import { AuthResult, User } from '../models/user.model';
import { CryptoSessionService } from './crypto/crypto-session.service';
import {
  BACKUP_WRAP_LEGACY_PASSWORD,
  BACKUP_WRAP_RECOVERY_KEY,
  normalizeRecoveryPhrase,
  validateRecoveryPhrase
} from './crypto/recovery-key.util';
import { CryptoApiService } from './crypto/crypto-api.service';
import { firstValueFrom } from 'rxjs';
import { AppStorageService, StorageScope } from './storage/app-storage.service';
import {
  AUTH_TOKEN_STORAGE_KEY,
  SESSION_RECOVERY_PHRASE_STORAGE_KEY
} from './storage/storage-keys';

export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiUrl = '/api/auth';
  private readonly currentUserSubject = new BehaviorSubject<User | null>(null);
  private encryptionReadyPromise: Promise<void> | null = null;

  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(
    private http: HttpClient,
    private cryptoSession: CryptoSessionService,
    private cryptoApi: CryptoApiService,
    private storage: AppStorageService
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
      this.setToken(response.token);
      this.currentUserSubject.next(response.user ?? null);
      this.resetEncryptionReady();
    }
  }

  logout(): void {
    this.removeToken();
    this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    this.cryptoSession.clearSession();
    this.currentUserSubject.next(null);
    this.resetEncryptionReady();
  }

  getToken(): string | null {
    return this.storage.get(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY);
  }

  setToken(token: string): void {
    this.storage.set(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY, token);
  }

  removeToken(): void {
    this.storage.remove(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY);
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  needsEncryptionUnlock(): boolean {
    return this.isAuthenticated() && !this.cryptoSession.isUnlocked();
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
    if (rememberOnDevice) {
      this.storage.set(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY, normalized);
    }
    this.resetEncryptionReady();
  }

  async unlockWithRecoveryPhrase(recoveryPhrase: string, rememberOnDevice = false): Promise<void> {
    const normalized = normalizeRecoveryPhrase(recoveryPhrase);
    if (!(await validateRecoveryPhrase(normalized))) {
      throw new Error('Invalid recovery phrase.');
    }

    await this.cryptoSession.unlockFromRecoveryPhrase(normalized);
    if (rememberOnDevice) {
      this.storage.set(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY, normalized);
    } else {
      this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    }
    this.resetEncryptionReady();
  }

  async unlockWithLegacyPassword(password: string, rememberOnDevice = false): Promise<void> {
    await this.cryptoSession.unlockFromLegacyPassword(password);
    if (rememberOnDevice) {
      this.storage.set(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY, `legacy:${password}`);
    }
    this.resetEncryptionReady();
  }

  async rotateRecoveryPhrase(recoveryPhrase: string): Promise<void> {
    const normalized = normalizeRecoveryPhrase(recoveryPhrase);
    if (!(await validateRecoveryPhrase(normalized))) {
      throw new Error('Invalid recovery phrase.');
    }

    await this.cryptoSession.rotateRecoveryPhrase(normalized);
    this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    this.resetEncryptionReady();
  }

  async getBackupWrapVersion(): Promise<number> {
    try {
      const backup = await firstValueFrom(this.cryptoApi.getMyPrivateKeyBackup());
      return backup.keyVersion ?? BACKUP_WRAP_LEGACY_PASSWORD;
    } catch {
      return BACKUP_WRAP_RECOVERY_KEY;
    }
  }

  updateCurrentUser(user: User): void {
    this.currentUserSubject.next(user);
  }

  private loadToken(): void {
    const token = this.getToken();
    if (token) {
      this.currentUserSubject.next(null);
      void this.getEncryptionReady();
    }
  }

  private async ensureEncryptionReady(): Promise<void> {
    if (!this.isAuthenticated() || this.cryptoSession.isUnlocked()) {
      return;
    }

    const stored = this.storage.get(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    if (!stored) {
      return;
    }

    try {
      if (stored.startsWith('legacy:')) {
        await this.cryptoSession.unlockFromLegacyPassword(stored.slice('legacy:'.length));
      } else {
        await this.cryptoSession.unlockFromRecoveryPhrase(stored);
      }
    } catch {
      this.storage.remove(StorageScope.Session, SESSION_RECOVERY_PHRASE_STORAGE_KEY);
    }
  }

  private resetEncryptionReady(): void {
    this.encryptionReadyPromise = null;
  }
}
