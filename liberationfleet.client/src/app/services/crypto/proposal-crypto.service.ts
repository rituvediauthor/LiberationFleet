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

export interface ProposalCryptoScope {
  crewId?: number;
  fleetId?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ProposalCryptoService {
  constructor(
    private cryptoService: CryptoService,
    private cryptoApi: CryptoApiService,
    private cryptoSession: CryptoSessionService
  ) {}

  async decryptListItems(items: ProposalListItem[], scope: ProposalCryptoScope | number): Promise<ProposalListItem[]> {
    const normalizedScope = this.normalizeScope(scope);
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

    const scopeKey = await this.resolveScopeKey(normalizedScope);
    return Promise.all(items.map(async item => this.decryptListItem(item, scopeKey, normalizedScope)));
  }

  async decryptDetail(proposal: ProposalDetail, scope: ProposalCryptoScope | number): Promise<ProposalDetail> {
    const normalizedScope = this.normalizeScope(scope);
    const usesAnonymousComments = proposal.usesAnonymousComments ?? false;
    const comments = await this.decryptCommentsForDetail(proposal.comments, normalizedScope, usesAnonymousComments);

    if (proposal.hasPlaintextContent) {
      return {
        ...proposal,
        title: proposal.title ?? 'Editing crew settings',
        description: proposal.description ?? proposal.descriptionPreview ?? '',
        authorUsername: this.isAnonymousAuthor(proposal) ? 'Anonymous' : (proposal.authorUsername ?? 'Anonymous'),
        comments
      };
    }

    if (!proposal.hasEncryptedContent) {
      return {
        ...proposal,
        title: proposal.title ?? '',
        description: proposal.description ?? proposal.body ?? proposal.descriptionPreview ?? '',
        authorUsername: proposal.authorUsername ?? 'Unknown',
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

    const scopeKey = await this.resolveScopeKey(normalizedScope);
    const decrypted = await this.decryptListItem(proposal, scopeKey, normalizedScope);
    let payload: ProposalEncryptedPayload | null = null;
    try {
      payload = await this.decryptProposalPayload(proposal, scopeKey);
    } catch {
      payload = null;
    }

    const attachments = payload?.attachments ?? [];
    const resolvedAttachments = await this.decryptAttachments(normalizedScope, attachments);
    const unableToDecrypt = proposal.hasEncryptedContent && !payload;

    return {
      ...proposal,
      ...decrypted,
      title: unableToDecrypt ? '[Unable to decrypt]' : decrypted.title,
      description: payload?.description ?? (unableToDecrypt ? '[Unable to decrypt]' : ''),
      attachments,
      resolvedAttachments,
      comments,
      viewerAlias: proposal.viewerAlias,
      usesAnonymousComments: proposal.usesAnonymousComments,
      aliasRerollsRemaining: proposal.aliasRerollsRemaining
    };
  }

  private async decryptCommentsForDetail(
    comments: ProposalComment[],
    scope: ProposalCryptoScope,
    usesAnonymousComments = false
  ): Promise<ProposalComment[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return comments.map(c => ({
        ...c,
        body: c.hasEncryptedContent ? '[Encrypted]' : c.body,
        authorUsername: c.authorUsername || '[Encrypted]'
      }));
    }

    const scopeKey = await this.resolveScopeKey(scope);
    return Promise.all(comments.map(comment => this.decryptComment(comment, scopeKey, scope, usesAnonymousComments)));
  }

  async encryptProposalPayload(
    scope: ProposalCryptoScope | number,
    payload: ProposalEncryptedPayload,
    newAttachments: PendingAttachment[] = [],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string }> {
    const normalizedScope = this.normalizeScope(scope);
    const scopeKey = await this.resolveScopeKey(normalizedScope);
    const uploadedAttachments = await this.uploadAttachments(normalizedScope, newAttachments);
    const allAttachments = [...existingAttachments, ...uploadedAttachments];
    const fullPayload: ProposalEncryptedPayload = {
      ...payload,
      attachments: allAttachments,
      thumbnailResourceId: allAttachments.find(a => a.type === 'image')?.resourceId ?? null
    };
    return this.cryptoService.encryptJson(scopeKey, fullPayload);
  }

  async encryptCommentPayload(
    scope: ProposalCryptoScope | number,
    payload: ProposalCommentEncryptedPayload,
    newAttachments: PendingAttachment[] = [],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string }> {
    const normalizedScope = this.normalizeScope(scope);
    const scopeKey = await this.resolveScopeKey(normalizedScope);
    const storedAttachments = await this.uploadAttachments(normalizedScope, newAttachments);
    return this.cryptoService.encryptJson(scopeKey, {
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
    scopeKey: CryptoKey,
    scope: ProposalCryptoScope
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
      return {
        ...item,
        title: item.title ?? '',
        descriptionPreview: item.descriptionPreview ?? item.body ?? ''
      };
    }

    try {
      const payload = await this.cryptoService.decryptJson<ProposalEncryptedPayload>(
        scopeKey,
        item.encryptedPayload.nonce,
        item.encryptedPayload.ciphertext
      );
      const thumbnailUrl = await this.resolveThumbnail(scopeKey, payload, scope);
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

  async decryptComments(
    comments: ProposalComment[],
    scope: ProposalCryptoScope | number,
    usesAnonymousComments = false
  ): Promise<ProposalComment[]> {
    const normalizedScope = this.normalizeScope(scope);
    if (!this.cryptoSession.isUnlocked()) {
      return comments.map(c => ({
        ...c,
        body: c.hasEncryptedContent ? '[Encrypted]' : c.body,
        authorUsername: c.authorUsername || '[Encrypted]'
      }));
    }

    const scopeKey = await this.resolveScopeKey(normalizedScope);
    return Promise.all(comments.map(comment => this.decryptComment(comment, scopeKey, normalizedScope, usesAnonymousComments)));
  }

  private async decryptComment(
    comment: ProposalComment,
    scopeKey: CryptoKey,
    scope: ProposalCryptoScope,
    usesAnonymousComments = false
  ): Promise<ProposalComment> {
    if (!comment.hasEncryptedContent || !comment.encryptedPayload) {
      return comment;
    }

    try {
      const payload = await this.cryptoService.decryptJson<ProposalCommentEncryptedPayload>(
        scopeKey,
        comment.encryptedPayload.nonce,
        comment.encryptedPayload.ciphertext
      );
      const attachments = payload.attachments ?? [];
      const resolvedAttachments = await this.decryptAttachments(scope, attachments);
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
    scope: ProposalCryptoScope | number,
    attachments: ProposalAttachment[]
  ): Promise<ResolvedAttachment[]> {
    const normalizedScope = this.normalizeScope(scope);
    if (!attachments.length) {
      return [];
    }

    if (!this.cryptoSession.isUnlocked()) {
      return attachments.map(attachment => ({ ...attachment }));
    }

    const scopeKey = await this.resolveScopeKey(normalizedScope);
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
          this.cryptoApi.getEncryptedContents(
            contentType as EncryptedContentType,
            resourceIds,
            normalizedScope.crewId,
            normalizedScope.fleetId
          )
        );
        for (const envelope of envelopes) {
          try {
            const blobPayload = await this.cryptoService.decryptJson<{ dataUrl: string }>(
              scopeKey,
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
    scopeKey: CryptoKey,
    attachment: ProposalAttachment,
    scope: ProposalCryptoScope
  ): Promise<ResolvedAttachment> {
    const contentType = attachment.type === 'image'
      ? 'ImageAsset'
      : attachment.type === 'video'
        ? 'VideoAsset'
        : 'AudioAsset';

    try {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents(
          contentType,
          [attachment.resourceId],
          scope.crewId,
          scope.fleetId
        )
      );
      const envelope = envelopes[0];
      if (!envelope) {
        return { ...attachment };
      }

      const blobPayload = await this.cryptoService.decryptJson<{ dataUrl: string }>(
        scopeKey,
        envelope.nonce,
        envelope.ciphertext
      );
      return { ...attachment, dataUrl: blobPayload.dataUrl };
    } catch {
      return { ...attachment };
    }
  }

  private async resolveThumbnail(
    scopeKey: CryptoKey,
    payload: ProposalEncryptedPayload,
    scope: ProposalCryptoScope
  ): Promise<string | null> {
    const thumbId = payload.thumbnailResourceId
      ?? payload.attachments?.find(a => a.type === 'image')?.resourceId;
    if (!thumbId) {
      return null;
    }

    const envelopes = await firstValueFrom(
      this.cryptoApi.getEncryptedContents(
        'ImageAsset',
        [thumbId],
        scope.crewId,
        scope.fleetId
      )
    );
    const envelope = envelopes[0];
    if (!envelope) {
      return null;
    }

    try {
      const blobPayload = await this.cryptoService.decryptJson<{ dataUrl: string }>(
        scopeKey,
        envelope.nonce,
        envelope.ciphertext
      );
      return blobPayload.dataUrl;
    } catch {
      return null;
    }
  }

  private async uploadAttachments(scope: ProposalCryptoScope, attachments: PendingAttachment[]): Promise<ProposalAttachment[]> {
    if (!attachments.length) {
      return [];
    }

    const scopeKey = await this.resolveScopeKey(scope);
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

      const encrypted = await this.cryptoService.encryptJson(scopeKey, { dataUrl });
      const contentType = attachment.type === 'image'
        ? 'ImageAsset'
        : attachment.type === 'video'
          ? 'VideoAsset'
          : 'AudioAsset';

      await firstValueFrom(this.cryptoApi.upsertEncryptedContent({
        contentType,
        resourceId: attachment.resourceId,
        crewId: scope.crewId,
        fleetId: scope.fleetId,
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

  private normalizeScope(scope: ProposalCryptoScope | number): ProposalCryptoScope {
    return typeof scope === 'number' ? { crewId: scope } : scope;
  }

  private async resolveScopeKey(scope: ProposalCryptoScope): Promise<CryptoKey> {
    if (scope.fleetId) {
      return this.cryptoSession.ensureFleetKeyReady(scope.fleetId);
    }

    if (scope.crewId) {
      return this.cryptoSession.ensureCrewKeyReady(scope.crewId);
    }

    throw new Error('Encryption scope is required.');
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

  async uploadImageAttachment(
    scope: ProposalCryptoScope | number,
    attachment: PendingAttachment,
    contentType: Extract<EncryptedContentType, 'ImageAsset' | 'ProfileAvatar'> = 'ImageAsset'
  ): Promise<string> {
    const normalizedScope = this.normalizeScope(scope);
    const scopeKey = await this.resolveScopeKey(normalizedScope);

    let file = attachment.file;
    if (file && attachment.type === 'image') {
      file = await compressMediaFile(file, 'image');
    }

    let dataUrl = attachment.previewUrl ?? '';
    if (file) {
      dataUrl = await this.readFileAsDataUrl(file);
    } else if (attachment.blob) {
      dataUrl = await this.readBlobAsDataUrl(attachment.blob);
    }

    const encrypted = await this.cryptoService.encryptJson(scopeKey, { dataUrl });
    const result = await firstValueFrom(this.cryptoApi.upsertEncryptedContent({
      contentType,
      resourceId: attachment.resourceId,
      crewId: normalizedScope.crewId,
      fleetId: normalizedScope.fleetId,
      keyVersion: 1,
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext
    }));

    if (!result.success) {
      throw new Error(result.message || 'Failed to upload image.');
    }

    return attachment.resourceId;
  }

  async decryptImageDataUrl(
    scope: ProposalCryptoScope | number,
    resourceId: string,
    contentType: Extract<EncryptedContentType, 'ImageAsset' | 'ProfileAvatar'> = 'ImageAsset'
  ): Promise<string | null> {
    const normalizedScope = this.normalizeScope(scope);
    if (!resourceId || !this.cryptoSession.isUnlocked()) {
      return null;
    }

    const scopeKey = await this.resolveScopeKey(normalizedScope);
    const envelopes = await firstValueFrom(
      this.cryptoApi.getEncryptedContents(
        contentType,
        [resourceId],
        normalizedScope.crewId,
        normalizedScope.fleetId
      )
    );
    const envelope = envelopes[0];
    if (!envelope) {
      return null;
    }

    try {
      const blobPayload = await this.cryptoService.decryptJson<{ dataUrl: string }>(
        scopeKey,
        envelope.nonce,
        envelope.ciphertext
      );
      return blobPayload.dataUrl;
    } catch {
      return null;
    }
  }
}
