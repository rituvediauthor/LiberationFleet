import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UserActivityItem } from '../../models/activity.model';
import { DiscussionEncryptedPayload } from '../../models/crew-discussion.model';
import { EncryptedContentEnvelope, EncryptedContentType, GiftLogEncryptedPayload } from '../../models/crypto.model';
import {
  ProposalCommentEncryptedPayload,
  ProposalEncryptedPayload
} from '../../models/proposal.model';
import { CryptoApiService } from './crypto-api.service';
import { CryptoService } from './crypto.service';
import { CryptoSessionService } from './crypto-session.service';
import { ProposalCryptoService } from './proposal-crypto.service';

const PREVIEW_MAX_LENGTH = 140;

type ActivityCryptoScope = { crewId: number } | { fleetId: number };

@Injectable({
  providedIn: 'root'
})
export class ActivityCryptoService {
  constructor(
    private cryptoSession: CryptoSessionService,
    private cryptoService: CryptoService,
    private cryptoApi: CryptoApiService,
    private proposalCrypto: ProposalCryptoService
  ) {}

  async enrichItems(items: UserActivityItem[]): Promise<UserActivityItem[]> {
    if (!items.length) {
      return items;
    }

    const enriched = items.map(item => ({
      ...item,
      previewText: this.truncate(item.plaintextPreview),
      thumbnailUrl: null as string | null
    }));

    if (!this.cryptoSession.isUnlocked()) {
      return enriched.map(item => this.maskLockedItem(item));
    }

    const previewGroups = new Map<string, UserActivityItem[]>();
    for (const item of enriched) {
      if (!item.previewContentType) {
        continue;
      }

      const scope = this.resolveScope(item);
      if (!scope) {
        continue;
      }

      const groupKey = `${this.scopeGroupKey(scope)}:${item.previewContentType}`;
      const group = previewGroups.get(groupKey) ?? [];
      group.push(item);
      previewGroups.set(groupKey, group);
    }

    for (const [groupKey, groupItems] of previewGroups) {
      const separator = groupKey.lastIndexOf(':');
      const scopeKey = groupKey.slice(0, separator);
      const contentType = groupKey.slice(separator + 1) as EncryptedContentType;
      const scope = this.parseScopeGroupKey(scopeKey);
      if (!scope) {
        continue;
      }

      const envelopes = await this.fetchEnvelopes(
        contentType,
        groupItems.map(item => item.resourceId.toString()),
        scope
      );
      const envelopeByResourceId = new Map(envelopes.map(envelope => [envelope.resourceId, envelope]));
      try {
        await this.warmScopeKeys(scope);
      } catch {
        for (const item of groupItems) {
          item.previewText = item.previewText ?? '[Unable to decrypt]';
        }
        continue;
      }

      for (const item of groupItems) {
        const envelope = envelopeByResourceId.get(item.resourceId.toString());
        if (!envelope) {
          continue;
        }

        try {
          const extracted = await this.extractPreview(contentType, scope, envelope);
          if (extracted.text) {
            item.previewText = this.truncate(extracted.text);
          }
          if (extracted.thumbnailResourceId) {
            item.thumbnailResourceId = extracted.thumbnailResourceId;
          }
        } catch {
          item.previewText = item.previewText ?? '[Unable to decrypt]';
        }
      }
    }

    const thumbnailTargets = enriched.filter(item => item.thumbnailResourceId);
    const thumbnailByScope = new Map<string, UserActivityItem[]>();
    for (const item of thumbnailTargets) {
      const scope = this.resolveScope(item);
      if (!scope) {
        continue;
      }
      const groupKey = this.scopeGroupKey(scope);
      const group = thumbnailByScope.get(groupKey) ?? [];
      group.push(item);
      thumbnailByScope.set(groupKey, group);
    }

    for (const [scopeKey, scopeItems] of thumbnailByScope) {
      const scope = this.parseScopeGroupKey(scopeKey);
      if (!scope) {
        continue;
      }

      try {
        await this.warmScopeKeys(scope);
      } catch {
        continue;
      }
      const resourceIds = [...new Set(
        scopeItems
          .map(item => item.thumbnailResourceId)
          .filter((id): id is string => !!id)
      )];
      try {
        const resolved = await this.proposalCrypto.decryptAttachments(
          scope,
          resourceIds.map(resourceId => ({
            resourceId,
            type: 'image' as const
          }))
        );
        const urlById = new Map(
          resolved
            .filter(attachment => !!attachment.dataUrl)
            .map(attachment => [attachment.resourceId, attachment.dataUrl!] as const)
        );
        for (const item of scopeItems) {
          if (!item.thumbnailResourceId) {
            continue;
          }
          item.thumbnailUrl = urlById.get(item.thumbnailResourceId) ?? null;
        }
      } catch {
        // Thumbnail preview is best-effort.
      }
    }

    return enriched;
  }

