import { Component, EventEmitter, Input, Output, ViewChild, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { ResolvedAttachment } from '../../models/proposal.model';
import { EncryptedContentType } from '../../models/crypto.model';
import { LibraryImageCarouselComponent } from '../library-image-carousel/library-image-carousel.component';
import { isPlainMediaStreamUrl, isSafeMediaDataUrl } from '../../utils/media-attachment-allowlist.util';
import { enterMediaDetailZoom, exitMediaDetailZoom } from '../../utils/media-viewport-zoom';
import { CryptoApiService } from '../../services/crypto/crypto-api.service';

/** Above this size, skip full-file blob buffering on Play (progressive only). */
const MAX_PLAIN_BLOB_BYTES = 48 * 1024 * 1024;

@Component({
  selector: 'app-proposal-attachment-display',
  standalone: true,
  imports: [CommonModule, LibraryImageCarouselComponent],
  templateUrl: './proposal-attachment-display.component.html',
  styleUrl: './proposal-attachment-display.component.css'
})
export class ProposalAttachmentDisplayComponent {
  private readonly cryptoApi = inject(CryptoApiService);
  private readonly cdr = inject(ChangeDetectorRef);

  @Input() attachments: ResolvedAttachment[] = [];
  @Input() compact = false;
  @Input() canDelete = false;
  @Input() crewId = 0;
  @Output() attachmentDeleted = new EventEmitter<string>();

  @ViewChild('imageCarousel') imageCarousel?: LibraryImageCarouselComponent;

  /** Tracks native fullscreen so chrome hide/show stays balanced. */
  private videoFullscreenActive = false;

  /** resourceIds converting plain-media stream → blob for playback. */
  private readonly blobFallbackInflight = new Set<string>();

  /** resourceIds unlocked for native controls (progressive play or blob ready). */
  private readonly playbackUnlocked = new Set<string>();

  /** resourceIds showing "Preparing playback…" */
  preparingPlayback = new Set<string>();

  /** Last prepare error message by resourceId. */
  prepareError = new Map<string, string>();

  get imageAttachments(): ResolvedAttachment[] {
    return this.attachments.filter(attachment => attachment.type === 'image');
  }

  get nonImageAttachments(): ResolvedAttachment[] {
    return this.attachments.filter(attachment => attachment.type !== 'image');
  }

  get imageUrls(): string[] {
    return this.imageAttachments
      .map(attachment => attachment.dataUrl)
      .filter((url): url is string => !!url && isSafeMediaDataUrl(url));
  }

  safeDataUrl(attachment: ResolvedAttachment): string | null {
    return isSafeMediaDataUrl(attachment.dataUrl) ? attachment.dataUrl! : null;
  }

  safePosterUrl(attachment: ResolvedAttachment): string | null {
    return isSafeMediaDataUrl(attachment.posterUrl) ? attachment.posterUrl! : null;
  }

  get unresolvedImageAttachments(): ResolvedAttachment[] {
    return this.imageAttachments.filter(attachment => !this.safeDataUrl(attachment));
  }

  isPlainStream(attachment: ResolvedAttachment): boolean {
    return isPlainMediaStreamUrl(attachment.dataUrl || '');
  }

  needsPlainPlayButton(attachment: ResolvedAttachment): boolean {
    return this.isPlainStream(attachment)
      && !this.playbackUnlocked.has(attachment.resourceId)
      && !this.preparingPlayback.has(attachment.resourceId);
  }

  deleteAttachment(attachment: ResolvedAttachment) {
    if (!this.canDelete || !this.crewId) {
      return;
    }

    this.attachmentDeleted.emit(attachment.resourceId);
  }

  deleteActiveImage() {
    const activeIndex = this.imageCarousel?.activeIndex ?? 0;
    const resolvedImages = this.imageAttachments.filter(attachment => !!this.safeDataUrl(attachment));
    const attachment = resolvedImages[activeIndex];
    if (attachment) {
      this.deleteAttachment(attachment);
    }
  }

  contentTypeFor(attachment: ResolvedAttachment): EncryptedContentType {
    if (attachment.type === 'video') {
      return 'VideoAsset';
    }

    if (attachment.type === 'audio') {
      return 'AudioAsset';
    }

    return 'ImageAsset';
  }

  /**
   * Dedicated Play starts under a real user gesture:
   * 1) Try progressive stream play() first (no full download)
   * 2) If that fails, fetch the same access_token URL into a blob: src
   */
  startPlainPlayback(attachment: ResolvedAttachment, media: HTMLMediaElement): void {
    void this.unlockPlainPlayback(attachment, media);
  }

  onMediaError(event: Event, attachment: ResolvedAttachment): void {
    if (!this.isPlainStream(attachment) || this.playbackUnlocked.has(attachment.resourceId)) {
      return;
    }
    const media = event.target as HTMLMediaElement | null;
    void this.unlockPlainPlayback(attachment, media, /* preferBlob */ true);
  }

  onVideoFullscreenEnter(): void {
    if (this.videoFullscreenActive) {
      return;
    }
    this.videoFullscreenActive = true;
    enterMediaDetailZoom();
  }

  onVideoFullscreenExit(): void {
    if (!this.videoFullscreenActive) {
      return;
    }
    this.videoFullscreenActive = false;
    exitMediaDetailZoom();
  }

  onVideoFullscreenChange(event: Event): void {
    const video = event.target as HTMLVideoElement | null;
    if (!video) {
      return;
    }
    const active =
      document.fullscreenElement === video ||
      (document as Document & { webkitFullscreenElement?: Element | null }).webkitFullscreenElement === video;
    if (active) {
      this.onVideoFullscreenEnter();
    } else {
      this.onVideoFullscreenExit();
    }
  }

  private async unlockPlainPlayback(
    attachment: ResolvedAttachment,
    media: HTMLMediaElement | null,
    preferBlob = false
  ): Promise<void> {
    const streamUrl = attachment.dataUrl;
    if (!streamUrl || !isPlainMediaStreamUrl(streamUrl)) {
      return;
    }
    if (!attachment.resourceId || this.blobFallbackInflight.has(attachment.resourceId)) {
      return;
    }

    this.blobFallbackInflight.add(attachment.resourceId);
    this.preparingPlayback.add(attachment.resourceId);
    this.prepareError.delete(attachment.resourceId);
    this.cdr.markForCheck();

    try {
      if (media && !preferBlob) {
        const progressiveOk = await this.tryProgressivePlay(media);
        if (progressiveOk) {
          this.playbackUnlocked.add(attachment.resourceId);
          return;
        }
        media.pause();
      }

      const length = await firstValueFrom(this.cryptoApi.plainMediaContentLength(streamUrl));
      if (length != null && length > MAX_PLAIN_BLOB_BYTES) {
        // Too large to buffer — last chance progressive play under this gesture.
        if (media) {
          const retryOk = await this.tryProgressivePlay(media);
          if (retryOk) {
            this.playbackUnlocked.add(attachment.resourceId);
            return;
          }
        }
        throw new Error(
          'This video is too large to buffer on this device. Check your connection and try again.'
        );
      }

      const mimeHint = attachment.mimeType
        || (attachment.type === 'audio' ? 'audio/mp4' : 'video/mp4');
      const blobUrl = await firstValueFrom(
        this.cryptoApi.fetchPlainMediaObjectUrl(streamUrl, mimeHint)
      );
      if (attachment.dataUrl !== streamUrl) {
        URL.revokeObjectURL(blobUrl);
        return;
      }
      attachment.dataUrl = blobUrl;
      this.playbackUnlocked.add(attachment.resourceId);
      if (media) {
        media.src = blobUrl;
        media.load();
        try {
          await media.play();
        } catch {
          // Gesture may expire on slow downloads; controls remain for a second tap.
        }
      }
    } catch (error) {
      const message = error instanceof Error && error.message.trim()
        ? error.message
        : 'Unable to prepare this video for playback.';
      this.prepareError.set(attachment.resourceId, message);
    } finally {
      this.preparingPlayback.delete(attachment.resourceId);
      this.blobFallbackInflight.delete(attachment.resourceId);
      this.cdr.markForCheck();
    }
  }

  /** Returns true when progressive playback actually starts under the user gesture. */
  private async tryProgressivePlay(media: HTMLMediaElement): Promise<boolean> {
    try {
      const playResult = media.play();
      if (playResult && typeof (playResult as Promise<void>).then === 'function') {
        await playResult;
      }
    } catch {
      return false;
    }

    await new Promise<void>(resolve => window.setTimeout(resolve, 500));
    if (media.error) {
      return false;
    }
    // Playing (possibly still buffering) or time advanced counts as success.
    if (!media.paused || media.currentTime > 0.05) {
      return true;
    }
    return media.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA && !media.paused;
  }
}
