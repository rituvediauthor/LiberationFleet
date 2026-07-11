import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ProposalComment,
  ProposalCommentEncryptedPayload,
  ProposalDetail,
  ProposalEncryptedPayload,
  ProposalListItem,
  ProposalAttachment,
  PendingAttachment,
  ResolvedAttachment
} from '../../models/proposal.model';
import { EncryptedContentType } from '../../models/crypto.model';
import { CryptoApiService } from './crypto-api.service';
import { CryptoService } from './crypto.service';
import { CryptoSessionService } from './crypto-session.service';
import { bytesToBase64 } from './crypto-encoding.util';
import { compressMediaFile } from '../../utils/media-compression.util';

@Injectable({
  providedIn: 'root'
})
export class ProposalCryptoService {
  constructor(
    private cryptoService: CryptoService,
    private cryptoApi: CryptoApiService,
    private cryptoSession: CryptoSessionService
  ) {}

  async decryptListItems(items: ProposalListItem[], crewId: number): Promise<ProposalListItem[]> {
    const mapPlaintext = (item: ProposalListItem): ProposalListItem => ({
      ...item,
      title: item.title ?? 'Editing crew settings',
      descriptionPreview: item.descriptionPreview ?? '',
      authorUsername: this.isAnonymousAuthor(item) ? 'Anonymous' : (item.authorUsername ?? 'Unknown')
    });

    if (!this.cryptoSession.isUnlocked()) {
      return items.map(item => {
        if (item.hasPlaintextContent) {
          return mapPlaintext(item);
        }

        return {
          ...item,
          title: '[Encrypted]',
          descriptionPreview: '[Unlock encryption to view]',
          authorUsername: this.isAnonymousAuthor(item) ? 'Anonymous' : (item.authorUsername || '[Encrypted]')
        };
      });
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return Promise.all(items.map(async item => this.decryptListItem(item, crewKey, crewId)));
  }

  async decryptDetail(proposal: ProposalDetail, crewId: number): Promise<ProposalDetail> {
    const usesAnonymousComments = proposal.usesAnonymousComments ?? false;
    const comments = await this.decryptCommentsForDetail(proposal.comments, crewId, usesAnonymousComments);

    if (proposal.hasPlaintextContent) {
      return {
        ...proposal,
        title: proposal.title ?? 'Editing crew settings',
        description: proposal.description ?? proposal.descriptionPreview ?? '',
        authorUsername: this.isAnonymousAuthor(proposal) ? 'Anonymous' : (proposal.authorUsername ?? 'Anonymous'),
        comments
      };
    }

    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...proposal,
        title: '[Encrypted]',
        description: '[Unlock encryption to view]',
        authorUsername: proposal.authorUsername || '[Encrypted]',
        comments
      };
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const decrypted = await this.decryptListItem(proposal, crewKey, crewId);
    let payload: ProposalEncryptedPayload | null = null;
    try {
      payload = await this.decryptProposalPayload(proposal, crewKey);
    } catch {
      payload = null;
    }

    const attachments = payload?.attachments ?? [];
    const resolvedAttachments = await this.decryptAttachments(crewId, attachments);
    const unableToDecrypt = proposal.hasEncryptedContent && !payload;

    return {
      ...proposal,
      ...decrypted,
      title: unableToDecrypt ? '[Unable to decrypt]' : decrypted.title,
      description: payload?.description ?? (unableToDecrypt ? '[Unable to decrypt]' : ''),
      attachments,
      resolvedAttachments,
      comments
    };
  }

  private async decryptCommentsForDetail(
    comments: ProposalComment[],
    crewId: number,
    usesAnonymousComments = false
  ): Promise<ProposalComment[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return comments.map(c => ({
        ...c,
        body: c.hasEncryptedContent ? '[Encrypted]' : c.body,
        authorUsername: c.authorUsername || '[Encrypted]'
      }));
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return Promise.all(comments.map(comment => this.decryptComment(comment, crewKey, crewId, usesAnonymousComments)));
  }

  async encryptProposalPayload(
    crewId: number,
    payload: ProposalEncryptedPayload,
    newAttachments: PendingAttachment[] = [],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string }> {
    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const uploadedAttachments = await this.uploadAttachments(crewId, newAttachments);
    const allAttachments = [...existingAttachments, ...uploadedAttachments];
    const fullPayload: ProposalEncryptedPayload = {
      ...payload,
      attachments: allAttachments,
      thumbnailResourceId: allAttachments.find(a => a.type === 'image')?.resourceId ?? null
    };
    return this.cryptoService.encryptJson(crewKey, fullPayload);
  }

