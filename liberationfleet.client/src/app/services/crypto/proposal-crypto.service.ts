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
import { buildPlainMediaPayload, MEDIA_PLAIN_NONCE } from '../../utils/media-chunk-crypto.util';
import { MediaUploadQueueService } from '../media-upload-queue.service';
import { AuthService } from '../auth.service';

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
  private authService = inject(AuthService);
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
          if (attachment.type === 'video' && attachment.resourceId && attachment.encrypted !== false) {
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

    // Defer crew/fleet key resolve until an encrypted envelope needs AES.
    // Unencrypted videos must not wait on key material before download/parse.
    let scopeKeyPromise: Promise<CryptoKey> | null = null;
    const getScopeKey = (): Promise<CryptoKey> => {
      if (!scopeKeyPromise) {
        scopeKeyPromise = this.resolveScopeKey(normalizedScope);
      }
      return scopeKeyPromise;
    };

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
    const attachmentByResourceId = new Map(
      attachments.map(attachment => [attachment.resourceId, attachment] as const)
    );

    for (const [contentType, bucket] of grouped.entries()) {
      const pendingIds: string[] = [];
      for (const attachment of bucket) {
        // Known-plain video: stream only — never fall through to full /content/bytes.
        if (attachment.type === 'video' && attachment.encrypted === false) {
          const streamUrl = this.tryBuildPlainMediaStreamUrl(
            contentType as EncryptedContentType,
            attachment.resourceId,
            normalizedScope
          );
          if (streamUrl) {
            dataUrlByResourceId.set(attachment.resourceId, streamUrl);
          }
          continue;
        }

        const cachedUrl = await this.tryCachedMediaUrl(
          normalizedScope,
          attachment.resourceId,
          sessionKeyVersion
        );
        if (cachedUrl) {
          dataUrlByResourceId.set(attachment.resourceId, cachedUrl);
        } else {
          pendingIds.push(attachment.resourceId);
        }
      }

      if (pendingIds.length === 0) {
        continue;
      }

      const useBinaryDownload = contentType === 'VideoAsset'
        || contentType === 'AudioAsset'
        || bucket.some(attachment => attachment.encrypted === false);

      if (useBinaryDownload) {
        // Probe nonce first for video/audio so plain envelopes stream instead of
        // downloading up to ~600 MB into memory (which never meaningfully finishes).
        const downloadIds: string[] = [];
        await Promise.all(pendingIds.map(async resourceId => {
          const attachment = attachmentByResourceId.get(resourceId);
          const shouldProbe = contentType === 'VideoAsset'
            || contentType === 'AudioAsset'
            || attachment?.encrypted === false;
          if (!shouldProbe) {
            downloadIds.push(resourceId);
            return;
          }

          try {
            const meta = await firstValueFrom(
              this.cryptoApi.getEncryptedContentMeta(
                contentType as EncryptedContentType,
                resourceId,
                normalizedScope.crewId,
                normalizedScope.fleetId
              )
            );
            if (meta.nonce === MEDIA_PLAIN_NONCE) {
              const streamUrl = this.tryBuildPlainMediaStreamUrl(
                contentType as EncryptedContentType,
                meta.resourceId || resourceId,
                normalizedScope
              );
              if (streamUrl) {
                dataUrlByResourceId.set(meta.resourceId || resourceId, streamUrl);
              }
              // Plain but no token / stream URL: skip — do not full-download.
              return;
            }
            downloadIds.push(resourceId);
          } catch {
            // Meta unavailable — only full-download when the attachment is not marked plain.
            if (attachment?.encrypted !== false) {
              downloadIds.push(resourceId);
            }
          }
        }));

        await Promise.all(downloadIds.map(async resourceId => {
          try {
            const payload = await firstValueFrom(
              this.cryptoApi.getEncryptedContentBytes(
                contentType as EncryptedContentType,
                resourceId,
                normalizedScope.crewId,
                normalizedScope.fleetId
              )
            );

            // Safety net: if bytes endpoint still returns a plain envelope, stream instead.
            if (payload.nonce === MEDIA_PLAIN_NONCE) {
              const streamUrl = this.tryBuildPlainMediaStreamUrl(
                contentType as EncryptedContentType,
                payload.resourceId || resourceId,
                normalizedScope
              );
              if (streamUrl) {
                dataUrlByResourceId.set(payload.resourceId || resourceId, streamUrl);
              }
              return;
            }

            const scopeKey = await getScopeKey();
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
        const scopeKey = await getScopeKey();
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

    const resolved: ResolvedAttachment[] = attachments.map(attachment => ({
      ...attachment,
      dataUrl: dataUrlByResourceId.get(attachment.resourceId)
    }));

    const posterIds = [...new Set(
      resolved
        .map(attachment => attachment.posterResourceId)
        .filter((id): id is string => !!id)
    )];
    const posterUrlById = new Map<string, string>();
    if (posterIds.length > 0 && this.cryptoSession.isUnlocked()) {
      const pendingPosterIds: string[] = [];
      for (const posterId of posterIds) {
        const cachedUrl = await this.tryCachedMediaUrl(normalizedScope, posterId, sessionKeyVersion);
        if (cachedUrl) {
          posterUrlById.set(posterId, cachedUrl);
        } else {
          pendingPosterIds.push(posterId);
        }
      }
      if (pendingPosterIds.length > 0) {
        try {
          const scopeKey = await getScopeKey();
          const envelopes = await firstValueFrom(
            this.cryptoApi.getEncryptedContents(
              'ImageAsset',
              pendingPosterIds,
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
              posterUrlById.set(envelope.resourceId, url);
            } catch {
              // Skip unreadable posters.
            }
          }
        } catch {
          // Skip poster batch.
        }
      }
    }

    for (const attachment of resolved) {
      if (attachment.type !== 'video' || !attachment.posterResourceId) {
        continue;
      }
      attachment.posterUrl = posterUrlById.get(attachment.posterResourceId);
    }

    return resolved;
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
      if (attachment.type === 'video') {
        if (attachment.encrypted === false) {
          const streamUrl = this.tryBuildPlainMediaStreamUrl(contentType, attachment.resourceId, scope);
          if (streamUrl) {
            return { ...attachment, dataUrl: streamUrl };
          }
          return { ...attachment };
        }

        try {
          const meta = await firstValueFrom(
            this.cryptoApi.getEncryptedContentMeta(
              contentType,
              attachment.resourceId,
              scope.crewId,
              scope.fleetId
            )
          );
          if (meta.nonce === MEDIA_PLAIN_NONCE) {
            const streamUrl = this.tryBuildPlainMediaStreamUrl(
              contentType,
              meta.resourceId || attachment.resourceId,
              scope
            );
            if (streamUrl) {
              return { ...attachment, dataUrl: streamUrl, encrypted: false };
            }
            return { ...attachment, encrypted: false };
          }
        } catch {
          // Fall through to encrypted download path.
        }
      }

      const sessionKeyVersion = this.resolveScopeKeyVersion(scope);
      const cachedUrl = await this.tryCachedMediaUrl(scope, attachment.resourceId, sessionKeyVersion);
      if (cachedUrl) {
        return { ...attachment, dataUrl: cachedUrl };
      }

      if (contentType === 'VideoAsset' || contentType === 'AudioAsset' || attachment.encrypted === false) {
        const payload = await firstValueFrom(
          this.cryptoApi.getEncryptedContentBytes(
            contentType,
            attachment.resourceId,
            scope.crewId,
            scope.fleetId
          )
        );
        if (payload.nonce === MEDIA_PLAIN_NONCE) {
          const streamUrl = this.tryBuildPlainMediaStreamUrl(
            contentType,
            payload.resourceId || attachment.resourceId,
            scope
          );
          if (streamUrl) {
            return { ...attachment, dataUrl: streamUrl, encrypted: false };
          }
          return { ...attachment, encrypted: false };
        }
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
    // Uploads hardcode keyVersion 1; session version can drift after rotation.
    // Try both so plain/encrypted media still hit IndexedDB without a re-download.
    const versions = keyVersion === 1 ? [1] : [keyVersion, 1];
    for (const version of versions) {
      const cacheKey = buildMediaCacheKey(scope, resourceId, version);
      const blob = await this.mediaBlobCache.get(cacheKey);
      if (blob) {
        return URL.createObjectURL(blob);
      }
    }
    return null;
  }

  private tryBuildPlainMediaStreamUrl(
    contentType: EncryptedContentType,
    resourceId: string,
    scope: ProposalCryptoScope
  ): string | null {
    const accessToken = this.authService.getToken();
    if (!accessToken || !resourceId) {
      return null;
    }

    return this.cryptoApi.buildPlainMediaStreamUrl({
      contentType,
      resourceId,
      accessToken,
      crewId: scope.crewId,
      fleetId: scope.fleetId
    });
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
    scopeKey: CryptoKey | null,
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
        mimeType: attachment.file?.type,
        role: attachment.role,
        encrypted: attachment.encrypted !== false,
        posterResourceId: attachment.type === 'video'
          ? (await this.uploadSingleVideoPoster(scope, attachment) ?? undefined)
          : undefined
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
    const storeEncrypted = attachment.encrypted !== false;
    this.uploadQueue.updateJob(jobId, {
      phase: storeEncrypted ? 'encrypting' : 'uploading',
      progress: 5
    });
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
    this.uploadQueue.updateJob(jobId, {
      phase: storeEncrypted ? 'encrypting' : 'uploading',
      progress: 15
    });
    onProgress?.(15);

    const contentType = attachment.type === 'image'
      ? 'ImageAsset'
      : attachment.type === 'audio'
        ? 'AudioAsset'
        : 'VideoAsset';

    // Unencrypted payloads always use the binary path (supports large plain images/files).
    const useBinaryUpload = !storeEncrypted
      || contentType === 'VideoAsset'
      || contentType === 'AudioAsset';

    if (!storeEncrypted) {
      const fileBytes = new Uint8Array(await file.arrayBuffer());
      const plainPayload = buildPlainMediaPayload(fileBytes, file.type || 'application/octet-stream');
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
            nonce: MEDIA_PLAIN_NONCE,
            ciphertext: new Blob([plainPayload], { type: 'application/octet-stream' })
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

  /** Upload a JPEG poster for a single video so in-thread players can show a still. */
  private async uploadSingleVideoPoster(
    scope: ProposalCryptoScope,
    video: PendingAttachment
  ): Promise<string | null> {
    return this.uploadVideoPosterThumbnail(scope, [video]);
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

    return this.cryptoSession.ensureUserContentKeyReady();
  }

  async hasEncryptedContent(
    scope: ProposalCryptoScope,
    resourceId: string,
    contentType: EncryptedContentType
  ): Promise<boolean> {
    const crewId = scope.crewId && scope.crewId > 0 ? scope.crewId : undefined;
    const fleetId = scope.fleetId && scope.fleetId > 0 ? scope.fleetId : undefined;
    const envelopes = await firstValueFrom(
      this.cryptoApi.getEncryptedContents(contentType, [resourceId], crewId, fleetId)
    );
    return !!envelopes[0];
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
      crewId: normalizedScope.crewId && normalizedScope.crewId > 0 ? normalizedScope.crewId : undefined,
      fleetId: normalizedScope.fleetId && normalizedScope.fleetId > 0 ? normalizedScope.fleetId : undefined,
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

    let scopeKey = await this.resolveScopeKey(normalizedScope);
    let envelopes = await firstValueFrom(
      this.cryptoApi.getEncryptedContents(
        contentType,
        [resourceId],
        normalizedScope.crewId && normalizedScope.crewId > 0 ? normalizedScope.crewId : undefined,
        normalizedScope.fleetId && normalizedScope.fleetId > 0 ? normalizedScope.fleetId : undefined
      )
    );
    if (
      !envelopes[0]
      && contentType === 'ProfileAvatar'
      && ((normalizedScope.crewId && normalizedScope.crewId > 0) || (normalizedScope.fleetId && normalizedScope.fleetId > 0))
    ) {
      envelopes = await firstValueFrom(
        this.cryptoApi.getEncryptedContents(contentType, [resourceId])
      );
      if (envelopes[0]) {
        scopeKey = await this.cryptoSession.ensureUserContentKeyReady();
      }
    }
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
