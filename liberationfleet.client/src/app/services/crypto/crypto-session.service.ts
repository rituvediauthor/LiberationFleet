import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';
import { firstValueFrom } from 'rxjs';
import { CryptoApiService } from './crypto-api.service';
import { CryptoService } from './crypto.service';
import { CrewKeyDistribution, CrewKeyState, FleetKeyDistribution, FleetKeyState, UserKeyBundle } from '../../models/crypto.model';
import { AppStorageService, StorageScope } from '../storage/app-storage.service';
import { AUTH_TOKEN_STORAGE_KEY } from '../storage/storage-keys';
import { getUserIdFromToken } from '../../utils/jwt.util';
import {
  BACKUP_WRAP_LEGACY_PASSWORD,
  BACKUP_WRAP_RECOVERY_KEY,
  recoveryPhraseToSecret
} from './recovery-key.util';
import { MediaBlobCacheService } from './media-blob-cache.service';

interface CrewKeyMaterial {
  key: CryptoKey;
  bytes: Uint8Array;
  keyVersion: number;
}

interface FleetKeyMaterial {
  key: CryptoKey;
  bytes: Uint8Array;
  keyVersion: number;
}

interface CrewKeyCache {
  latestVersion: number;
  byVersion: Map<number, CrewKeyMaterial>;
}

interface FleetKeyCache {
  latestVersion: number;
  byVersion: Map<number, FleetKeyMaterial>;
}

const CREW_KEY_POLL_ATTEMPTS = 30;
const CREW_KEY_POLL_INTERVAL_MS = 2000;

@Injectable({
  providedIn: 'root'
})
export class CryptoSessionService {
  private identityPrivateKey: CryptoKey | null = null;
  private identityPublicKeySpki: string | null = null;
  private readonly crewKeyCaches = new Map<number, CrewKeyCache>();
  private readonly fleetKeyCaches = new Map<number, FleetKeyCache>();
  private readonly friendDmKeyCache = new Map<number, CryptoKey>();
  private userContentKey: CryptoKey | null = null;
  private readonly unlockedSubject = new BehaviorSubject(false);
  private backupWrapVersion: number | null = null;

  readonly unlocked$ = this.unlockedSubject.asObservable();

  constructor(
    private cryptoService: CryptoService,
    private cryptoApi: CryptoApiService,
    private storage: AppStorageService,
    private mediaBlobCache: MediaBlobCacheService
  ) {}

  isUnlocked(): boolean {
    return this.identityPrivateKey !== null;
  }

  usesLegacyPasswordBackup(): boolean {
    return this.backupWrapVersion === BACKUP_WRAP_LEGACY_PASSWORD;
  }

  /** Current (latest) crew AES key version after ensureCrewKeyReady (null if not cached). */
  getCrewKeyVersion(crewId: number): number | null {
    return this.crewKeyCaches.get(crewId)?.latestVersion ?? null;
  }

  /** Current (latest) fleet AES key version after ensureFleetKeyReady (null if not cached). */
  getFleetKeyVersion(fleetId: number): number | null {
    return this.fleetKeyCaches.get(fleetId)?.latestVersion ?? null;
  }

  async ensureUserContentKeyReady(): Promise<CryptoKey> {
    if (this.userContentKey) {
      return this.userContentKey;
    }

    const privateKey = this.requireIdentityPrivateKey();
    const publicKeySpki = this.identityPublicKeySpki;
    if (!publicKeySpki) {
      throw new Error('Encryption is not unlocked.');
    }

    this.userContentKey = await this.cryptoService.deriveUserContentAesKey(privateKey, publicKeySpki);
    return this.userContentKey;
  }

  /** ECDH shared AES key for DMs with a friend (independent of crew keys). */
  async ensureFriendDmKeyReady(friendUserId: number): Promise<CryptoKey> {
    const cached = this.friendDmKeyCache.get(friendUserId);
    if (cached) {
      return cached;
    }

    const privateKey = this.requireIdentityPrivateKey();
    const friendKey = await this.fetchPublicKey(friendUserId);
    if (!friendKey?.identityPublicKey) {
      throw new Error('Friend encryption keys are not available.');
    }

    const dmKey = await this.cryptoService.deriveFriendDmAesKey(privateKey, friendKey.identityPublicKey);
    this.friendDmKeyCache.set(friendUserId, dmKey);
    return dmKey;
  }

  private requireIdentityPrivateKey(): CryptoKey {
    if (!this.identityPrivateKey) {
      throw new Error('Encryption is not unlocked.');
    }

    return this.identityPrivateKey;
  }

