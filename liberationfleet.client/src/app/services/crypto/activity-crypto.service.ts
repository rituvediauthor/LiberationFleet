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

const PREVIEW_MAX_LENGTH = 140;

@Injectable({
  providedIn: 'root'
})
export class ActivityCryptoService {
  constructor(
    private cryptoSession: CryptoSessionService,
    private cryptoService: CryptoService,
    private cryptoApi: CryptoApiService
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

      const groupKey = `${item.crewId}:${item.previewContentType}`;
      const group = previewGroups.get(groupKey) ?? [];
      group.push(item);
      previewGroups.set(groupKey, group);
    }

    for (const [groupKey, groupItems] of previewGroups) {
      const [crewIdPart, contentType] = groupKey.split(':');
      const crewId = Number(crewIdPart);
      const envelopes = await this.fetchEnvelopes(
        contentType as EncryptedContentType,
        groupItems.map(item => item.resourceId.toString()),
        crewId
      );
      const envelopeByResourceId = new Map(envelopes.map(envelope => [envelope.resourceId, envelope]));
      const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);

      for (const item of groupItems) {
        const envelope = envelopeByResourceId.get(item.resourceId.toString());
        if (!envelope) {
          continue;
        }

        try {
          const extracted = await this.extractPreview(
            contentType as EncryptedContentType,
            crewKey,
            envelope
          );
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
    const thumbnailByCrew = new Map<number, UserActivityItem[]>();
    for (const item of thumbnailTargets) {
      const group = thumbnailByCrew.get(item.crewId) ?? [];
      group.push(item);
      thumbnailByCrew.set(item.crewId, group);
    }

    for (const [crewId, crewItems] of thumbnailByCrew) {
      const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
      const resourceIds = [...new Set(
        crewItems
          .map(item => item.thumbnailResourceId)
          .filter((id): id is string => !!id)
      )];
      const envelopes = await this.fetchEnvelopes('ImageAsset', resourceIds, crewId);
      const envelopeByResourceId = new Map(envelopes.map(envelope => [envelope.resourceId, envelope]));

      for (const item of crewItems) {
        if (!item.thumbnailResourceId) {
          continue;
        }

        const envelope = envelopeByResourceId.get(item.thumbnailResourceId);
        if (!envelope) {
          continue;
        }

        try {
          const blobPayload = await this.cryptoService.decryptJson<{ dataUrl: string }>(
            crewKey,
            envelope.nonce,
            envelope.ciphertext
          );
          item.thumbnailUrl = blobPayload.dataUrl;
        } catch {
          // Thumbnail preview is best-effort.
        }
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

  private async fetchEnvelopes(
    contentType: EncryptedContentType,
    resourceIds: string[],
    crewId: number
  ): Promise<EncryptedContentEnvelope[]> {
    if (!resourceIds.length) {
      return [];
    }

    return firstValueFrom(this.cryptoApi.getEncryptedContents(contentType, resourceIds, crewId));
  }

  private async extractPreview(
    contentType: EncryptedContentType,
    crewKey: CryptoKey,
    envelope: EncryptedContentEnvelope
  ): Promise<{ text?: string; thumbnailResourceId?: string | null }> {
    switch (contentType) {
      case 'ChatRoomMessage':
      case 'ForumComment':
      case 'ProposalComment':
      case 'LibraryRequestMessage': {
        const payload = await this.cryptoService.decryptJson<ProposalCommentEncryptedPayload>(
          crewKey,
          envelope.nonce,
          envelope.ciphertext
        );
        return {
          text: payload.body,
          thumbnailResourceId: payload.attachments?.find(attachment => attachment.type === 'image')?.resourceId ?? null
        };
      }
      case 'ForumPost':
      case 'Proposal':
      case 'LibraryItem': {
        const payload = await this.cryptoService.decryptJson<DiscussionEncryptedPayload | ProposalEncryptedPayload>(
          crewKey,
          envelope.nonce,
          envelope.ciphertext
        );
        const text = payload.description?.trim() || payload.title?.trim();
        return {
          text,
          thumbnailResourceId: payload.thumbnailResourceId
            ?? payload.attachments?.find(attachment => attachment.type === 'image')?.resourceId
            ?? null
        };
      }
      case 'GiftLogEntry': {
        const payload = await this.cryptoService.decryptJson<GiftLogEncryptedPayload>(
          crewKey,
          envelope.nonce,
          envelope.ciphertext
        );
        return { text: payload.message };
      }
      case 'LibraryMaintenanceRecord': {
        const payload = await this.cryptoService.decryptJson<{ note: string }>(
          crewKey,
          envelope.nonce,
          envelope.ciphertext
        );
        return { text: payload.note };
      }
      default:
        return {};
    }
  }

  private truncate(value?: string | null): string | null {
    if (!value?.trim()) {
      return null;
    }

    const trimmed = value.trim().replace(/\s+/g, ' ');
    if (trimmed.length <= PREVIEW_MAX_LENGTH) {
      return trimmed;
    }

    return `${trimmed.slice(0, PREVIEW_MAX_LENGTH - 1)}…`;
  }
}
