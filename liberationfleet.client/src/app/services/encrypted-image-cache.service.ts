import { Injectable } from '@angular/core';
import { ProposalCryptoService, ProposalCryptoScope } from './crypto/proposal-crypto.service';
import { EncryptedContentType } from '../models/crypto.model';

type AvatarContentType = Extract<EncryptedContentType, 'ProfileAvatar' | 'ImageAsset'>;

@Injectable({ providedIn: 'root' })
export class EncryptedImageCacheService {
  private readonly cache = new Map<string, Promise<string | null>>();

  constructor(private proposalCrypto: ProposalCryptoService) {}

  getDataUrl(
    scope: ProposalCryptoScope,
    resourceId: string | null | undefined,
    contentType: AvatarContentType = 'ProfileAvatar'
  ): Promise<string | null> {
    const id = resourceId?.trim();
    if (!id) {
      return Promise.resolve(null);
    }

    const key = `${contentType}|${scope.crewId ?? ''}|${scope.fleetId ?? ''}|${id}`;
    const cached = this.cache.get(key);
    if (cached) {
      return cached;
    }

    const pending = this.proposalCrypto.decryptImageDataUrl(scope, id, contentType).catch(() => null);
    this.cache.set(key, pending);
    // Don't keep permanent nulls from pre-unlock attempts — drop failed entries after settle.
    void pending.then(result => {
      if (result == null) {
        this.cache.delete(key);
      }
    });
    return pending;
  }

  /** Drop a cached image after the user uploads a replacement. */
  invalidate(resourceId: string | null | undefined, contentType: AvatarContentType = 'ProfileAvatar'): void {
    const id = resourceId?.trim();
    if (!id) {
      return;
    }
    for (const key of [...this.cache.keys()]) {
      if (key.endsWith(`|${id}`) && key.startsWith(`${contentType}|`)) {
        this.cache.delete(key);
      }
    }
  }

  clear(): void {
    this.cache.clear();
  }
}
