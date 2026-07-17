import { Injectable } from '@angular/core';
import { PendingAttachment, ProposalCommentEncryptedPayload, ResolvedAttachment } from '../../models/proposal.model';
import {
  LibraryRequestDetail,
  LibraryRequestListItem,
  LibraryRequestMessage,
  LibraryUnitDetail,
  LibraryUnitListItem
} from '../../models/library.model';
import { ProposalCryptoService } from './proposal-crypto.service';
import { CryptoSessionService } from './crypto-session.service';
import { CryptoApiService } from './crypto-api.service';
import { CryptoService } from './crypto.service';
import { ProposalEncryptedPayload } from '../../models/proposal.model';
import { firstValueFrom } from 'rxjs';

export interface LibraryOfferingEncryptInput {
  title: string;
  description: string;
  authorDisplayName: string;
}

export interface LibraryRequestEncryptedPayload {
  purpose: string;
}

@Injectable({
  providedIn: 'root'
})
export class LibraryCryptoService {
  constructor(
    private proposalCrypto: ProposalCryptoService,
    private cryptoSession: CryptoSessionService,
    private cryptoApi: CryptoApiService,
    private cryptoService: CryptoService
  ) {}

  async encryptOfferingPayload(
    crewId: number,
    payload: LibraryOfferingEncryptInput,
    attachments: PendingAttachment[]
  ): Promise<{ nonce: string; ciphertext: string; thumbnailResourceId: string | null; descriptionPreview: string }> {
    const limitedAttachments = attachments.slice(0, 5);
    const encrypted = await this.proposalCrypto.encryptProposalPayload(
      crewId,
      {
        title: payload.title.trim(),
        description: payload.description.trim(),
        authorDisplayName: payload.authorDisplayName
      },
      limitedAttachments
    );

    const thumbnailResourceId = limitedAttachments.find(a => a.type === 'image')?.resourceId ?? null;
    const descriptionPreview = payload.description.trim().slice(0, 200);

    return {
      ...encrypted,
      thumbnailResourceId,
      descriptionPreview
    };
  }

  async encryptRequestPurpose(crewId: number, purpose: string): Promise<{ nonce: string; ciphertext: string; purposePreview: string }> {
    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const encrypted = await this.cryptoService.encryptJson<LibraryRequestEncryptedPayload>(crewKey, {
      purpose: purpose.trim()
    });

    return {
      ...encrypted,
      purposePreview: purpose.trim().slice(0, 200)
    };
  }

  async encryptTextNote(crewId: number, text: string): Promise<{ nonce: string; ciphertext: string; preview: string }> {
    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const encrypted = await this.cryptoService.encryptJson<{ note: string }>(crewKey, {
      note: text.trim()
    });

    return {
      ...encrypted,
      preview: text.trim().slice(0, 200)
    };
  }

  async enrichUnitListItems(items: LibraryUnitListItem[], crewId: number): Promise<LibraryUnitListItem[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return items;
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const thumbnailIds = [...new Set(
      items
        .map(item => item.thumbnailResourceId)
        .filter((id): id is string => !!id)
    )];
    const thumbnailMap = await this.resolveThumbnailsBatch(thumbnailIds, crewId, crewKey);

    return items.map(item => ({
      ...item,
      thumbnailUrl: (item.thumbnailResourceId ? thumbnailMap.get(item.thumbnailResourceId) : null)
        ?? item.thumbnailUrl
        ?? null
    }));
  }

