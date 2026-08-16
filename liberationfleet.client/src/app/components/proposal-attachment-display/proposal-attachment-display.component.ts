import { Component, EventEmitter, Input, OnDestroy, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResolvedAttachment } from '../../models/proposal.model';
import { EncryptedContentType } from '../../models/crypto.model';
import { LibraryImageCarouselComponent } from '../library-image-carousel/library-image-carousel.component';
import {
  isPlainMediaStreamUrl,
  isSafeMediaDataUrl
} from '../../utils/media-attachment-allowlist.util';
import { normalizeMediaMime, resolveBlobMime } from '../../utils/media-mime.util';
import { enterMediaDetailZoom, exitMediaDetailZoom } from '../../utils/media-viewport-zoom';

@Component({
  selector: 'app-proposal-attachment-display',
  standalone: true,
  imports: [CommonModule, LibraryImageCarouselComponent],
  templateUrl: './proposal-attachment-display.component.html',
  styleUrl: './proposal-attachment-display.component.css'
})
export class ProposalAttachmentDisplayComponent implements OnDestroy {
  @Input() attachments: ResolvedAttachment[] = [];
  @Input() compact = false;
  @Input() canDelete = false;
  @Input() crewId = 0;
  @Output() attachmentDeleted = new EventEmitter<string>();

  @ViewChild('imageCarousel') imageCarousel?: LibraryImageCarouselComponent;

  /** Tracks native fullscreen so chrome hide/show stays balanced. */
  private videoFullscreenActive = false;

  /** Blob fallbacks when progressive audio streams fail to decode in &lt;audio&gt;. */
  private readonly audioBlobUrls = new Map<string, string>();
  private readonly audioBlobInFlight = new Set<string>();

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
    if (attachment.type === 'audio') {
      const blobUrl = this.audioBlobUrls.get(attachment.resourceId);
      if (blobUrl) {
        return blobUrl;
      }
    }
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
   * Progressive Range URLs work well for video; some audio containers (esp. WebM)
   * fail in &lt;audio&gt; until the full file is buffered as a blob with a correct MIME.
   */
  onAudioError(attachment: ResolvedAttachment, event: Event): void {
    const el = event.target as HTMLAudioElement | null;
    const streamUrl = attachment.dataUrl;
    if (!el || !streamUrl || !isPlainMediaStreamUrl(streamUrl)) {
      return;
    }
    if (this.audioBlobUrls.has(attachment.resourceId) || this.audioBlobInFlight.has(attachment.resourceId)) {
      return;
    }

    this.audioBlobInFlight.add(attachment.resourceId);
    void this.loadAudioBlobFallback(attachment, streamUrl, el)
      .catch(() => undefined)
      .finally(() => this.audioBlobInFlight.delete(attachment.resourceId));
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

  ngOnDestroy(): void {
    for (const url of this.audioBlobUrls.values()) {
      URL.revokeObjectURL(url);
    }
    this.audioBlobUrls.clear();
  }

  private async loadAudioBlobFallback(
    attachment: ResolvedAttachment,
    streamUrl: string,
    el: HTMLAudioElement
  ): Promise<void> {
    const response = await fetch(streamUrl);
    if (!response.ok) {
      return;
    }

    const bytes = new Uint8Array(await response.arrayBuffer());
    if (bytes.byteLength === 0) {
      return;
    }

    const declared = normalizeMediaMime(attachment.mimeType)
      || normalizeMediaMime(response.headers.get('content-type'))
      || 'audio/mpeg';
    const mime = resolveBlobMime(declared, bytes, { preferAudio: true });
    const blobUrl = URL.createObjectURL(new Blob([bytes], { type: mime }));

    const previous = this.audioBlobUrls.get(attachment.resourceId);
    if (previous) {
      URL.revokeObjectURL(previous);
    }
    this.audioBlobUrls.set(attachment.resourceId, blobUrl);

    el.src = blobUrl;
    el.load();
  }
}