  private maskLockedItem(item: UserActivityItem): UserActivityItem {
    if (!item.previewContentType && !item.thumbnailResourceId) {
      return item;
    }

    return {
      ...item,
      previewText: item.previewText ?? '[Unlock encryption to view]',
      thumbnailUrl: null
    };
  }

  private resolveScope(item: UserActivityItem): ActivityCryptoScope | null {
    if (item.fleetId != null && item.fleetId > 0) {
      return { fleetId: item.fleetId };
    }
    if (item.crewId > 0) {
      return { crewId: item.crewId };
    }
    return null;
  }

  private scopeGroupKey(scope: ActivityCryptoScope): string {
    return 'fleetId' in scope ? `fleet:${scope.fleetId}` : `crew:${scope.crewId}`;
  }

  private parseScopeGroupKey(scopeKey: string): ActivityCryptoScope | null {
    const [kind, idPart] = scopeKey.split(':');
    const id = Number(idPart);
    if (!Number.isFinite(id) || id <= 0) {
      return null;
    }
    if (kind === 'fleet') {
      return { fleetId: id };
    }
    if (kind === 'crew') {
      return { crewId: id };
    }
    return null;
  }

  private async warmScopeKeys(scope: ActivityCryptoScope): Promise<void> {
    if ('fleetId' in scope) {
      await this.cryptoSession.warmFleetKeys(scope.fleetId);
      return;
    }
    await this.cryptoSession.warmCrewKeys(scope.crewId);
  }

  private async fetchEnvelopes(
    contentType: EncryptedContentType,
    resourceIds: string[],
    scope: ActivityCryptoScope
  ): Promise<EncryptedContentEnvelope[]> {
    if (!resourceIds.length) {
      return [];
    }

    if ('fleetId' in scope) {
      return firstValueFrom(this.cryptoApi.getEncryptedContents(contentType, resourceIds, null, scope.fleetId));
    }

    return firstValueFrom(this.cryptoApi.getEncryptedContents(contentType, resourceIds, scope.crewId));
  }

  private async extractPreview(
    contentType: EncryptedContentType,
    scope: ActivityCryptoScope,
    envelope: EncryptedContentEnvelope
  ): Promise<{ text?: string; thumbnailResourceId?: string | null }> {
    switch (contentType) {
      case 'ChatRoomMessage':
      case 'ForumComment':
      case 'ProposalComment':
      case 'LibraryRequestMessage': {
        const payload = await this.decryptJsonWithScopeFallback<ProposalCommentEncryptedPayload>(
          scope,
          envelope
        );
        return {
          text: payload.body,
          thumbnailResourceId: payload.attachments?.find(attachment => attachment.type === 'image')?.resourceId
            ?? payload.attachments?.find(attachment => attachment.type === 'video')?.posterResourceId
            ?? null
        };
      }
      case 'ForumPost':
      case 'Proposal':
      case 'LibraryItem': {
        const payload = await this.decryptJsonWithScopeFallback<DiscussionEncryptedPayload | ProposalEncryptedPayload>(
          scope,
          envelope
        );
        const text = payload.description?.trim() || payload.title?.trim();
        return {
          text,
          thumbnailResourceId: payload.thumbnailResourceId
            ?? payload.attachments?.find(attachment => attachment.type === 'image')?.resourceId
            ?? payload.attachments?.find(attachment => attachment.type === 'video')?.posterResourceId
            ?? null
        };
      }
      case 'GiftLogEntry': {
        const payload = await this.decryptJsonWithScopeFallback<GiftLogEncryptedPayload>(scope, envelope);
        return { text: payload.message };
      }
      case 'LibraryMaintenanceRecord': {
        const payload = await this.decryptJsonWithScopeFallback<{ note: string }>(scope, envelope);
        return { text: payload.note };
      }
      default:
        return {};
    }
  }

  private decryptJsonWithScopeFallback<T>(
    scope: ActivityCryptoScope,
    envelope: EncryptedContentEnvelope
  ): Promise<T> {
    const decrypt = (key: CryptoKey) =>
      this.cryptoService.decryptJson<T>(key, envelope.nonce, envelope.ciphertext);

    if ('fleetId' in scope) {
      return this.cryptoSession.decryptWithFleetKeyFallback(scope.fleetId, envelope.keyVersion, decrypt);
    }

    return this.cryptoSession.decryptWithCrewKeyFallback(scope.crewId, envelope.keyVersion, decrypt);
  }

  private truncate(value?: string | null): string | null {
    if (!value?.trim()) {
      return null;
    }

    const trimmed = value.trim().replace(/\s+/g, ' ');
    if (trimmed.length <= PREVIEW_MAX_LENGTH) {
      return trimmed;
    }

    return `${trimmed.slice(0, Math.max(0, PREVIEW_MAX_LENGTH - 3)).trimEnd()}...`;
  }
}