  async encryptCommentPayload(
    crewId: number,
    payload: ProposalCommentEncryptedPayload,
    newAttachments: PendingAttachment[] = [],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string }> {
    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const storedAttachments = await this.uploadAttachments(crewId, newAttachments);
    return this.cryptoService.encryptJson(crewKey, {
      ...payload,
      attachments: [...existingAttachments, ...storedAttachments]
    });
  }

  private isAnonymousAuthor(item: Pick<ProposalListItem, 'authorUserId'>): boolean {
    return !item.authorUserId;
  }

  private resolveAuthorUsername(
    item: Pick<ProposalListItem, 'authorUserId' | 'authorUsername'>,
    payloadDisplayName?: string
  ): string {
    if (this.isAnonymousAuthor(item)) {
      return 'Anonymous';
    }

    return payloadDisplayName ?? item.authorUsername ?? 'Unknown';
  }

  private async decryptListItem(
    item: ProposalListItem,
    crewKey: CryptoKey,
    crewId: number
  ): Promise<ProposalListItem> {
    if (item.hasPlaintextContent) {
      return {
        ...item,
        title: item.title ?? 'Editing crew settings',
        descriptionPreview: item.descriptionPreview ?? '',
        authorUsername: this.isAnonymousAuthor(item) ? 'Anonymous' : (item.authorUsername ?? 'Unknown')
      };
    }

    if (!item.hasEncryptedContent || !item.encryptedPayload) {
      return item;
    }

    try {
      const payload = await this.cryptoService.decryptJson<ProposalEncryptedPayload>(
        crewKey,
        item.encryptedPayload.nonce,
        item.encryptedPayload.ciphertext
      );
      const thumbnailUrl = await this.resolveThumbnail(crewKey, payload, crewId);
      return {
        ...item,
        title: payload.title,
        descriptionPreview: payload.description.slice(0, 100),
        authorUsername: this.resolveAuthorUsername(item, payload.authorDisplayName),
        thumbnailUrl
      };
    } catch {
      return {
        ...item,
        title: '[Unable to decrypt]',
        descriptionPreview: '[Unable to decrypt]'
      };
    }
  }

  private async decryptProposalPayload(
    item: ProposalListItem,
    crewKey: CryptoKey
  ): Promise<ProposalEncryptedPayload | null> {
    if (!item.encryptedPayload) {
      return null;
    }
    return this.cryptoService.decryptJson<ProposalEncryptedPayload>(
      crewKey,
      item.encryptedPayload.nonce,
      item.encryptedPayload.ciphertext
    );
  }

  async decryptComments(comments: ProposalComment[], crewId: number): Promise<ProposalComment[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return comments.map(c => ({
        ...c,
        body: '[Encrypted]',
        authorUsername: c.authorUsername || '[Encrypted]'
      }));
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return Promise.all(comments.map(comment => this.decryptComment(comment, crewKey, crewId)));
  }

  private async decryptComment(
    comment: ProposalComment,
    crewKey: CryptoKey,
    crewId: number,
    usesAnonymousComments = false
  ): Promise<ProposalComment> {
    if (!comment.hasEncryptedContent || !comment.encryptedPayload) {
      return comment;
    }

    try {
      const payload = await this.cryptoService.decryptJson<ProposalCommentEncryptedPayload>(
        crewKey,
        comment.encryptedPayload.nonce,
        comment.encryptedPayload.ciphertext
      );
      const attachments = payload.attachments ?? [];
      const resolvedAttachments = await this.decryptAttachments(crewId, attachments);
      const serverUsername = comment.authorUsername;
      let authorUsername = serverUsername || 'Anonymous';
      if (!usesAnonymousComments && (!authorUsername || authorUsername === 'Anonymous')) {
        authorUsername = payload.authorDisplayName ?? authorUsername;
      }

      return {
        ...comment,
        body: payload.body,
        authorUsername,
        attachments,
        resolvedAttachments
      };
    } catch {
      return { ...comment, body: '[Unable to decrypt]' };
    }
  }