  clearSession(): void {
    this.identityPrivateKey = null;
    this.identityPublicKeySpki = null;
    this.backupWrapVersion = null;
    this.crewKeyCaches.clear();
    this.fleetKeyCaches.clear();
    this.friendDmKeyCache.clear();
    this.userContentKey = null;
    this.unlockedSubject.next(false);
    void this.mediaBlobCache.clear();
  }

  async provisionIdentityKeysWithRecoveryPhrase(recoveryPhrase: string): Promise<void> {
    const secret = recoveryPhraseToSecret(recoveryPhrase);
    const keyPair = await this.cryptoService.generateIdentityKeyPair();
    this.identityPrivateKey = keyPair.privateKey;
    this.identityPublicKeySpki = await this.cryptoService.exportPublicKeySpki(keyPair.publicKey);

    const backup = await this.cryptoService.wrapPrivateKeyBackup(
      keyPair.privateKey,
      secret,
      BACKUP_WRAP_RECOVERY_KEY
    );
    await firstValueFrom(this.cryptoApi.upsertPublicKey(this.identityPublicKeySpki));
    const backupResult = await firstValueFrom(this.cryptoApi.upsertPrivateKeyBackup({
      salt: backup.salt,
      iv: backup.iv,
      ciphertext: backup.ciphertext,
      keyVersion: backup.keyVersion
    }));
    this.assertCryptoOperationSucceeded(backupResult, 'Failed to save encryption backup.');

    this.backupWrapVersion = BACKUP_WRAP_RECOVERY_KEY;
    this.unlockedSubject.next(true);
  }

  async unlockFromRecoveryPhrase(recoveryPhrase: string): Promise<void> {
    await this.unlockFromSecret(recoveryPhraseToSecret(recoveryPhrase), BACKUP_WRAP_RECOVERY_KEY);
  }

  async unlockFromLegacyPassword(password: string): Promise<void> {
    await this.unlockFromSecret(password, BACKUP_WRAP_LEGACY_PASSWORD);
  }

  async rotateRecoveryPhrase(recoveryPhrase: string): Promise<void> {
    if (!this.identityPrivateKey) {
      throw new Error('Encryption keys are locked.');
    }

    const secret = recoveryPhraseToSecret(recoveryPhrase);
    const backup = await this.cryptoService.wrapPrivateKeyBackup(
      this.identityPrivateKey,
      secret,
      BACKUP_WRAP_RECOVERY_KEY
    );

    await firstValueFrom(this.cryptoApi.upsertPrivateKeyBackup({
      salt: backup.salt,
      iv: backup.iv,
      ciphertext: backup.ciphertext,
      keyVersion: backup.keyVersion
    }));

    this.backupWrapVersion = BACKUP_WRAP_RECOVERY_KEY;
  }

  async ensureCrewKeyReady(crewId: number): Promise<CryptoKey> {
    if (!this.identityPrivateKey) {
      throw new Error('Encryption keys are locked.');
    }

    const material = await this.resolveCrewKeyMaterial(crewId, true);
    await this.provisionMissingDistributionsForCrew(crewId, material);
    return material.key;
  }

  async syncCrewKeyDistributions(crewId: number): Promise<void> {
    if (!this.identityPrivateKey) {
      return;
    }

    const cached = this.crewKeyCaches.get(crewId)?.byVersion.get(
      this.crewKeyCaches.get(crewId)!.latestVersion
    );
    if (cached) {
      await this.provisionMissingDistributionsForCrew(crewId, cached);
      return;
    }

    try {
      await this.ensureCrewKeyReady(crewId);
    } catch {
      // Existing members without cached keys cannot help yet; new members may still be waiting.
    }
  }

  /** Wrap and upload the current crew key for a specific user (e.g. pending invitee). */
  async distributeCrewKeyToUser(crewId: number, userId: number): Promise<void> {
    if (!this.identityPrivateKey) {
      return;
    }

    const material = await this.resolveCrewKeyMaterial(crewId, false);
    let memberKey: UserKeyBundle;
    try {
      memberKey = await firstValueFrom(this.cryptoApi.getPublicKey(userId));
    } catch {
      return;
    }

    await this.uploadSingleDistribution(crewId, material.keyVersion, material.bytes, memberKey);
  }

  async ensureFleetKeyReady(fleetId: number): Promise<CryptoKey> {
    if (!this.identityPrivateKey) {
      throw new Error('Encryption keys are locked.');
    }

    const material = await this.resolveFleetKeyMaterial(fleetId, true);
    await this.provisionMissingDistributionsForFleet(fleetId, material);
    return material.key;
  }

