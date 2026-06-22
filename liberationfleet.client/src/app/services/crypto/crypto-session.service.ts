import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CryptoApiService } from './crypto-api.service';
import { CryptoService } from './crypto.service';
import { UserKeyBundle } from '../../models/crypto.model';

@Injectable({
  providedIn: 'root'
})
export class CryptoSessionService {
  private identityPrivateKey: CryptoKey | null = null;
  private identityPublicKeySpki: string | null = null;
  private readonly crewKeys = new Map<number, CryptoKey>();

  constructor(
    private cryptoService: CryptoService,
    private cryptoApi: CryptoApiService
  ) {}

  isUnlocked(): boolean {
    return this.identityPrivateKey !== null;
  }

  clearSession(): void {
    this.identityPrivateKey = null;
    this.identityPublicKeySpki = null;
    this.crewKeys.clear();
  }

  async provisionIdentityKeys(password: string): Promise<void> {
    const keyPair = await this.cryptoService.generateIdentityKeyPair();
    this.identityPrivateKey = keyPair.privateKey;
    this.identityPublicKeySpki = await this.cryptoService.exportPublicKeySpki(keyPair.publicKey);

    const backup = await this.cryptoService.wrapPrivateKeyBackup(keyPair.privateKey, password);
    await firstValueFrom(this.cryptoApi.upsertPublicKey(this.identityPublicKeySpki));
    await firstValueFrom(this.cryptoApi.upsertPrivateKeyBackup({
      ...backup,
      keyVersion: 1
    }));
  }

  async unlockFromPassword(password: string): Promise<void> {
    const backup = await firstValueFrom(this.cryptoApi.getMyPrivateKeyBackup());
    this.identityPrivateKey = await this.cryptoService.unwrapPrivateKeyBackup(backup, password);
    this.identityPublicKeySpki = await this.cryptoService.exportPublicKeyFromPrivateKey(this.identityPrivateKey);
    await firstValueFrom(this.cryptoApi.upsertPublicKey(this.identityPublicKeySpki));
  }

  async ensureCrewKeyReady(crewId: number): Promise<CryptoKey> {
    if (!this.identityPrivateKey) {
      throw new Error('Encryption keys are locked.');
    }

    const cached = this.crewKeys.get(crewId);
    if (cached) {
      return cached;
    }

    const state = await firstValueFrom(this.cryptoApi.getCrewKeyState(crewId));
    const publicKeys = await firstValueFrom(this.cryptoApi.getCrewPublicKeys(crewId));
    const publicKeyByUserId = new Map(publicKeys.map(key => [key.userId, key]));

    if (state.myDistribution) {
      const keyVersion = state.myDistribution.keyVersion;
      const wrapperPublicKey = publicKeyByUserId.get(state.myDistribution.wrappedByUserId);
      if (!wrapperPublicKey) {
        throw new Error('Missing public key for crew key author.');
      }

      const crewKeyBytes = await this.cryptoService.unwrapCrewKey(
        state.myDistribution.wrappedCrewKey,
        state.myDistribution.wrapNonce,
        wrapperPublicKey.identityPublicKey,
        this.identityPrivateKey
      );
      const crewKey = await this.cryptoService.importCrewAesKey(crewKeyBytes);
      this.crewKeys.set(crewId, crewKey);
      await this.provisionMissingDistributions(crewId, keyVersion, crewKeyBytes, publicKeys, state.distributions);
      return crewKey;
    }

    if ((state.latestKeyVersion ?? 0) > 0) {
      throw new Error('Crew encryption key is not yet available for your account.');
    }

    const keyVersion = 1;
    const crewKeyBytes = this.cryptoService.generateCrewKeyBytes();
    await this.uploadCrewKeyDistributions(crewId, keyVersion, crewKeyBytes, publicKeys);
    const crewKey = await this.cryptoService.importCrewAesKey(crewKeyBytes);
    this.crewKeys.set(crewId, crewKey);
    return crewKey;
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
}
