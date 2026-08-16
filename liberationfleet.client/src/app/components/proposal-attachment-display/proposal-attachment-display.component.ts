import { Component, EventEmitter, Input, Output, ViewChild, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { ResolvedAttachment } from '../../models/proposal.model';
import { EncryptedContentType } from '../../models/crypto.model';
import { LibraryImageCarouselComponent } from '../library-image-carousel/library-image-carousel.component';
import { isPlainMediaStreamUrl, isSafeMediaDataUrl } from '../../utils/media-attachment-allowlist.util';
import { enterMediaDetailZoom, exitMediaDetailZoom } from '../../utils/media-viewport-zoom';
import { CryptoApiService } from '../../services/crypto/crypto-api.service';

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

  /** resourceIds that already have a playable blob: URL. */
  private readonly blobReady = new Set<string>();

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
      && !this.blobReady.has(attachment.resourceId)
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
   * Progressive plain-media mounts the player quickly (poster + shell) but often
   * will not start on iOS/Safari. A dedicated Play button starts an authenticated
   * blob download under the user gesture, then swaps src and plays.
   */
  startPlainPlayback(attachment: ResolvedAttachment, media: HTMLMediaElement): void {
    void this.convertPlainStreamToBlobAndPlay(attachment, media);
  }

  onMediaError(event: Event, attachment: ResolvedAttachment): void {
    if (!this.isPlainStream(attachment)) {
      return;
    }
    const media = event.target as HTMLMediaElement | null;
    void this.convertPlainStreamToBlobAndPlay(attachment, media, /* autoPlay */ false);
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

  private async convertPlainStreamToBlobAndPlay(
    attachment: ResolvedAttachment,
    media: HTMLMediaElement | null,
    autoPlay = true
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
      media?.pause();
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
      this.blobReady.add(attachment.resourceId);
      if (media) {
        media.src = blobUrl;
        media.load();
        if (autoPlay) {
          try {
            await media.play();
          } catch {
            // Gesture may be gone after a long download; native controls work on next tap.
          }
        }
      }
    } catch {
      this.prepareError.set(attachment.resourceId, 'Unable to prepare this video for playback.');
    } finally {
      this.preparingPlayback.delete(attachment.resourceId);
      this.blobFallbackInflight.delete(attachment.resourceId);
      this.cdr.markForCheck();
    }
  }
}