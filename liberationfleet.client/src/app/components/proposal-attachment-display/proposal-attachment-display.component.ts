import { Component, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResolvedAttachment } from '../../models/proposal.model';
import { EncryptedContentType } from '../../models/crypto.model';
import { LibraryImageCarouselComponent } from '../library-image-carousel/library-image-carousel.component';
import { isSafeMediaDataUrl } from '../../utils/media-attachment-allowlist.util';

@Component({
  selector: 'app-proposal-attachment-display',
  standalone: true,
  imports: [CommonModule, LibraryImageCarouselComponent],
  templateUrl: './proposal-attachment-display.component.html',
  styleUrl: './proposal-attachment-display.component.css'
})
export class ProposalAttachmentDisplayComponent {
  @Input() attachments: ResolvedAttachment[] = [];
  @Input() compact = false;
  @Input() canDelete = false;
  @Input() crewId = 0;
  @Output() attachmentDeleted = new EventEmitter<string>();

  @ViewChild('imageCarousel') imageCarousel?: LibraryImageCarouselComponent;

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
}