  async enrichUnitDetail(detail: LibraryUnitDetail, crewId: number): Promise<LibraryUnitDetail> {
    if (!detail.hasEncryptedContent || !this.cryptoSession.isUnlocked()) {
      const fallbackImages = detail.thumbnailUrl ? [detail.thumbnailUrl] : [];
      return {
        ...detail,
        fullDescription: detail.descriptionPreview || null,
        imageUrls: fallbackImages
      };
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const payload = await this.decryptOfferingPayload(detail.offeringId, crewId, crewKey);
    if (!payload) {
      return {
        ...detail,
        fullDescription: detail.descriptionPreview || null,
        imageUrls: detail.thumbnailUrl ? [detail.thumbnailUrl] : []
      };
    }

    const imageAttachments = (payload.attachments ?? []).filter(attachment => attachment.type === 'image');
    const resolvedImages = imageAttachments.length > 0
      ? await this.proposalCrypto.decryptAttachments(crewId, imageAttachments)
      : [];
    const imageUrls = resolvedImages
      .map(attachment => attachment.dataUrl)
      .filter((url): url is string => !!url);

    let thumbnailUrl = detail.thumbnailUrl ?? null;
    const thumbId = payload.thumbnailResourceId ?? imageAttachments[0]?.resourceId;
    if (thumbId) {
      const thumbFromPayload = resolvedImages.find(attachment => attachment.resourceId === thumbId)?.dataUrl;
      if (thumbFromPayload) {
        thumbnailUrl = thumbFromPayload;
      } else if (detail.thumbnailResourceId) {
        thumbnailUrl = await this.resolveThumbnail(detail.thumbnailResourceId, crewId, crewKey);
      }
    }

    return {
      ...detail,
      thumbnailUrl,
      fullDescription: payload.description ?? detail.descriptionPreview ?? null,
      imageUrls: imageUrls.length > 0 ? imageUrls : (thumbnailUrl ? [thumbnailUrl] : [])
    };
  }

  async enrichRequestListItems(items: LibraryRequestListItem[], crewId: number): Promise<LibraryRequestListItem[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return items;
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const thumbnailIds = [...new Set(
      items
        .map(item => item.thumbnailResourceId)
        .filter((id): id is string => !!id)
    )];
    const thumbnailMap = await this.resolveThumbnailsBatch(thumbnailIds, crewId, crewKey);
    const encryptedRequestIds = items
      .filter(item => item.hasEncryptedPurpose)
      .map(item => item.requestId.toString());
    const purposeMap = await this.decryptRequestPurposesBatch(encryptedRequestIds, crewId, crewKey);

    return items.map(item => ({
      ...item,
      thumbnailUrl: (item.thumbnailResourceId ? thumbnailMap.get(item.thumbnailResourceId) : null)
        ?? item.thumbnailUrl
        ?? null,
      fullPurpose: purposeMap.get(item.requestId.toString()) ?? item.purposePreview
    }));
  }

  async enrichRequestDetail(detail: LibraryRequestDetail, crewId: number): Promise<LibraryRequestDetail> {
    const enriched = await this.enrichRequestListItems([detail], crewId);
    return { ...detail, ...enriched[0] };
  }

  toListItem(detail: LibraryUnitDetail): LibraryUnitListItem {
    return {
      unitId: detail.unitId,
      offeringId: detail.offeringId,
      holderUserId: detail.holderUserId,
      holderUsername: detail.holderUsername,
      title: detail.title,
      descriptionPreview: detail.descriptionPreview,
      categories: detail.categories,
      thumbnailResourceId: detail.thumbnailResourceId,
      thumbnailUrl: detail.thumbnailUrl,
      hasEncryptedContent: detail.hasEncryptedContent,
      remainingStock: detail.remainingStock,
      quantityNotApplicable: detail.quantityNotApplicable,
      isOutOfStock: detail.isOutOfStock,
      offeringKind: detail.offeringKind,
      fulfillmentMode: detail.fulfillmentMode
    };
  }

  toRequestListItem(detail: LibraryRequestDetail): LibraryRequestListItem {
    return {
      requestId: detail.requestId,
      unitId: detail.unitId,
      offeringId: detail.offeringId,
      holderUserId: detail.holderUserId,
      holderUsername: detail.holderUsername,
      requesterUserId: detail.requesterUserId,
      requesterUsername: detail.requesterUsername,
      title: detail.title,
      descriptionPreview: detail.descriptionPreview,
      purposePreview: detail.purposePreview,
      categories: detail.categories,
      thumbnailResourceId: detail.thumbnailResourceId,
      thumbnailUrl: detail.thumbnailUrl,
      hasEncryptedContent: detail.hasEncryptedContent,
      hasEncryptedPurpose: detail.hasEncryptedPurpose,
      status: detail.status,
      quantity: detail.quantity,
      neededByStart: detail.neededByStart,
      neededByEnd: detail.neededByEnd,
      createdAt: detail.createdAt,
      fullPurpose: detail.fullPurpose
    };
  }

  async decryptRequestMessages(messages: LibraryRequestMessage[], crewId: number): Promise<LibraryRequestMessage[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return messages.map(message => ({
        ...message,
        body: '[Unlock encryption to view]'
      }));
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return Promise.all(messages.map(async message => {
      if (!message.hasEncryptedContent || !message.encryptedPayload) {
        return message;
      }

      try {
        const payload = await this.cryptoService.decryptJson<ProposalCommentEncryptedPayload>(
          crewKey,
          message.encryptedPayload.nonce,
          message.encryptedPayload.ciphertext
        );
        const resolvedAttachments: ResolvedAttachment[] = await this.proposalCrypto.decryptAttachments(
          { crewId },
          payload.attachments ?? []
        );
        return {
          ...message,
          body: payload.body,
          authorUsername: payload.authorDisplayName ?? message.authorUsername,
          resolvedAttachments
        };
      } catch {
        return { ...message, body: '[Unable to decrypt]' };
      }
    }));
  }

  private async decryptOfferingPayload(
    offeringId: number,
    crewId: number,
    crewKey: CryptoKey
  ): Promise<ProposalEncryptedPayload | null> {
    try {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents('LibraryItem', [offeringId.toString()], crewId)
      );
      const envelope = envelopes[0];
      if (!envelope) {
        return null;
      }

      return await this.cryptoService.decryptJson<ProposalEncryptedPayload>(
        crewKey,
        envelope.nonce,
        envelope.ciphertext
      );
    } catch {
      return null;
    }
  }