  async syncFleetKeyDistributions(fleetId: number): Promise<void> {
    if (!this.identityPrivateKey) {
      return;
    }

    const cache = this.fleetKeyCaches.get(fleetId);
    const cached = cache?.byVersion.get(cache.latestVersion);
    if (cached) {
      await this.provisionMissingDistributionsForFleet(fleetId, cached);
      return;
    }

    try {
      await this.ensureFleetKeyReady(fleetId);
    } catch {
      // Existing members without cached keys cannot help yet; new members may still be waiting.
    }
  }

  /**
   * Decrypt using the preferred key version when available, then fall back across
   * every historical crew key we can unwrap. Recovers pre-rotation ciphertext after
   * accidental key-version bumps that left older wraps intact.
   */
  async decryptWithCrewKeyFallback<T>(
    crewId: number,
    preferredKeyVersion: number | null | undefined,
    decrypt: (key: CryptoKey) => Promise<T>
  ): Promise<T> {
    const keys = await this.getCrewDecryptKeys(crewId, preferredKeyVersion);
    return this.tryDecryptWithKeys(keys, decrypt);
  }

  async decryptWithFleetKeyFallback<T>(
    fleetId: number,
    preferredKeyVersion: number | null | undefined,
    decrypt: (key: CryptoKey) => Promise<T>
  ): Promise<T> {
    const keys = await this.getFleetDecryptKeys(fleetId, preferredKeyVersion);
    return this.tryDecryptWithKeys(keys, decrypt);
  }

  /** Load latest + historical crew keys into cache without performing a decrypt. */
  async warmCrewKeys(crewId: number): Promise<void> {
    try {
      await this.ensureCrewKeyReady(crewId);
    } catch {
      await this.loadHistoricalCrewKeysForDecrypt(crewId);
    }

    if ((this.crewKeyCaches.get(crewId)?.byVersion.size ?? 0) === 0) {
      throw new Error('Crew encryption key is not available.');
    }
  }

  /** Load latest + historical fleet keys into cache without performing a decrypt. */
  async warmFleetKeys(fleetId: number): Promise<void> {
    try {
      await this.ensureFleetKeyReady(fleetId);
    } catch {
      await this.loadHistoricalFleetKeysForDecrypt(fleetId);
    }

    if ((this.fleetKeyCaches.get(fleetId)?.byVersion.size ?? 0) === 0) {
      throw new Error('Fleet encryption key is not available.');
    }
  }

  private async getCrewDecryptKeys(
    crewId: number,
    preferredKeyVersion: number | null | undefined
  ): Promise<CryptoKey[]> {
    try {
      await this.ensureCrewKeyReady(crewId);
    } catch {
      await this.loadHistoricalCrewKeysForDecrypt(crewId);
    }

    const cache = this.crewKeyCaches.get(crewId);
    if (!cache || cache.byVersion.size === 0) {
      throw new Error('Crew encryption key is not available.');
    }

    return this.orderKeysForDecrypt(
      [...cache.byVersion.values()],
      preferredKeyVersion,
      cache.latestVersion
    ).map(material => material.key);
  }

  private async getFleetDecryptKeys(
    fleetId: number,
    preferredKeyVersion: number | null | undefined
  ): Promise<CryptoKey[]> {
    try {
      await this.ensureFleetKeyReady(fleetId);
    } catch {
      await this.loadHistoricalFleetKeysForDecrypt(fleetId);
    }

    const cache = this.fleetKeyCaches.get(fleetId);
    if (!cache || cache.byVersion.size === 0) {
      throw new Error('Fleet encryption key is not available.');
    }

    return this.orderKeysForDecrypt(
      [...cache.byVersion.values()],
      preferredKeyVersion,
      cache.latestVersion
    ).map(material => material.key);
  }

  private async loadHistoricalCrewKeysForDecrypt(crewId: number): Promise<void> {
    if (!this.identityPrivateKey) {
      throw new Error('Encryption keys are locked.');
    }

    const state = await firstValueFrom(this.cryptoApi.getCrewKeyState(crewId));
    const publicKeys = await firstValueFrom(this.cryptoApi.getCrewPublicKeys(crewId));
    const publicKeyByUserId = new Map(publicKeys.map(key => [key.userId, key]));
    await this.tryLoadHistoricalCrewKeysOnly(crewId, state, publicKeyByUserId);
  }

  private async loadHistoricalFleetKeysForDecrypt(fleetId: number): Promise<void> {
    if (!this.identityPrivateKey) {
      throw new Error('Encryption keys are locked.');
    }

    const state = await firstValueFrom(this.cryptoApi.getFleetKeyState(fleetId));
    const publicKeys = await firstValueFrom(this.cryptoApi.getFleetPublicKeys(fleetId));
    const publicKeyByUserId = new Map(publicKeys.map(key => [key.userId, key]));
    await this.tryLoadHistoricalFleetKeysOnly(fleetId, state, publicKeyByUserId);
  }

