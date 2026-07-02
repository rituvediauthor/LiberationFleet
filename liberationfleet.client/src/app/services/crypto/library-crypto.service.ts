import { Injectable } from '@angular/core';
import { PendingAttachment } from '../../models/proposal.model';
import {
  LibraryRequestDetail,
  LibraryRequestListItem,
  LibraryRequestMessage,
  LibraryUnitDetail,
  LibraryUnitListItem
} from '../../models/library.model';
import { ProposalCommentEncryptedPayload } from '../../models/proposal.model';
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
    return Promise.all(items.map(async item => {
      const withThumb = await this.resolveThumbnail(item.thumbnailResourceId, crewId, crewKey);
      return { ...item, thumbnailUrl: withThumb ?? item.thumbnailUrl ?? null };
    }));
  }

  async enrichUnitDetail(detail: LibraryUnitDetail, crewId: number): Promise<LibraryUnitDetail> {
    const [withThumb, withDescription, imageUrls] = await Promise.all([
      this.enrichUnitListItems([this.toListItem(detail)], crewId),
      this.decryptOfferingDescription(detail, crewId),
      this.decryptOfferingImages(detail, crewId)
    ]);

    return {
      ...detail,
      thumbnailUrl: withThumb[0]?.thumbnailUrl ?? null,
      fullDescription: withDescription,
      imageUrls
    };
  }

  async enrichRequestListItems(items: LibraryRequestListItem[], crewId: number): Promise<LibraryRequestListItem[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return items;
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return Promise.all(items.map(async item => {
      const thumbnailUrl = await this.resolveThumbnail(item.thumbnailResourceId, crewId, crewKey);
      let fullPurpose: string | null = null;
      if (item.hasEncryptedPurpose) {
        try {
          fullPurpose = await this.decryptRequestPurpose(item.requestId, crewId, crewKey);
        } catch {
          fullPurpose = null;
        }
      }

      return {
        ...item,
        thumbnailUrl: thumbnailUrl ?? item.thumbnailUrl ?? null,
        fullPurpose: fullPurpose ?? item.purposePreview
      };
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
        return {
          ...message,
          body: payload.body,
          authorUsername: payload.authorDisplayName ?? message.authorUsername
        };
      } catch {
        return { ...message, body: '[Unable to decrypt]' };
      }
    }));
  }

  private async decryptOfferingImages(detail: LibraryUnitDetail, crewId: number): Promise<string[]> {
    if (!detail.hasEncryptedContent || !this.cryptoSession.isUnlocked()) {
      return detail.thumbnailUrl ? [detail.thumbnailUrl] : [];
    }

    try {
      const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents('LibraryItem', [detail.offeringId.toString()], crewId)
      );
      const envelope = envelopes[0];
      if (!envelope) {
        return detail.thumbnailUrl ? [detail.thumbnailUrl] : [];
      }

      const payload = await this.cryptoService.decryptJson<ProposalEncryptedPayload>(
        crewKey,
        envelope.nonce,
        envelope.ciphertext
      );

      const imageAttachments = (payload.attachments ?? []).filter(attachment => attachment.type === 'image');
      if (imageAttachments.length === 0) {
        return detail.thumbnailUrl ? [detail.thumbnailUrl] : [];
      }

      const resolved = await this.proposalCrypto.decryptAttachments(crewId, imageAttachments);
      const urls = resolved
        .map(attachment => attachment.dataUrl)
        .filter((url): url is string => !!url);

      return urls.length > 0 ? urls : (detail.thumbnailUrl ? [detail.thumbnailUrl] : []);
    } catch {
      return detail.thumbnailUrl ? [detail.thumbnailUrl] : [];
    }
  }

  private async decryptOfferingDescription(detail: LibraryUnitDetail, crewId: number): Promise<string | null> {
    if (!detail.hasEncryptedContent || !this.cryptoSession.isUnlocked()) {
      return detail.descriptionPreview || null;
    }

    try {
      const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents('LibraryItem', [detail.offeringId.toString()], crewId)
      );
      const envelope = envelopes[0];
      if (!envelope) {
        return null;
      }

      const payload = await this.cryptoService.decryptJson<ProposalEncryptedPayload>(
        crewKey,
        envelope.nonce,
        envelope.ciphertext
      );
      return payload.description;
    } catch {
      return null;
    }
  }

  private async decryptRequestPurpose(requestId: number, crewId: number, crewKey?: CryptoKey): Promise<string> {
    const key = crewKey ?? await this.cryptoSession.ensureCrewKeyReady(crewId);
    const envelopes = await firstValueFrom(
      this.cryptoApi.getEncryptedContents('LibraryRequest', [requestId.toString()], crewId)
    );
    const envelope = envelopes[0];
    if (!envelope) {
      throw new Error('Missing encrypted purpose');
    }

    const payload = await this.cryptoService.decryptJson<LibraryRequestEncryptedPayload>(
      key,
      envelope.nonce,
      envelope.ciphertext
    );
    return payload.purpose;
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