  private async resolveThumbnailsBatch(
    resourceIds: string[],
    crewId: number,
    crewKey: CryptoKey
  ): Promise<Map<string, string>> {
    const results = new Map<string, string>();
    if (resourceIds.length === 0) {
      return results;
    }

    try {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents('ImageAsset', resourceIds, crewId)
      );
      for (const envelope of envelopes) {
        try {
          const blobPayload = await this.cryptoService.decryptJson<{ dataUrl: string }>(
            crewKey,
            envelope.nonce,
            envelope.ciphertext
          );
          results.set(envelope.resourceId, blobPayload.dataUrl);
        } catch {
          // Skip unreadable thumbnails.
        }
      }
    } catch {
      // Fall back to empty map.
    }

    return results;
  }

  private async decryptRequestPurposesBatch(
    requestIds: string[],
    crewId: number,
    crewKey: CryptoKey
  ): Promise<Map<string, string>> {
    const results = new Map<string, string>();
    if (requestIds.length === 0) {
      return results;
    }

    try {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents('LibraryRequest', requestIds, crewId)
      );
      for (const envelope of envelopes) {
        try {
          const payload = await this.cryptoService.decryptJson<LibraryRequestEncryptedPayload>(
            crewKey,
            envelope.nonce,
            envelope.ciphertext
          );
          results.set(envelope.resourceId, payload.purpose);
        } catch {
          // Skip unreadable purposes.
        }
      }
    } catch {
      // Fall back to empty map.
    }

    return results;
  }

  private async resolveThumbnail(
    thumbnailResourceId: string | null | undefined,
    crewId: number,
    crewKey: CryptoKey
  ): Promise<string | null> {
    if (!thumbnailResourceId) {
      return null;
    }

    try {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents('ImageAsset', [thumbnailResourceId], crewId)
      );
      const envelope = envelopes[0];
      if (!envelope) {
        return null;
      }

      const blobPayload = await this.cryptoService.decryptJson<{ dataUrl: string }>(
        crewKey,
        envelope.nonce,
        envelope.ciphertext
      );
      return blobPayload.dataUrl;
    } catch {
      return null;
    }
  }
}