  private orderKeysForDecrypt<T extends { keyVersion: number; key: CryptoKey }>(
    materials: T[],
    preferredKeyVersion: number | null | undefined,
    latestVersion: number
  ): T[] {
    const preferred = preferredKeyVersion && preferredKeyVersion > 0
      ? preferredKeyVersion
      : null;
    return [...materials].sort((a, b) => {
      const score = (m: T): number => {
        if (preferred != null && m.keyVersion === preferred) {
          return 0;
        }
        if (m.keyVersion === latestVersion) {
          return 1;
        }
        // Prefer older versions next — pre-audit content usually lives there.
        return 1000 - m.keyVersion;
      };
      return score(a) - score(b);
    });
  }

  private async tryDecryptWithKeys<T>(
    keys: CryptoKey[],
    decrypt: (key: CryptoKey) => Promise<T>
  ): Promise<T> {
    let lastError: unknown;
    for (const key of keys) {
      try {
        return await decrypt(key);
      } catch (error: unknown) {
        lastError = error;
      }
    }

    throw lastError instanceof Error
      ? lastError
      : new Error('Unable to decrypt with any available key version.');
  }

  private async resolveCrewKeyMaterial(crewId: number, waitForDistribution: boolean): Promise<CrewKeyMaterial> {
    const existing = this.crewKeyCaches.get(crewId);
    if (existing?.byVersion.has(existing.latestVersion)) {
      return existing.byVersion.get(existing.latestVersion)!;
    }

    const state = waitForDistribution
      ? await this.waitForCrewKeyState(crewId)
      : await firstValueFrom(this.cryptoApi.getCrewKeyState(crewId));
    const publicKeys = await firstValueFrom(this.cryptoApi.getCrewPublicKeys(crewId));
    const publicKeyByUserId = new Map(publicKeys.map(key => [key.userId, key]));

    if (state.myDistribution) {
      try {
        const latest = await this.unwrapDistribution(
          crewId,
          state.myDistribution,
          publicKeyByUserId,
          state.latestKeyVersion ?? state.myDistribution.keyVersion
        );
        await this.cacheHistoricalCrewDistributions(crewId, state, publicKeyByUserId, latest.keyVersion);
        return latest;
      } catch (error: unknown) {
        // Never mint a replacement crew key when a distribution already exists —
        // that permanently orphans all prior ciphertext. Surface identity mismatch instead.
        const detail = error instanceof Error ? error.message : 'unwrap failed';
        throw new Error(
          `Could not unlock the crew encryption key (${detail}). ` +
          'Confirm you unlocked with the same recovery phrase used when this crew key was created, then try again.'
        );
      }
    }

    // Only mint a fresh key when this crew has never distributed one (or this solo
    // member has no wrap yet and is allowed to bootstrap). Never after a failed unwrap.
    const soloRecovery = await this.tryRecoverSoloCrewKey(crewId, state, publicKeys);
    if (soloRecovery) {
      return soloRecovery;
    }

    // Latest wrap missing for this user, but older wraps may still decrypt history.
    const historical = await this.tryLoadHistoricalCrewKeysOnly(crewId, state, publicKeyByUserId);
    if (historical) {
      return historical;
    }

    if ((state.latestKeyVersion ?? 0) > 0) {
      throw new Error(
        'Crew encryption key is not yet available for your account. Ask a crewmate to open the app, then try again.'
      );
    }

    const keyVersion = 1;
    const crewKeyBytes = this.cryptoService.generateCrewKeyBytes();
    await this.uploadCrewKeyDistributions(crewId, keyVersion, crewKeyBytes, publicKeys);
    return await this.cacheCrewKeyMaterial(crewId, keyVersion, crewKeyBytes, keyVersion);
  }

