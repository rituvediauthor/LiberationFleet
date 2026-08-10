import { Injectable, inject } from '@angular/core';
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
import { buildMediaCacheKey, MediaBlobCacheService } from './media-blob-cache.service';
import { compressMediaFile, extractVideoPosterFrame } from '../../utils/media-compression.util';
import { pendingAttachmentsAllowSubmit } from '../../utils/pending-attachment.util';
import { MediaUploadQueueService } from '../media-upload-queue.service';

export interface ProposalCryptoScope {
  crewId?: number;
  fleetId?: number;
}

/** Cap eager video downloads after a list decrypt (crew/fleet feed). */
const MAX_VIDEO_PREFETCH_PER_LIST = 0;

@Injectable({
  providedIn: 'root'
})
export class ProposalCryptoService {
  private cryptoService = inject(CryptoService);
  private cryptoApi = inject(CryptoApiService);
  private cryptoSession = inject(CryptoSessionService);
  private mediaBlobCache = inject(MediaBlobCacheService);
  private uploadQueue = inject(MediaUploadQueueService);
  private readonly videoPrefetchInFlight = new Set<string>();

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
    const decryptedRows = await Promise.all(
      items.map(async (item, index) => {
        const localVideos: ProposalAttachment[] = [];
        const decrypted = await this.decryptListItem(item, scopeKey, normalizedScope, localVideos);
        return { index, decrypted, localVideos };
      })
    );
    decryptedRows.sort((a, b) => a.index - b.index);
    const videoPrefetchQueue = decryptedRows.flatMap(row => row.localVideos);
    this.scheduleVideoPrefetch(normalizedScope, videoPrefetchQueue);
    return decryptedRows.map(row => row.decrypted);
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
    if (!pendingAttachmentsAllowSubmit(newAttachments)) {
      throw new Error('Attachments are still processing. Wait until they finish or cancel them.');
    }