  async decryptAttachments(
    crewId: number,
    attachments: ProposalAttachment[]
  ): Promise<ResolvedAttachment[]> {
    if (!attachments.length) {
      return [];
    }

    if (!this.cryptoSession.isUnlocked()) {
      return attachments.map(attachment => ({ ...attachment }));
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const grouped = new Map<string, ProposalAttachment[]>();
    for (const attachment of attachments) {
      const contentType = attachment.type === 'image'
        ? 'ImageAsset'
        : attachment.type === 'video'
          ? 'VideoAsset'
          : 'AudioAsset';
      const bucket = grouped.get(contentType) ?? [];
      bucket.push(attachment);
      grouped.set(contentType, bucket);
    }

    const dataUrlByResourceId = new Map<string, string>();
    for (const [contentType, bucket] of grouped.entries()) {
      const resourceIds = bucket.map(attachment => attachment.resourceId);
      try {
        const envelopes = await firstValueFrom(
          this.cryptoApi.getEncryptedContents(contentType as EncryptedContentType, resourceIds, crewId)
        );
        for (const envelope of envelopes) {
          try {
            const blobPayload = await this.cryptoService.decryptJson<{ dataUrl: string }>(
              crewKey,
              envelope.nonce,
              envelope.ciphertext
            );
            dataUrlByResourceId.set(envelope.resourceId, blobPayload.dataUrl);
          } catch {
            // Skip unreadable attachments.
          }
        }
      } catch {
        // Skip this content type batch.
      }
    }

    return attachments.map(attachment => ({
      ...attachment,
      dataUrl: dataUrlByResourceId.get(attachment.resourceId)
    }));
  }

  private async decryptAttachment(
    crewKey: CryptoKey,
    attachment: ProposalAttachment,
    crewId: number
  ): Promise<ResolvedAttachment> {
    const contentType = attachment.type === 'image'
      ? 'ImageAsset'
      : attachment.type === 'video'
        ? 'VideoAsset'
        : 'AudioAsset';

    try {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents(contentType, [attachment.resourceId], crewId)
      );
      const envelope = envelopes[0];
      if (!envelope) {
        return { ...attachment };
      }

      const blobPayload = await this.cryptoService.decryptJson<{ dataUrl: string }>(
        crewKey,
        envelope.nonce,
        envelope.ciphertext
      );
      return { ...attachment, dataUrl: blobPayload.dataUrl };
    } catch {
      return { ...attachment };
    }
  }

  private async resolveThumbnail(
    crewKey: CryptoKey,
    payload: ProposalEncryptedPayload,
    crewId: number
  ): Promise<string | null> {
    const thumbId = payload.thumbnailResourceId
      ?? payload.attachments?.find(a => a.type === 'image')?.resourceId;
    if (!thumbId) {
      return null;
    }

    const envelopes = await firstValueFrom(
      this.cryptoApi.getEncryptedContents('ImageAsset', [thumbId], crewId)
    );
    const envelope = envelopes[0];
    if (!envelope) {
      return null;
    }

    try {
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

  private async uploadAttachments(crewId: number, attachments: PendingAttachment[]): Promise<ProposalAttachment[]> {
    if (!attachments.length) {
      return [];
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const results: ProposalAttachment[] = [];

    for (const attachment of attachments) {
      let file = attachment.file;
      if (file && (attachment.type === 'image' || attachment.type === 'video')) {
        file = await compressMediaFile(file, attachment.type);
      }

      let dataUrl = attachment.previewUrl ?? '';
      if (file) {
        dataUrl = await this.readFileAsDataUrl(file);
      } else if (attachment.blob) {
        dataUrl = await this.readBlobAsDataUrl(attachment.blob);
      }

      const encrypted = await this.cryptoService.encryptJson(crewKey, { dataUrl });
      const contentType = attachment.type === 'image'
        ? 'ImageAsset'
        : attachment.type === 'video'
          ? 'VideoAsset'
          : 'AudioAsset';

      await firstValueFrom(this.cryptoApi.upsertEncryptedContent({
        contentType,
        resourceId: attachment.resourceId,
        crewId,
        keyVersion: 1,
        nonce: encrypted.nonce,
        ciphertext: encrypted.ciphertext
      }));

      results.push({
        resourceId: attachment.resourceId,
        type: attachment.type,
        fileName: file?.name ?? attachment.file?.name,
        mimeType: file?.type ?? attachment.file?.type
      });
    }

    return results;
  }

  private readFileAsDataUrl(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(file);
    });
  }

  private readBlobAsDataUrl(blob: Blob): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(blob);
    });
  }

  createResourceId(): string {
    const bytes = crypto.getRandomValues(new Uint8Array(16));
    return bytesToBase64(bytes).replace(/[/+=]/g, '').slice(0, 22);
  }
}