  private async resolveFleetKeyMaterial(fleetId: number, waitForDistribution: boolean): Promise<FleetKeyMaterial> {
    const existing = this.fleetKeyCaches.get(fleetId);
    if (existing?.byVersion.has(existing.latestVersion)) {
      return existing.byVersion.get(existing.latestVersion)!;
    }

    const state = waitForDistribution
      ? await this.waitForFleetKeyState(fleetId)
      : await firstValueFrom(this.cryptoApi.getFleetKeyState(fleetId));
    const publicKeys = await firstValueFrom(this.cryptoApi.getFleetPublicKeys(fleetId));
    const publicKeyByUserId = new Map(publicKeys.map(key => [key.userId, key]));

    if (state.myDistribution) {
      try {
        const latest = await this.unwrapFleetDistribution(
          fleetId,
          state.myDistribution,
          publicKeyByUserId,
          state.latestKeyVersion ?? state.myDistribution.keyVersion
        );
        await this.cacheHistoricalFleetDistributions(fleetId, state, publicKeyByUserId, latest.keyVersion);
        return latest;
      } catch (error: unknown) {
        const detail = error instanceof Error ? error.message : 'unwrap failed';
        throw new Error(
          `Could not unlock the fleet encryption key (${detail}). ` +
          'Confirm you unlocked with the same recovery phrase used when this fleet key was created, then try again.'
        );
      }
    }

    const soloRecovery = await this.tryRecoverSoloFleetKey(fleetId, state, publicKeys);
    if (soloRecovery) {
      return soloRecovery;
    }

    const historical = await this.tryLoadHistoricalFleetKeysOnly(fleetId, state, publicKeyByUserId);
    if (historical) {
      return historical;
    }

    if ((state.latestKeyVersion ?? 0) > 0) {
      throw new Error(
        'Fleet encryption key is not yet available for your account. Ask a fleet member to open the app, then try again.'
      );
    }

    const keyVersion = 1;
    const fleetKeyBytes = this.cryptoService.generateFleetKeyBytes();
    await this.uploadFleetKeyDistributions(fleetId, keyVersion, fleetKeyBytes, publicKeys);
    return await this.cacheFleetKeyMaterial(fleetId, keyVersion, fleetKeyBytes, keyVersion);
  }

  private async tryLoadHistoricalCrewKeysOnly(
    crewId: number,
    state: CrewKeyState,
    publicKeyByUserId: Map<number, UserKeyBundle>
  ): Promise<CrewKeyMaterial | null> {
    const historical = this.collectHistoricalCrewDistributions(state);
    if (historical.length === 0) {
      return null;
    }

    let first: CrewKeyMaterial | null = null;
    for (const distribution of historical) {
      try {
        const material = await this.unwrapDistribution(
          crewId,
          distribution,
          publicKeyByUserId,
          state.latestKeyVersion ?? distribution.keyVersion
        );
        first ??= material;
      } catch {
        // Keep trying older wraps.
      }
    }

    return first;
  }

  private async tryLoadHistoricalFleetKeysOnly(
    fleetId: number,
    state: FleetKeyState,
    publicKeyByUserId: Map<number, UserKeyBundle>
  ): Promise<FleetKeyMaterial | null> {
    const historical = this.collectHistoricalFleetDistributions(state);
    if (historical.length === 0) {
      return null;
    }

    let first: FleetKeyMaterial | null = null;
    for (const distribution of historical) {
      try {
        const material = await this.unwrapFleetDistribution(
          fleetId,
          distribution,
          publicKeyByUserId,
          state.latestKeyVersion ?? distribution.keyVersion
        );
        first ??= material;
      } catch {
        // Keep trying older wraps.
      }
    }

    return first;
  }

  private collectHistoricalCrewDistributions(state: CrewKeyState): CrewKeyDistribution[] {
    const byVersion = new Map<number, CrewKeyDistribution>();
    for (const distribution of state.myHistoricalDistributions ?? []) {
      byVersion.set(distribution.keyVersion, distribution);
    }
    if (state.myDistribution) {
      byVersion.set(state.myDistribution.keyVersion, state.myDistribution);
    }
    return [...byVersion.values()].sort((a, b) => b.keyVersion - a.keyVersion);
  }

  private collectHistoricalFleetDistributions(state: FleetKeyState): FleetKeyDistribution[] {
    const byVersion = new Map<number, FleetKeyDistribution>();
    for (const distribution of state.myHistoricalDistributions ?? []) {
      byVersion.set(distribution.keyVersion, distribution);
    }
    if (state.myDistribution) {
      byVersion.set(state.myDistribution.keyVersion, state.myDistribution);
    }
    return [...byVersion.values()].sort((a, b) => b.keyVersion - a.keyVersion);
  }

  private async cacheHistoricalCrewDistributions(
    crewId: number,
    state: CrewKeyState,
    publicKeyByUserId: Map<number, UserKeyBundle>,
    latestKeyVersion: number
  ): Promise<void> {
    for (const distribution of this.collectHistoricalCrewDistributions(state)) {
      if (distribution.keyVersion === latestKeyVersion) {
        continue;
      }
      try {
        await this.unwrapDistribution(crewId, distribution, publicKeyByUserId, latestKeyVersion);
      } catch {
        // Historical wrap may belong to a prior identity; skip without failing the session.
      }
    }
  }