    const normalizedScope = this.normalizeScope(scope);
    const scopeKey = await this.resolveScopeKey(normalizedScope);
    const uploadedAttachments = await this.uploadAttachments(normalizedScope, newAttachments);
    const allAttachments = [...existingAttachments, ...uploadedAttachments];
    let thumbnailResourceId = allAttachments.find(a => a.type === 'image')?.resourceId ?? null;
    if (!thumbnailResourceId) {
      thumbnailResourceId = await this.uploadVideoPosterThumbnail(normalizedScope, newAttachments);
    }
    const fullPayload: ProposalEncryptedPayload = {
      ...payload,
      attachments: allAttachments,
      thumbnailResourceId
    };
    return this.cryptoService.encryptJson(scopeKey, fullPayload);
  }

  async encryptCommentPayload(
    scope: ProposalCryptoScope | number,
    payload: ProposalCommentEncryptedPayload,
    newAttachments: PendingAttachment[] = [],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string }> {
    if (!pendingAttachmentsAllowSubmit(newAttachments)) {
      throw new Error('Attachments are still processing. Wait until they finish or cancel them.');
    }

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
    scope: ProposalCryptoScope,
    videoPrefetchQueue?: ProposalAttachment[]
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
      const preview = item.descriptionPreview ?? item.body ?? '';
      return {
        ...item,
        title: item.title ?? '',
        descriptionPreview: preview.slice(0, 200),
        previewImageUrls: item.previewImageUrls ?? (item.thumbnailUrl ? [item.thumbnailUrl] : [])
      };
    }

    try {
      const payload = await this.cryptoService.decryptJson<ProposalEncryptedPayload>(
        scopeKey,
        item.encryptedPayload.nonce,
        item.encryptedPayload.ciphertext
      );
      const attachments = payload.attachments ?? [];
      if (videoPrefetchQueue) {
        for (const attachment of attachments) {
          if (attachment.type === 'video' && attachment.resourceId) {
            videoPrefetchQueue.push(attachment);
          }
        }
      }
      const previewImageUrls = await this.resolvePreviewImageUrls(scopeKey, payload, scope);
      return {
        ...item,
        title: payload.title,
        descriptionPreview: payload.description.slice(0, 200),
        authorUsername: this.resolveAuthorUsername(item, payload.authorDisplayName),
        thumbnailUrl: previewImageUrls[0] ?? null,
        previewImageUrls,
        hasVideoAttachment: attachments.some(attachment => attachment.type === 'video')
      };
    } catch {
      return {
        ...item,
        title: '[Unable to decrypt]',
        descriptionPreview: '[Unable to decrypt]'
      };
    }
  }

  /**
   * After list decrypt, warm the decrypted-media cache for the first few videos
   * (newest-first feed order). Best-effort; never blocks list rendering.
   */
  private scheduleVideoPrefetch(
    scope: ProposalCryptoScope,
    attachments: ProposalAttachment[]
  ): void {
    if (!attachments.length || !this.shouldPrefetchVideos()) {
      return;
    }

    const seen = new Set<string>();
    const unique: ProposalAttachment[] = [];
    for (const attachment of attachments) {
      if (!attachment.resourceId || seen.has(attachment.resourceId)) {
        continue;
      }
      seen.add(attachment.resourceId);
      unique.push(attachment);
      if (unique.length >= MAX_VIDEO_PREFETCH_PER_LIST) {
        break;
      }
    }

    if (unique.length === 0) {
      return;
    }

    // Yield so posters/titles paint first.
    const run = () => void this.prefetchVideosIntoCache(scope, unique);
    if (typeof requestIdleCallback === 'function') {
      requestIdleCallback(() => run(), { timeout: 2500 });
    } else {
      setTimeout(run, 0);
    }
  }

  private shouldPrefetchVideos(): boolean {
    if (typeof navigator === 'undefined') {
      return true;
    }
    const connection = (navigator as Navigator & {
      connection?: { saveData?: boolean; effectiveType?: string };
    }).connection;
    if (connection?.saveData) {
      return false;
    }
    const effective = connection?.effectiveType;
    if (effective === 'slow-2g' || effective === '2g') {
      return false;
    }
    return true;
  }

  /** Download + decrypt videos into IndexedDB only (no object URLs). Serial for iOS. */
  private async prefetchVideosIntoCache(
    scope: ProposalCryptoScope,
    attachments: ProposalAttachment[]
  ): Promise<void> {
    if (!this.cryptoSession.isUnlocked()) {
      return;
    }

    let scopeKey: CryptoKey;
    try {
      scopeKey = await this.resolveScopeKey(scope);
    } catch {
      return;
    }

    const keyVersion = this.resolveScopeKeyVersion(scope);

    for (const attachment of attachments) {
      const cacheKey = buildMediaCacheKey(scope, attachment.resourceId, keyVersion);
      if (this.videoPrefetchInFlight.has(cacheKey)) {
        continue;
      }
      try {
        if (await this.mediaBlobCache.has(cacheKey)) {
          continue;
        }
      } catch {
        continue;
      }

      this.videoPrefetchInFlight.add(cacheKey);
      try {
        const payload = await firstValueFrom(
          this.cryptoApi.getEncryptedContentBytes(
            'VideoAsset',
            attachment.resourceId,
            scope.crewId,
            scope.fleetId
          )
        );
        const putKey = buildMediaCacheKey(
          scope,
          payload.resourceId || attachment.resourceId,
          payload.keyVersion
        );
        if (await this.mediaBlobCache.has(putKey)) {
          continue;
        }
        const blob = await this.cryptoService.decryptMediaBytesToBlob(
          scopeKey,
          payload.nonce,
          payload.ciphertext
        );
        await this.mediaBlobCache.put(putKey, blob);
      } catch {
        // Prefetch is best-effort.
      } finally {
        this.videoPrefetchInFlight.delete(cacheKey);
      }
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
    const sessionKeyVersion = this.resolveScopeKeyVersion(normalizedScope);
    const grouped = new Map<string, ProposalAttachment[]>();
    for (const attachment of attachments) {
      const contentType = attachment.type === 'image'
        ? 'ImageAsset'
        : attachment.type === 'audio'
          ? 'AudioAsset'
          : 'VideoAsset';
      const bucket = grouped.get(contentType) ?? [];
      bucket.push(attachment);
      grouped.set(contentType, bucket);
    }

    const dataUrlByResourceId = new Map<string, string>();
    for (const [contentType, bucket] of grouped.entries()) {
      const resourceIds = bucket.map(attachment => attachment.resourceId);
      const useBinaryDownload = contentType === 'VideoAsset' || contentType === 'AudioAsset';

      const pendingIds: string[] = [];
      for (const resourceId of resourceIds) {
        const cachedUrl = await this.tryCachedMediaUrl(normalizedScope, resourceId, sessionKeyVersion);
        if (cachedUrl) {
          dataUrlByResourceId.set(resourceId, cachedUrl);
        } else {
          pendingIds.push(resourceId);
        }
      }

      if (pendingIds.length === 0) {
        continue;
      }

      if (useBinaryDownload) {
        await Promise.all(pendingIds.map(async resourceId => {
          try {
            const payload = await firstValueFrom(
              this.cryptoApi.getEncryptedContentBytes(
                contentType as EncryptedContentType,
                resourceId,
                normalizedScope.crewId,
                normalizedScope.fleetId
              )
            );
            const url = await this.decryptMediaBytesCached(
              scopeKey,
              normalizedScope,
              payload.resourceId || resourceId,
              payload.keyVersion,
              payload.nonce,
              payload.ciphertext
            );
            dataUrlByResourceId.set(payload.resourceId || resourceId, url);
          } catch {
            // Skip unreadable attachments.
          }
        }));
        continue;
      }

      try {
        const envelopes = await firstValueFrom(
          this.cryptoApi.getEncryptedContents(
            contentType as EncryptedContentType,
            pendingIds,
            normalizedScope.crewId,
            normalizedScope.fleetId
          )
        );
        for (const envelope of envelopes) {
          try {
            const url = await this.decryptMediaCached(
              scopeKey,
              normalizedScope,
              envelope.resourceId,
              envelope.keyVersion,
              envelope.nonce,
              envelope.ciphertext
            );
            dataUrlByResourceId.set(envelope.resourceId, url);
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
      : attachment.type === 'audio'
        ? 'AudioAsset'
        : 'VideoAsset';

    try {
      const sessionKeyVersion = this.resolveScopeKeyVersion(scope);
      const cachedUrl = await this.tryCachedMediaUrl(scope, attachment.resourceId, sessionKeyVersion);
      if (cachedUrl) {
        return { ...attachment, dataUrl: cachedUrl };
      }

      if (contentType === 'VideoAsset' || contentType === 'AudioAsset') {
        const payload = await firstValueFrom(
          this.cryptoApi.getEncryptedContentBytes(
            contentType,
            attachment.resourceId,
            scope.crewId,
            scope.fleetId
          )
        );
        const url = await this.decryptMediaBytesCached(
          scopeKey,
          scope,
          payload.resourceId || attachment.resourceId,
          payload.keyVersion,
          payload.nonce,
          payload.ciphertext
        );
        return { ...attachment, dataUrl: url };
      }

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

      const url = await this.decryptMediaCached(
        scopeKey,
        scope,
        envelope.resourceId,
        envelope.keyVersion,
        envelope.nonce,
        envelope.ciphertext
      );
      return { ...attachment, dataUrl: url };
    } catch {
      return { ...attachment };
    }
  }

  private async resolvePreviewImageUrls(
    scopeKey: CryptoKey,
    payload: ProposalEncryptedPayload,
    scope: ProposalCryptoScope
  ): Promise<string[]> {
    const imageIds: string[] = (payload.attachments ?? [])
      .filter(attachment => attachment.type === 'image' && !!attachment.resourceId)
      .map(attachment => attachment.resourceId)
      .slice(0, 20);

    // Always include the dedicated list poster (video posts rely on this).
    if (payload.thumbnailResourceId && !imageIds.includes(payload.thumbnailResourceId)) {
      imageIds.unshift(payload.thumbnailResourceId);
    }

    if (imageIds.length === 0) {
      return [];
    }

    const sessionKeyVersion = this.resolveScopeKeyVersion(scope);
    const urls: (string | null)[] = new Array(imageIds.length).fill(null);
    const pendingIds: string[] = [];
    const pendingIndexes: number[] = [];

    for (let i = 0; i < imageIds.length; i++) {
      const resourceId = imageIds[i];
      const cachedUrl = await this.tryCachedMediaUrl(scope, resourceId, sessionKeyVersion);
      if (cachedUrl) {
        urls[i] = cachedUrl;
      } else {
        pendingIds.push(resourceId);
        pendingIndexes.push(i);
      }
    }

    if (pendingIds.length > 0) {
      const envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents(
          'ImageAsset',
          pendingIds,
          scope.crewId,
          scope.fleetId
        )
      );
      const envelopeById = new Map(envelopes.map(envelope => [envelope.resourceId, envelope]));

      await Promise.all(pendingIndexes.map(async (index, pendingOffset) => {
        const resourceId = pendingIds[pendingOffset];
        const envelope = envelopeById.get(resourceId);
        if (!envelope) {
          return;
        }

        try {
          urls[index] = await this.decryptMediaCached(
            scopeKey,
            scope,
            envelope.resourceId,
            envelope.keyVersion,
            envelope.nonce,
            envelope.ciphertext
          );
        } catch {
          urls[index] = null;
        }
      }));
    }

    return urls.filter((url): url is string => !!url);
  }

  private resolveScopeKeyVersion(scope: ProposalCryptoScope): number {
    if (scope.fleetId) {
      return this.cryptoSession.getFleetKeyVersion(scope.fleetId) ?? 1;
    }
    if (scope.crewId) {
      return this.cryptoSession.getCrewKeyVersion(scope.crewId) ?? 1;
    }
    return 1;
  }

  private async tryCachedMediaUrl(
    scope: ProposalCryptoScope,
    resourceId: string,
    keyVersion: number
  ): Promise<string | null> {
    const cacheKey = buildMediaCacheKey(scope, resourceId, keyVersion);
    const blob = await this.mediaBlobCache.get(cacheKey);
    return blob ? URL.createObjectURL(blob) : null;
  }

  private async decryptMediaCached(
    scopeKey: CryptoKey,
    scope: ProposalCryptoScope,
    resourceId: string,
    keyVersion: number,
    nonce: string,
    ciphertext: string
  ): Promise<string> {
    const cacheKey = buildMediaCacheKey(scope, resourceId, keyVersion);
    const cached = await this.mediaBlobCache.get(cacheKey);
    if (cached) {
      return URL.createObjectURL(cached);
    }

    const blob = await this.cryptoService.decryptMediaToBlob(scopeKey, nonce, ciphertext);
    void this.mediaBlobCache.put(cacheKey, blob);
    return URL.createObjectURL(blob);
  }

  private async decryptMediaBytesCached(
    scopeKey: CryptoKey,
    scope: ProposalCryptoScope,
    resourceId: string,
    keyVersion: number,
    nonce: string,
    ciphertext: Uint8Array | ArrayBuffer
  ): Promise<string> {
    const cacheKey = buildMediaCacheKey(scope, resourceId, keyVersion);
    const cached = await this.mediaBlobCache.get(cacheKey);
    if (cached) {
      return URL.createObjectURL(cached);
    }

    const blob = await this.cryptoService.decryptMediaBytesToBlob(scopeKey, nonce, ciphertext);
    void this.mediaBlobCache.put(cacheKey, blob);
    return URL.createObjectURL(blob);
  }

  /**
   * Start encrypt+upload as soon as a file is compressed (background).
   * Create/send can proceed once `uploaded` is true.
   */
  async uploadAttachmentInBackground(
    scope: ProposalCryptoScope | number,
    attachment: PendingAttachment,
    onLocalProgress?: (percent: number, label: string) => void
  ): Promise<void> {
    if (attachment.uploaded) {
      return;
    }
    const normalizedScope = this.normalizeScope(scope);
    const label = attachment.fileName || attachment.file?.name || `${attachment.type} attachment`;
    const jobId = this.uploadQueue.createJob(label);
    attachment.status = 'uploading';
    attachment.progress = 0;
    attachment.progressLabel = 'Encrypting…';
    onLocalProgress?.(0, 'Encrypting…');

    try {
      await this.encryptAndUpsertAttachment(normalizedScope, attachment, jobId, percent => {
        attachment.progress = percent;
        attachment.progressLabel = percent < 40 ? 'Encrypting…' : 'Uploading…';
        onLocalProgress?.(percent, attachment.progressLabel);
      });
      attachment.uploaded = true;
      attachment.status = 'ready';
      attachment.progress = 100;
      attachment.progressLabel = 'Ready';
      onLocalProgress?.(100, 'Ready');
      this.uploadQueue.updateJob(jobId, { phase: 'done', progress: 100 });
    } catch (error) {
      attachment.status = 'error';
      attachment.progress = 0;
      attachment.progressLabel = 'Upload failed';
      attachment.uploaded = false;
      const message = error instanceof Error ? error.message : 'Upload failed';
      this.uploadQueue.updateJob(jobId, { phase: 'error', progress: 0, error: message });
      throw error;
    }
  }

  private async uploadAttachments(scope: ProposalCryptoScope, attachments: PendingAttachment[]): Promise<ProposalAttachment[]> {
    if (!attachments.length) {
      return [];
    }

    const results: ProposalAttachment[] = [];

    for (const attachment of attachments) {
      if (!attachment.uploaded) {
        const label = attachment.fileName || attachment.file?.name || `${attachment.type} attachment`;
        const jobId = this.uploadQueue.createJob(label);
        try {
          await this.encryptAndUpsertAttachment(scope, attachment, jobId);
          attachment.uploaded = true;
          this.uploadQueue.updateJob(jobId, { phase: 'done', progress: 100 });
        } catch (error) {
          const message = error instanceof Error ? error.message : 'Upload failed';
          this.uploadQueue.updateJob(jobId, { phase: 'error', progress: 0, error: message });
          throw error;
        }
      }

      results.push({
        resourceId: attachment.resourceId,
        type: attachment.type,
        fileName: attachment.fileName ?? attachment.file?.name,
        mimeType: attachment.file?.type
      });
    }

    return results;
  }

  private async encryptAndUpsertAttachment(
    scope: ProposalCryptoScope,
    attachment: PendingAttachment,
    jobId: string,
    onProgress?: (percent: number) => void
  ): Promise<void> {
    this.uploadQueue.updateJob(jobId, { phase: 'encrypting', progress: 5 });
    onProgress?.(5);

    let file = attachment.file;
    const alreadyPrepared = (attachment.status === 'ready' || attachment.status === 'uploading') && !!file;
    if (
      file
      && !alreadyPrepared
      && (attachment.type === 'image' || attachment.type === 'video' || attachment.type === 'audio')
    ) {
      file = await compressMediaFile(file, attachment.type);
      attachment.file = file;
    }

    if (!file && attachment.blob) {
      const raw = new File([attachment.blob], attachment.fileName || `audio-${Date.now()}.webm`, {
        type: attachment.blob.type || 'audio/webm',
        lastModified: Date.now()
      });
      file = attachment.type === 'audio' && attachment.status !== 'ready' && attachment.status !== 'uploading'
        ? await compressMediaFile(raw, 'audio')
        : raw;
      attachment.file = file;
    }

    if (!file) {
      throw new Error('Attachment file is missing.');
    }

    const scopeKey = await this.resolveScopeKey(scope);
    this.uploadQueue.updateJob(jobId, { phase: 'encrypting', progress: 15 });
    onProgress?.(15);

    const contentType = attachment.type === 'image'
      ? 'ImageAsset'
      : attachment.type === 'audio'
        ? 'AudioAsset'
        : 'VideoAsset';

    const useBinaryUpload = contentType === 'VideoAsset' || contentType === 'AudioAsset';

    // Video/audio: chunked encrypt → Blob PUT (Signal-style). Never load the whole file
    // + base64 into RAM — that reloads the iOS PWA tab.
    if (useBinaryUpload) {
      const encrypted = await this.cryptoService.encryptMediaBlob(
        scopeKey,
        file,
        file.type || 'application/octet-stream',
        (percent) => {
          const mapped = 15 + Math.round(percent * 0.2);
          this.uploadQueue.updateJob(jobId, { phase: 'encrypting', progress: mapped });
          onProgress?.(mapped);
        }
      );

      this.uploadQueue.updateJob(jobId, { phase: 'uploading', progress: 35 });
      onProgress?.(35);

      const result = await firstValueFrom(
        this.cryptoApi.upsertEncryptedContentBytesWithProgress(
          {
            contentType,
            resourceId: attachment.resourceId,
            crewId: scope.crewId,
            fleetId: scope.fleetId,
            keyVersion: 1,
            nonce: encrypted.nonce,
            ciphertext: encrypted.ciphertext
          },
          uploadPercent => {
            const mapped = 35 + Math.round(uploadPercent * 0.6);
            this.uploadQueue.updateJob(jobId, { phase: 'uploading', progress: mapped });
            onProgress?.(mapped);
          }
        )
      );

      if (!result.success) {
        throw new Error(result.message || 'Failed to upload attachment.');
      }

      this.uploadQueue.updateJob(jobId, { phase: 'finalizing', progress: 98 });
      onProgress?.(98);
      return;
    }

    const fileBytes = new Uint8Array(await file.arrayBuffer());
    const encrypted = await this.cryptoService.encryptMediaBytes(
      scopeKey,
      fileBytes,
      file.type || 'application/octet-stream'
    );

    this.uploadQueue.updateJob(jobId, { phase: 'uploading', progress: 35 });
    onProgress?.(35);

    const result = await firstValueFrom(
      this.cryptoApi.upsertEncryptedContentWithProgress(
        {
          contentType,
          resourceId: attachment.resourceId,
          crewId: scope.crewId,
          fleetId: scope.fleetId,
          keyVersion: 1,
          nonce: encrypted.nonce,
          ciphertext: encrypted.ciphertext
        },
        uploadPercent => {
          const mapped = 35 + Math.round(uploadPercent * 0.6);
          this.uploadQueue.updateJob(jobId, { phase: 'uploading', progress: mapped });
          onProgress?.(mapped);
        }
      )
    );

    if (!result.success) {
      throw new Error(result.message || 'Failed to upload attachment.');
    }

    this.uploadQueue.updateJob(jobId, { phase: 'finalizing', progress: 98 });
    onProgress?.(98);
  }

  /** Upload a JPEG poster for the first video so list cards can show a preview. */
  private async uploadVideoPosterThumbnail(
    scope: ProposalCryptoScope,
    attachments: PendingAttachment[]
  ): Promise<string | null> {
    const video = attachments.find(attachment => attachment.type === 'video' && (attachment.file || attachment.blob || attachment.thumbnailUrl));
    if (!video) {
      return null;
    }

    try {
      let poster: File;
      const source = video.file ?? video.blob;
      if (source) {
        // Prefer a fresh frame from the prepared file (iOS chips can be black).
        try {
          poster = await extractVideoPosterFrame(source);
        } catch {
          if (!video.thumbnailUrl?.startsWith('blob:')) {
            return null;
          }
          const blob = await fetch(video.thumbnailUrl).then(response => response.blob());
          poster = new File([blob], `video-poster-${Date.now()}.jpg`, {
            type: 'image/jpeg',
            lastModified: Date.now()
          });
        }
      } else if (video.thumbnailUrl?.startsWith('blob:')) {
        const blob = await fetch(video.thumbnailUrl).then(response => response.blob());
        poster = new File([blob], `video-poster-${Date.now()}.jpg`, {
          type: 'image/jpeg',
          lastModified: Date.now()
        });
      } else {
        return null;
      }

      const resourceId = this.createResourceId();
      await this.uploadImageAttachment(scope, {
        type: 'image',
        resourceId,
        file: poster,
        status: 'ready',
        fileName: poster.name
      });
      return resourceId;
    } catch {
      return null;
    }
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
    if (file && attachment.type === 'image' && attachment.status !== 'ready' && attachment.status !== 'uploading') {
      file = await compressMediaFile(file, 'image');
    }

    if (!file && attachment.blob) {
      file = new File([attachment.blob], attachment.fileName || `image-${Date.now()}.jpg`, {
        type: attachment.blob.type || 'image/jpeg',
        lastModified: Date.now()
      });
    }

    if (!file) {
      throw new Error('Image file is missing.');
    }

    const fileBytes = new Uint8Array(await file.arrayBuffer());
    const encrypted = await this.cryptoService.encryptMediaBytes(
      scopeKey,
      fileBytes,
      file.type || 'image/jpeg'
    );
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
      return await this.cryptoService.decryptMediaToObjectUrl(
        scopeKey,
        envelope.nonce,
        envelope.ciphertext
      );
    } catch {
      return null;
    }
  }
}
