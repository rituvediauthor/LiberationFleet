import { Component, EventEmitter, Input, Output, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { ResolvedAttachment } from '../../models/proposal.model';
import { EncryptedContentType } from '../../models/crypto.model';
import { LibraryImageCarouselComponent } from '../library-image-carousel/library-image-carousel.component';
import { isPlainMediaStreamUrl, isSafeMediaDataUrl } from '../../utils/media-attachment-allowlist.util';
import { enterMediaDetailZoom, exitMediaDetailZoom } from '../../utils/media-viewport-zoom';
import { CryptoApiService } from '../../services/crypto/crypto-api.service';
import { isAppleMobileBrowser, isNativeIos } from '../../utils/app-platform.util';

@Component({
  selector: 'app-proposal-attachment-display',
  standalone: true,
  imports: [CommonModule, LibraryImageCarouselComponent],
  templateUrl: './proposal-attachment-display.component.html',
  styleUrl: './proposal-attachment-display.component.css'
})
export class ProposalAttachmentDisplayComponent {
  private readonly cryptoApi = inject(CryptoApiService);

  @Input() attachments: ResolvedAttachment[] = [];
  @Input() compact = false;
  @Input() canDelete = false;
  @Input() crewId = 0;
  @Output() attachmentDeleted = new EventEmitter<string>();

  @ViewChild('imageCarousel') imageCarousel?: LibraryImageCarouselComponent;

  /** Tracks native fullscreen so chrome hide/show stays balanced. */
  private videoFullscreenActive = false;

  /** resourceIds currently converting plain-media stream → blob for playback. */
  private readonly blobFallbackInflight = new Set<string>();

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
   * Progressive plain-media often shows a poster + controls ("loaded") while Safari/iOS
   * still cannot start playback (Range/moov). On error or a dead play attempt, swap to a
   * fully downloaded blob: URL which is locally seekable.
   */
  onMediaError(event: Event, attachment: ResolvedAttachment): void {
    const media = event.target as HTMLMediaElement | null;
    void this.tryPlainMediaBlobFallback(attachment, media, /* autoPlay */ false);
  }

  onMediaLoadedMetadata(event: Event, attachment: ResolvedAttachment): void {
    const media = event.target as HTMLMediaElement | null;
    if (!media || !isPlainMediaStreamUrl(attachment.dataUrl || '')) {
      return;
    }

    const durationOk = Number.isFinite(media.duration) && media.duration > 0;
    const seekableOk = media.seekable.length > 0;
    if (durationOk && seekableOk) {
      if (isNativeIos() || isAppleMobileBrowser()) {
        void this.ensureAcceptRangesOrBlob(attachment, media);
      }
      return;
    }

    // Metadata incomplete or non-seekable progressive source — blob so play can work.
    void this.tryPlainMediaBlobFallback(attachment, media, false);
  }

  private async ensureAcceptRangesOrBlob(
    attachment: ResolvedAttachment,
    media: HTMLMediaElement
  ): Promise<void> {
    const streamUrl = attachment.dataUrl;
    if (!streamUrl || !isPlainMediaStreamUrl(streamUrl)) {
      return;
    }
    try {
      const accepts = await firstValueFrom(this.cryptoApi.plainMediaAcceptsRanges(streamUrl));
      if (!accepts) {
        await this.tryPlainMediaBlobFallback(attachment, media, false);
      }
    } catch {
      await this.tryPlainMediaBlobFallback(attachment, media, false);
    }
  }

  onMediaPlay(event: Event, attachment: ResolvedAttachment): void {
    const media = event.target as HTMLMediaElement | null;
    if (!media || !isPlainMediaStreamUrl(attachment.dataUrl || '')) {
      return;
    }

    // Only probe for the iOS "play does nothing" failure mode. Elsewhere, trust
    // progressive Range playback and only fall back on the error event.
    if (!isNativeIos() && !isAppleMobileBrowser()) {
      return;
    }

    window.setTimeout(() => {
      if (!isPlainMediaStreamUrl(attachment.dataUrl || '')) {
        return;
      }
      if (media.error) {
        void this.tryPlainMediaBlobFallback(attachment, media, true);
        return;
      }
      // Successful start: playing (even if still buffering) or time advanced.
      if (!media.paused || media.currentTime > 0.05) {
        return;
      }
      // Still paused at t=0 after a play gesture — classic dead control chrome.
      void this.tryPlainMediaBlobFallback(attachment, media, true);
    }, 600);
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

  private async tryPlainMediaBlobFallback(
    attachment: ResolvedAttachment,
    media: HTMLMediaElement | null,
    autoPlay: boolean
  ): Promise<void> {
    const streamUrl = attachment.dataUrl;
    if (!streamUrl || !isPlainMediaStreamUrl(streamUrl)) {
      return;
    }
    if (!attachment.resourceId || this.blobFallbackInflight.has(attachment.resourceId)) {
      return;
    }

    this.blobFallbackInflight.add(attachment.resourceId);
    try {
      const blobUrl = await firstValueFrom(this.cryptoApi.fetchPlainMediaObjectUrl(streamUrl));
      if (attachment.dataUrl !== streamUrl) {
        URL.revokeObjectURL(blobUrl);
        return;
      }
      attachment.dataUrl = blobUrl;
      if (media) {
        const wasPaused = media.paused;
        media.src = blobUrl;
        media.load();
        if (autoPlay || !wasPaused) {
          try {
            await media.play();
          } catch {
            // User gesture may have expired; controls remain for a second tap.
          }
        }
      }
    } catch {
      // Leave stream URL in place; native error UI (if any) stays.
    } finally {
      this.blobFallbackInflight.delete(attachment.resourceId);
    }
  }
}