  private async cacheHistoricalFleetDistributions(
    fleetId: number,
    state: FleetKeyState,
    publicKeyByUserId: Map<number, UserKeyBundle>,
    latestKeyVersion: number
  ): Promise<void> {
    for (const distribution of this.collectHistoricalFleetDistributions(state)) {
      if (distribution.keyVersion === latestKeyVersion) {
        continue;
      }
      try {
        await this.unwrapFleetDistribution(fleetId, distribution, publicKeyByUserId, latestKeyVersion);
      } catch {
        // Historical wrap may belong to a prior identity; skip without failing the session.
      }
    }
  }

  private async waitForFleetKeyState(fleetId: number): Promise<FleetKeyState> {
    for (let attempt = 0; attempt < CREW_KEY_POLL_ATTEMPTS; attempt++) {
      const state = await firstValueFrom(this.cryptoApi.getFleetKeyState(fleetId));

      if (
        state.myDistribution
        || (state.myHistoricalDistributions?.length ?? 0) > 0
        || (state.latestKeyVersion ?? 0) === 0
      ) {
        return state;
      }

      if (attempt < CREW_KEY_POLL_ATTEMPTS - 1) {
        await this.sleep(CREW_KEY_POLL_INTERVAL_MS);
      }
    }

    return firstValueFrom(this.cryptoApi.getFleetKeyState(fleetId));
  }

  private async provisionMissingDistributionsForFleet(fleetId: number, material: FleetKeyMaterial): Promise<void> {
    const state = await firstValueFrom(this.cryptoApi.getFleetKeyState(fleetId));
    const latestVersion = state.latestKeyVersion ?? material.keyVersion;
    if (latestVersion !== material.keyVersion) {
      return;
    }

    const publicKeys = await firstValueFrom(this.cryptoApi.getFleetPublicKeys(fleetId));
    await this.provisionMissingFleetDistributions(
      fleetId,
      material.keyVersion,
      material.bytes,
      publicKeys,
      state.distributions
    );
  }

  private async cacheFleetKeyMaterial(
    fleetId: number,
    keyVersion: number,
    fleetKeyBytes: Uint8Array,
    serverLatestVersion: number
  ): Promise<FleetKeyMaterial> {
    const key = await this.cryptoService.importFleetAesKey(fleetKeyBytes);
    const material: FleetKeyMaterial = { key, bytes: fleetKeyBytes, keyVersion };
    let cache = this.fleetKeyCaches.get(fleetId);
    if (!cache) {
      cache = { latestVersion: keyVersion, byVersion: new Map() };
      this.fleetKeyCaches.set(fleetId, cache);
    }
    cache.byVersion.set(keyVersion, material);
    // Only advertise a version as "latest" when we actually hold that key.
    cache.latestVersion = cache.byVersion.has(serverLatestVersion)
      ? serverLatestVersion
      : Math.max(...cache.byVersion.keys());
    return material;
  }

  private async waitForCrewKeyState(crewId: number): Promise<CrewKeyState> {
    for (let attempt = 0; attempt < CREW_KEY_POLL_ATTEMPTS; attempt++) {
      const state = await firstValueFrom(this.cryptoApi.getCrewKeyState(crewId));

      if (
        state.myDistribution
        || (state.myHistoricalDistributions?.length ?? 0) > 0
        || (state.latestKeyVersion ?? 0) === 0
      ) {
        return state;
      }

      if (attempt < CREW_KEY_POLL_ATTEMPTS - 1) {
        await this.sleep(CREW_KEY_POLL_INTERVAL_MS);
      }
    }

    return firstValueFrom(this.cryptoApi.getCrewKeyState(crewId));
  }

  private async provisionMissingDistributionsForCrew(crewId: number, material: CrewKeyMaterial): Promise<void> {
    const state = await firstValueFrom(this.cryptoApi.getCrewKeyState(crewId));
    const latestVersion = state.latestKeyVersion ?? material.keyVersion;
    if (latestVersion !== material.keyVersion) {
      return;
    }

    const publicKeys = await firstValueFrom(this.cryptoApi.getCrewPublicKeys(crewId));
    await this.provisionMissingDistributions(
      crewId,
      material.keyVersion,
      material.bytes,
      publicKeys,
      state.distributions
    );
  }

  private async cacheCrewKeyMaterial(
    crewId: number,
    keyVersion: number,
    crewKeyBytes: Uint8Array,
    serverLatestVersion: number
  ): Promise<CrewKeyMaterial> {
    const key = await this.cryptoService.importCrewAesKey(crewKeyBytes);
    const material: CrewKeyMaterial = { key, bytes: crewKeyBytes, keyVersion };
    let cache = this.crewKeyCaches.get(crewId);
    if (!cache) {
      cache = { latestVersion: keyVersion, byVersion: new Map() };
      this.crewKeyCaches.set(crewId, cache);
    }
    cache.byVersion.set(keyVersion, material);
    // Only advertise a version as "latest" when we actually hold that key.
    cache.latestVersion = cache.byVersion.has(serverLatestVersion)
      ? serverLatestVersion
      : Math.max(...cache.byVersion.keys());
    return material;
  }

