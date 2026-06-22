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
import { CryptoApiService } from './crypto-api.service';
import { CryptoService } from './crypto.service';
import { CryptoSessionService } from './crypto-session.service';
import { bytesToBase64 } from './crypto-encoding.util';

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
    if (!this.cryptoSession.isUnlocked()) {
      return items.map(item => ({
        ...item,
        title: '[Encrypted]',
        descriptionPreview: '[Unlock encryption to view]',
        authorUsername: item.authorUsername || '[Encrypted]'
      }));
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return Promise.all(items.map(async item => this.decryptListItem(item, crewKey)));
  }

  async decryptDetail(proposal: ProposalDetail, crewId: number): Promise<ProposalDetail> {
    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...proposal,
        title: '[Encrypted]',
        description: '[Unlock encryption to view]',
        authorUsername: proposal.authorUsername || '[Encrypted]',
        comments: proposal.comments.map(c => ({
          ...c,
          body: '[Encrypted]',
          authorUsername: c.authorUsername || '[Encrypted]'
        }))
      };
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const decrypted = await this.decryptListItem(proposal, crewKey);
    const payload = await this.decryptProposalPayload(proposal, crewKey);

    const comments = await Promise.all(
      proposal.comments.map(comment => this.decryptComment(comment, crewKey, crewId))
    );

    const attachments = payload?.attachments ?? [];
    const resolvedAttachments = await this.decryptAttachments(crewId, attachments);

    return {
      ...proposal,
      ...decrypted,
      description: payload?.description ?? '',
      attachments,
      resolvedAttachments,
      comments
    };
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
    attachments: PendingAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string }> {
    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const storedAttachments = await this.uploadAttachments(crewId, attachments);
    return this.cryptoService.encryptJson(crewKey, {
      ...payload,
      attachments: storedAttachments
    });
  }

  private async decryptListItem(item: ProposalListItem, crewKey: CryptoKey): Promise<ProposalListItem> {
    if (!item.hasEncryptedContent || !item.encryptedPayload) {
      return item;
    }

    try {
      const payload = await this.cryptoService.decryptJson<ProposalEncryptedPayload>(
        crewKey,
        item.encryptedPayload.nonce,
        item.encryptedPayload.ciphertext
      );
      const thumbnailUrl = await this.resolveThumbnail(crewKey, payload);
      return {
        ...item,
        title: payload.title,
        descriptionPreview: payload.description.slice(0, 100),
        authorUsername: payload.authorDisplayName ?? item.authorUsername,
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
    crewId: number
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
      return {
        ...comment,
        body: payload.body,
        authorUsername: payload.authorDisplayName ?? comment.authorUsername,
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
    return Promise.all(
      attachments.map(attachment => this.decryptAttachment(crewKey, attachment))
    );
  }

  private async decryptAttachment(
    crewKey: CryptoKey,
    attachment: ProposalAttachment
  ): Promise<ResolvedAttachment> {
    const contentType = attachment.type === 'image'
      ? 'ImageAsset'
      : attachment.type === 'video'
        ? 'VideoAsset'
        : 'AudioAsset';

    try {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents(contentType, [attachment.resourceId])
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
    payload: ProposalEncryptedPayload
  ): Promise<string | null> {
    const thumbId = payload.thumbnailResourceId
      ?? payload.attachments?.find(a => a.type === 'image')?.resourceId;
    if (!thumbId) {
      return null;
    }

    const envelopes = await firstValueFrom(
      this.cryptoApi.getEncryptedContents('ImageAsset', [thumbId])
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
      let dataUrl = attachment.previewUrl ?? '';
      if (attachment.file) {
        dataUrl = await this.readFileAsDataUrl(attachment.file);
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
        fileName: attachment.file?.name,
        mimeType: attachment.file?.type
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
