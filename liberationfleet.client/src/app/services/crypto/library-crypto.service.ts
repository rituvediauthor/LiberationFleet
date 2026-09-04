import { Injectable } from '@angular/core';
import { PendingAttachment, ProposalAttachment, ProposalCommentEncryptedPayload, ProposalEncryptedPayload, ResolvedAttachment } from '../../models/proposal.model';
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
import { firstValueFrom } from 'rxjs';

export interface LibraryOfferingEncryptInput {
  title: string;
  description: string;
  authorDisplayName: string;
}

export interface LibraryTaskEncryptedPayload {
  title: string;
  details: string;
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
    attachments: PendingAttachment[],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string; thumbnailResourceId: string | null; descriptionPreview: string }> {
    const detailAttachments = attachments
      .filter(a => a.role === 'detail' || (!a.role && a.type === 'image'))
      .slice(0, 5);
    const downloadAttachments = attachments
      .filter(a => a.role === 'download' || (!a.role && a.type !== 'image'))
      .slice(0, 5);
    const limitedAttachments = [...detailAttachments, ...downloadAttachments];
    const encrypted = await this.proposalCrypto.encryptProposalPayload(
      crewId,
      {
        title: payload.title.trim(),
        description: payload.description.trim(),
        authorDisplayName: payload.authorDisplayName
      },
      limitedAttachments,
      existingAttachments
    );

    // Prefer the encrypt-time list thumb (first image or video poster), not images-only.
    const thumbnailResourceId = encrypted.thumbnailResourceId
      ?? detailAttachments.find(a => a.type === 'image')?.resourceId
      ?? existingAttachments.find(a => a.type === 'image')?.resourceId
      ?? limitedAttachments.find(a => a.type === 'image')?.resourceId
      ?? null;
    const descriptionPreview = payload.description.trim().slice(0, 200);

    return {
      ...encrypted,
      thumbnailResourceId,
      descriptionPreview
    };
  }

  async loadOfferingPayload(offeringId: number, crewId: number): Promise<ProposalEncryptedPayload | null> {
    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return this.decryptOfferingPayload(offeringId, crewId, crewKey);
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

  async encryptTaskPayload(
    crewId: number,
    payload: LibraryTaskEncryptedPayload
  ): Promise<{ nonce: string; ciphertext: string; keyVersion: number }> {
    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const encrypted = await this.cryptoService.encryptJson<LibraryTaskEncryptedPayload>(crewKey, {
      title: payload.title.trim(),
      details: (payload.details ?? '').trim()
    });
    return {
      ...encrypted,
      keyVersion: this.cryptoSession.getCrewKeyVersion(crewId) ?? 1
    };
  }

  async loadTaskPayload(taskId: number, crewId: number): Promise<LibraryTaskEncryptedPayload | null> {
    if (!this.cryptoSession.isUnlocked()) {
      return null;
    }

    try {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents('LibraryTask', [taskId.toString()], crewId)
      );
      const envelope = envelopes[0];
      if (!envelope) {
        return null;
      }

      return await this.cryptoSession.decryptWithCrewKeyFallback(
        crewId,
        envelope.keyVersion,
        key => this.cryptoService.decryptJson<LibraryTaskEncryptedPayload>(
          key,
          envelope.nonce,
          envelope.ciphertext
        )
      );
    } catch {
      return null;
    }
  }

  async enrichTaskListItems<T extends { taskId: number; title: string; hasEncryptedContent?: boolean }>(
    items: T[],
    crewId: number
  ): Promise<T[]> {
    if (!this.cryptoSession.isUnlocked() || items.length === 0) {
      return items.map(item => ({
        ...item,
        title: item.hasEncryptedContent && !item.title ? 'Encrypted quest' : item.title
      }));
    }

    return Promise.all(items.map(async item => {
      if (!item.hasEncryptedContent) {
        return item;
      }

      const payload = await this.loadTaskPayload(item.taskId, crewId);
      return {
        ...item,
        title: payload?.title?.trim() || 'Encrypted quest'
      };
    }));
  }

  async enrichTaskDetail<T extends { taskId: number; title: string; details: string; hasEncryptedContent?: boolean }>(
    task: T,
    crewId: number
  ): Promise<T> {
    if (!task.hasEncryptedContent) {
      return task;
    }

    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...task,
        title: 'Encrypted quest',
        details: 'Unlock encryption to view quest details.'
      };
    }

    const payload = await this.loadTaskPayload(task.taskId, crewId);
    if (!payload) {
      return {
        ...task,
        title: 'Encrypted quest',
        details: '[Unable to decrypt]'
      };
    }

    return {
      ...task,
      title: payload.title?.trim() || 'Encrypted quest',
      details: payload.details ?? ''
    };
  }

  async enrichUnitListItems(items: LibraryUnitListItem[], crewId: number): Promise<LibraryUnitListItem[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return items;
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const thumbnailIds = new Set(
      items
        .map(item => item.thumbnailResourceId)
        .filter((id): id is string => !!id)
    );

    // Recover list thumbs from ciphertext when the API column was never set (video posters, etc.).
    const recoveredIds = new Map<number, string>();
    await Promise.all(items.map(async item => {
      if (item.thumbnailResourceId || !item.hasEncryptedContent) {
        return;
      }
      const payload = await this.decryptOfferingPayload(item.offeringId, crewId, crewKey);
      const recovered = payload?.thumbnailResourceId
        ?? payload?.attachments?.find(attachment => attachment.type === 'image')?.resourceId
        ?? payload?.attachments?.find(attachment => attachment.type === 'video')?.posterResourceId
        ?? null;
      if (recovered) {
        recoveredIds.set(item.unitId, recovered);
        thumbnailIds.add(recovered);
      }
    }));

    const thumbnailMap = await this.resolveThumbnailsBatch([...thumbnailIds], crewId);

    return items.map(item => {
      const thumbId = item.thumbnailResourceId ?? recoveredIds.get(item.unitId) ?? null;
      return {
        ...item,
        thumbnailResourceId: thumbId ?? item.thumbnailResourceId,
        thumbnailUrl: (thumbId ? thumbnailMap.get(thumbId) : null)
          ?? item.thumbnailUrl
          ?? null
      };
    });
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

    const attachments = payload.attachments ?? [];
    const detailAttachments = attachments.filter(attachment =>
      attachment.role === 'detail' || (!attachment.role && attachment.type === 'image')
    );
    const downloadAttachments = attachments.filter(attachment =>
      attachment.role === 'download' || (!attachment.role && attachment.type !== 'image')
    );
    const resolvedImages = detailAttachments.length > 0
      ? await this.proposalCrypto.decryptAttachments(crewId, detailAttachments)
      : [];
    const imageUrls = resolvedImages
      .map(attachment => attachment.dataUrl)
      .filter((url): url is string => !!url);

    // Download files: metadata only until the user acquires and downloads.
    const downloadableFiles: ResolvedAttachment[] = downloadAttachments.map(attachment => ({
      resourceId: attachment.resourceId,
      type: attachment.type,
      fileName: attachment.fileName,
      mimeType: attachment.mimeType,
      role: attachment.role ?? 'download',
      encrypted: attachment.encrypted
    }));

    let thumbnailUrl = detail.thumbnailUrl ?? null;
    const thumbId = payload.thumbnailResourceId
      ?? detailAttachments.find(attachment => attachment.type === 'image')?.resourceId
      ?? attachments.find(attachment => attachment.type === 'video')?.posterResourceId
      ?? detail.thumbnailResourceId
      ?? null;
    if (thumbId) {
      const thumbFromPayload = resolvedImages.find(attachment => attachment.resourceId === thumbId)?.dataUrl;
      if (thumbFromPayload) {
        thumbnailUrl = thumbFromPayload;
      } else {
        thumbnailUrl = await this.resolveThumbnail(thumbId, crewId, crewKey) ?? thumbnailUrl;
      }
    }

    return {
      ...detail,
      thumbnailUrl,
      fullDescription: payload.description ?? detail.descriptionPreview ?? null,
      imageUrls: imageUrls.length > 0 ? imageUrls : (thumbnailUrl ? [thumbnailUrl] : []),
      downloadableFiles
    };
  }

  /** Decrypt downloadable digital files after acquisition (lazy). */
  async decryptDownloadFiles(crewId: number, files: ResolvedAttachment[]): Promise<ResolvedAttachment[]> {
    if (!files.length || !this.cryptoSession.isUnlocked()) {
      return files;
    }
    return this.proposalCrypto.decryptAttachments(crewId, files);
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
    const thumbnailMap = await this.resolveThumbnailsBatch(thumbnailIds, crewId);
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
      fulfillmentMode: detail.fulfillmentMode,
      visibility: detail.visibility
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

    await this.cryptoSession.warmCrewKeys(crewId);
    return Promise.all(messages.map(async message => {
      if (!message.hasEncryptedContent || !message.encryptedPayload) {
        return message;
      }

      try {
        const payload = await this.cryptoSession.decryptWithCrewKeyFallback(
          crewId,
          message.encryptedPayload.keyVersion,
          key => this.cryptoService.decryptJson<ProposalCommentEncryptedPayload>(
            key,
            message.encryptedPayload!.nonce,
            message.encryptedPayload!.ciphertext
          )
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
    _crewKey: CryptoKey
  ): Promise<ProposalEncryptedPayload | null> {
    try {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents('LibraryItem', [offeringId.toString()], crewId)
      );
      const envelope = envelopes[0];
      if (!envelope) {
        return null;
      }

      return await this.cryptoSession.decryptWithCrewKeyFallback(
        crewId,
        envelope.keyVersion,
        key => this.cryptoService.decryptJson<ProposalEncryptedPayload>(
          key,
          envelope.nonce,
          envelope.ciphertext
        )
      );
    } catch {
      return null;
    }
  }

  private async resolveThumbnailsBatch(
    resourceIds: string[],
    crewId: number
  ): Promise<Map<string, string>> {
    const results = new Map<string, string>();
    if (resourceIds.length === 0) {
      return results;
    }

    try {
      const resolved = await this.proposalCrypto.decryptAttachments(
        crewId,
        resourceIds.map(resourceId => ({
          resourceId,
          type: 'image' as const
        }))
      );
      for (const attachment of resolved) {
        if (attachment.dataUrl) {
          results.set(attachment.resourceId, attachment.dataUrl);
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
    _crewKey: CryptoKey
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
          const payload = await this.cryptoSession.decryptWithCrewKeyFallback(
            crewId,
            envelope.keyVersion,
            key => this.cryptoService.decryptJson<LibraryRequestEncryptedPayload>(
              key,
              envelope.nonce,
              envelope.ciphertext
            )
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
    _crewKey: CryptoKey
  ): Promise<string | null> {
    if (!thumbnailResourceId) {
      return null;
    }

    const map = await this.resolveThumbnailsBatch([thumbnailResourceId], crewId);
    return map.get(thumbnailResourceId) ?? null;
  }
}