  private async unlockFromSecret(secret: string, expectedWrapVersion: number): Promise<void> {
    let backup;
    try {
      backup = await firstValueFrom(this.cryptoApi.getMyPrivateKeyBackup());
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        if (expectedWrapVersion !== BACKUP_WRAP_RECOVERY_KEY) {
          throw new Error('Incorrect unlock method for this account.');
        }
        await this.provisionIdentityKeysWithRecoveryPhrase(secret);
        return;
      }
      throw error;
    }

    const wrapVersion = backup.keyVersion ?? BACKUP_WRAP_LEGACY_PASSWORD;
    if (wrapVersion !== expectedWrapVersion) {
      throw new Error('Incorrect unlock method for this account.');
    }

    try {
      this.identityPrivateKey = await this.cryptoService.unwrapPrivateKeyBackup(backup, secret);
    } catch {
      throw new Error('Invalid recovery key. Check all 12 words and try again.');
    }

    this.identityPublicKeySpki = await this.cryptoService.exportPublicKeyFromPrivateKey(this.identityPrivateKey);
    await firstValueFrom(this.cryptoApi.upsertPublicKey(this.identityPublicKeySpki));
    this.backupWrapVersion = wrapVersion;
    this.unlockedSubject.next(true);
  }

  private async fetchPublicKey(userId: number): Promise<UserKeyBundle | undefined> {
    try {
      return await firstValueFrom(this.cryptoApi.getPublicKey(userId));
    } catch {
      return undefined;
    }
  }

  private async unwrapDistribution(
    crewId: number,
    distribution: CrewKeyDistribution,
    publicKeyByUserId: Map<number, UserKeyBundle>,
    latestVersion: number
  ): Promise<CrewKeyMaterial> {
    const wrapperPublicKey = publicKeyByUserId.get(distribution.wrappedByUserId)
      ?? await this.fetchPublicKey(distribution.wrappedByUserId);
    if (!wrapperPublicKey) {
      throw new Error('Missing public key for crew key author.');
    }

    const crewKeyBytes = await this.cryptoService.unwrapCrewKey(
      distribution.wrappedCrewKey,
      distribution.wrapNonce,
      wrapperPublicKey.identityPublicKey,
      this.identityPrivateKey!
    );
    return this.cacheCrewKeyMaterial(crewId, distribution.keyVersion, crewKeyBytes, latestVersion);
  }

  private async unwrapFleetDistribution(
    fleetId: number,
    distribution: FleetKeyDistribution,
    publicKeyByUserId: Map<number, UserKeyBundle>,
    latestVersion: number
  ): Promise<FleetKeyMaterial> {
    const wrapperPublicKey = publicKeyByUserId.get(distribution.wrappedByUserId)
      ?? await this.fetchPublicKey(distribution.wrappedByUserId);
    if (!wrapperPublicKey) {
      throw new Error('Missing public key for fleet key author.');
    }

    const fleetKeyBytes = await this.cryptoService.unwrapFleetKey(
      distribution.wrappedFleetKey,
      distribution.wrapNonce,
      wrapperPublicKey.identityPublicKey,
      this.identityPrivateKey!
    );
    return this.cacheFleetKeyMaterial(fleetId, distribution.keyVersion, fleetKeyBytes, latestVersion);
  }

  private async tryRecoverSoloCrewKey(
    crewId: number,
    state: CrewKeyState,
    publicKeys: UserKeyBundle[]
  ): Promise<CrewKeyMaterial | null> {
    const currentUserId = this.getCurrentUserId();
    if (!currentUserId || publicKeys.length !== 1 || publicKeys[0].userId !== currentUserId) {
      return null;
    }

    // A prior key version means ciphertext already exists. Minting a replacement
    // would make that history permanently undecryptable.
    if ((state.latestKeyVersion ?? 0) > 0) {
      return null;
    }

    const keyVersion = 1;
    const crewKeyBytes = this.cryptoService.generateCrewKeyBytes();
    await this.uploadCrewKeyDistributions(crewId, keyVersion, crewKeyBytes, publicKeys);
    return this.cacheCrewKeyMaterial(crewId, keyVersion, crewKeyBytes, keyVersion);
  }

  private async tryRecoverSoloFleetKey(
    fleetId: number,
    state: FleetKeyState,
    publicKeys: UserKeyBundle[]
  ): Promise<FleetKeyMaterial | null> {
    const currentUserId = this.getCurrentUserId();
    if (!currentUserId || publicKeys.length !== 1 || publicKeys[0].userId !== currentUserId) {
      return null;
    }

    if ((state.latestKeyVersion ?? 0) > 0) {
      return null;
    }

    const keyVersion = 1;
    const fleetKeyBytes = this.cryptoService.generateFleetKeyBytes();
    await this.uploadFleetKeyDistributions(fleetId, keyVersion, fleetKeyBytes, publicKeys);
    return this.cacheFleetKeyMaterial(fleetId, keyVersion, fleetKeyBytes, keyVersion);
  }

  private getCurrentUserId(): number | null {
    // Auth may store the JWT in session (remember-login off) or persistent storage.
    const token = this.storage.get(StorageScope.Session, AUTH_TOKEN_STORAGE_KEY)
      ?? this.storage.get(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY);
    return token ? getUserIdFromToken(token) : null;
  }

  private assertCryptoOperationSucceeded(
    response: { success?: boolean; Success?: boolean; message?: string; Message?: string },
    fallbackMessage: string
  ): void {
    const success = response.success ?? response.Success ?? false;
    if (!success) {
      throw new Error(response.message ?? response.Message ?? fallbackMessage);
    }
  }

  private async uploadCrewKeyDistributions(
    crewId: number,
    keyVersion: number,
    crewKeyBytes: Uint8Array,
    publicKeys: UserKeyBundle[]
  ): Promise<void> {
    for (const memberKey of publicKeys) {
      await this.uploadSingleDistribution(crewId, keyVersion, crewKeyBytes, memberKey);
    }
  }

  private async uploadSingleDistribution(
    crewId: number,
    keyVersion: number,
    crewKeyBytes: Uint8Array,
    memberKey: UserKeyBundle
  ): Promise<void> {
    if (!this.identityPrivateKey) {
      return;
    }

    const wrapped = await this.cryptoService.wrapCrewKeyForUser(
      crewKeyBytes,
      memberKey.identityPublicKey,
      this.identityPrivateKey
    );
    await firstValueFrom(this.cryptoApi.upsertCrewKeyDistribution(crewId, {
      userId: memberKey.userId,
      keyVersion,
      wrappedCrewKey: wrapped.wrappedCrewKey,
      wrapNonce: wrapped.wrapNonce
    }));
  }

  private async uploadFleetKeyDistributions(
    fleetId: number,
    keyVersion: number,
    fleetKeyBytes: Uint8Array,
    publicKeys: UserKeyBundle[]
  ): Promise<void> {
    for (const memberKey of publicKeys) {
      await this.uploadSingleFleetDistribution(fleetId, keyVersion, fleetKeyBytes, memberKey);
    }
  }

  private async uploadSingleFleetDistribution(
    fleetId: number,
    keyVersion: number,
    fleetKeyBytes: Uint8Array,
    memberKey: UserKeyBundle
  ): Promise<void> {
    if (!this.identityPrivateKey) {
      return;
    }

    const wrapped = await this.cryptoService.wrapFleetKeyForUser(
      fleetKeyBytes,
      memberKey.identityPublicKey,
      this.identityPrivateKey
    );
    await firstValueFrom(this.cryptoApi.upsertFleetKeyDistribution(fleetId, {
      userId: memberKey.userId,
      keyVersion,
      wrappedFleetKey: wrapped.wrappedFleetKey,
      wrapNonce: wrapped.wrapNonce
    }));
  }

  private async provisionMissingFleetDistributions(
    fleetId: number,
    keyVersion: number,
    fleetKeyBytes: Uint8Array,
    publicKeys: UserKeyBundle[],
    distributions: { userId: number; keyVersion: number }[]
  ): Promise<void> {
    const distributedUserIds = new Set(
      distributions
        .filter(d => d.keyVersion === keyVersion)
        .map(d => d.userId)
    );

    for (const memberKey of publicKeys) {
      if (distributedUserIds.has(memberKey.userId)) {
        continue;
      }
      await this.uploadSingleFleetDistribution(fleetId, keyVersion, fleetKeyBytes, memberKey);
    }
  }

  private async provisionMissingDistributions(
    crewId: number,
    keyVersion: number,
    crewKeyBytes: Uint8Array,
    publicKeys: UserKeyBundle[],
    distributions: { userId: number; keyVersion: number }[]
  ): Promise<void> {
    const distributedUserIds = new Set(
      distributions
        .filter(d => d.keyVersion === keyVersion)
        .map(d => d.userId)
    );

    for (const memberKey of publicKeys) {
      if (distributedUserIds.has(memberKey.userId)) {
        continue;
      }
      await this.uploadSingleDistribution(crewId, keyVersion, crewKeyBytes, memberKey);
    }
  }

  private sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}
