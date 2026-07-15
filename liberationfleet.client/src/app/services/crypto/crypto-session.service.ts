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

const CREW_KEY_POLL_ATTEMPTS = 30;
const CREW_KEY_POLL_INTERVAL_MS = 2000;

@Injectable({
  providedIn: 'root'
})
export class CryptoSessionService {
  private identityPrivateKey: CryptoKey | null = null;
  private identityPublicKeySpki: string | null = null;
  private readonly crewKeyMaterial = new Map<number, CrewKeyMaterial>();
  private readonly fleetKeyMaterial = new Map<number, FleetKeyMaterial>();
  private readonly unlockedSubject = new BehaviorSubject(false);
  private backupWrapVersion: number | null = null;

  readonly unlocked$ = this.unlockedSubject.asObservable();

  constructor(
    private cryptoService: CryptoService,
    private cryptoApi: CryptoApiService,
    private storage: AppStorageService
  ) {}

  isUnlocked(): boolean {
    return this.identityPrivateKey !== null;
  }

  usesLegacyPasswordBackup(): boolean {
    return this.backupWrapVersion === BACKUP_WRAP_LEGACY_PASSWORD;
  }

  clearSession(): void {
    this.identityPrivateKey = null;
    this.identityPublicKeySpki = null;
    this.backupWrapVersion = null;
    this.crewKeyMaterial.clear();
    this.fleetKeyMaterial.clear();
    this.unlockedSubject.next(false);
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

    const cached = this.crewKeyMaterial.get(crewId);
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

    const cached = this.fleetKeyMaterial.get(fleetId);
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

  private async resolveCrewKeyMaterial(crewId: number, waitForDistribution: boolean): Promise<CrewKeyMaterial> {
    const cached = this.crewKeyMaterial.get(crewId);
    if (cached) {
      return cached;
    }

    const state = waitForDistribution
      ? await this.waitForCrewKeyState(crewId)
      : await firstValueFrom(this.cryptoApi.getCrewKeyState(crewId));
    const publicKeys = await firstValueFrom(this.cryptoApi.getCrewPublicKeys(crewId));
    const publicKeyByUserId = new Map(publicKeys.map(key => [key.userId, key]));

    if (state.myDistribution) {
      try {
        return await this.unwrapDistribution(
          crewId,
          state.myDistribution,
          publicKeyByUserId
        );
      } catch {
        // Distribution may have been wrapped for a previous identity key pair.
      }
    }

    const soloRecovery = await this.tryRecoverSoloCrewKey(crewId, state, publicKeys);
    if (soloRecovery) {
      return soloRecovery;
    }

    if ((state.latestKeyVersion ?? 0) > 0) {
      throw new Error(
        'Crew encryption key is not yet available for your account. Ask a crewmate to open the app, then try again.'
      );
    }

    const keyVersion = 1;
    const crewKeyBytes = this.cryptoService.generateCrewKeyBytes();
    await this.uploadCrewKeyDistributions(crewId, keyVersion, crewKeyBytes, publicKeys);
    return await this.cacheCrewKeyMaterial(crewId, keyVersion, crewKeyBytes);
  }

  private async resolveFleetKeyMaterial(fleetId: number, waitForDistribution: boolean): Promise<FleetKeyMaterial> {
    const cached = this.fleetKeyMaterial.get(fleetId);
    if (cached) {
      return cached;
    }

    const state = waitForDistribution
      ? await this.waitForFleetKeyState(fleetId)
      : await firstValueFrom(this.cryptoApi.getFleetKeyState(fleetId));
    const publicKeys = await firstValueFrom(this.cryptoApi.getFleetPublicKeys(fleetId));
    const publicKeyByUserId = new Map(publicKeys.map(key => [key.userId, key]));

    if (state.myDistribution) {
      try {
        return await this.unwrapFleetDistribution(
          fleetId,
          state.myDistribution,
          publicKeyByUserId
        );
      } catch {
        // Distribution may have been wrapped for a previous identity key pair.
      }
    }

    const soloRecovery = await this.tryRecoverSoloFleetKey(fleetId, state, publicKeys);
    if (soloRecovery) {
      return soloRecovery;
    }

    if ((state.latestKeyVersion ?? 0) > 0) {
      throw new Error(
        'Fleet encryption key is not yet available for your account. Ask a fleet member to open the app, then try again.'
      );
    }

    const keyVersion = 1;
    const fleetKeyBytes = this.cryptoService.generateFleetKeyBytes();
    await this.uploadFleetKeyDistributions(fleetId, keyVersion, fleetKeyBytes, publicKeys);
    return await this.cacheFleetKeyMaterial(fleetId, keyVersion, fleetKeyBytes);
  }

  private async waitForFleetKeyState(fleetId: number): Promise<FleetKeyState> {
    for (let attempt = 0; attempt < CREW_KEY_POLL_ATTEMPTS; attempt++) {
      const state = await firstValueFrom(this.cryptoApi.getFleetKeyState(fleetId));

      if (state.myDistribution || (state.latestKeyVersion ?? 0) === 0) {
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
    fleetKeyBytes: Uint8Array
  ): Promise<FleetKeyMaterial> {
    const key = await this.cryptoService.importFleetAesKey(fleetKeyBytes);
    const material: FleetKeyMaterial = { key, bytes: fleetKeyBytes, keyVersion };
    this.fleetKeyMaterial.set(fleetId, material);
    return material;
  }

  private async waitForCrewKeyState(crewId: number): Promise<CrewKeyState> {
    for (let attempt = 0; attempt < CREW_KEY_POLL_ATTEMPTS; attempt++) {
      const state = await firstValueFrom(this.cryptoApi.getCrewKeyState(crewId));

      if (state.myDistribution || (state.latestKeyVersion ?? 0) === 0) {
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
    crewKeyBytes: Uint8Array
  ): Promise<CrewKeyMaterial> {
    const key = await this.cryptoService.importCrewAesKey(crewKeyBytes);
    const material: CrewKeyMaterial = { key, bytes: crewKeyBytes, keyVersion };
    this.crewKeyMaterial.set(crewId, material);
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

  private async unwrapDistribution(
    crewId: number,
    distribution: CrewKeyDistribution,
    publicKeyByUserId: Map<number, UserKeyBundle>
  ): Promise<CrewKeyMaterial> {
    const wrapperPublicKey = publicKeyByUserId.get(distribution.wrappedByUserId);
    if (!wrapperPublicKey) {
      throw new Error('Missing public key for crew key author.');
    }

    const crewKeyBytes = await this.cryptoService.unwrapCrewKey(
      distribution.wrappedCrewKey,
      distribution.wrapNonce,
      wrapperPublicKey.identityPublicKey,
      this.identityPrivateKey!
    );
    return this.cacheCrewKeyMaterial(crewId, distribution.keyVersion, crewKeyBytes);
  }

  private async unwrapFleetDistribution(
    fleetId: number,
    distribution: FleetKeyDistribution,
    publicKeyByUserId: Map<number, UserKeyBundle>
  ): Promise<FleetKeyMaterial> {
    const wrapperPublicKey = publicKeyByUserId.get(distribution.wrappedByUserId);
    if (!wrapperPublicKey) {
      throw new Error('Missing public key for fleet key author.');
    }

    const fleetKeyBytes = await this.cryptoService.unwrapFleetKey(
      distribution.wrappedFleetKey,
      distribution.wrapNonce,
      wrapperPublicKey.identityPublicKey,
      this.identityPrivateKey!
    );
    return this.cacheFleetKeyMaterial(fleetId, distribution.keyVersion, fleetKeyBytes);
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

    const nextVersion = Math.max(state.latestKeyVersion ?? 0, 0) + 1;
    const crewKeyBytes = this.cryptoService.generateCrewKeyBytes();
    await this.uploadCrewKeyDistributions(crewId, nextVersion, crewKeyBytes, publicKeys);
    return this.cacheCrewKeyMaterial(crewId, nextVersion, crewKeyBytes);
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

    const nextVersion = Math.max(state.latestKeyVersion ?? 0, 0) + 1;
    const fleetKeyBytes = this.cryptoService.generateFleetKeyBytes();
    await this.uploadFleetKeyDistributions(fleetId, nextVersion, fleetKeyBytes, publicKeys);
    return this.cacheFleetKeyMaterial(fleetId, nextVersion, fleetKeyBytes);
  }

  private getCurrentUserId(): number | null {
    const token = this.storage.get(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY);
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
