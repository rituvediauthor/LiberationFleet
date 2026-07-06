import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResolvedAttachment } from '../../models/proposal.model';
import { EncryptedContentType } from '../../models/crypto.model';

@Component({
  selector: 'app-proposal-attachment-display',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './proposal-attachment-display.component.html',
  styleUrl: './proposal-attachment-display.component.css'
})
export class ProposalAttachmentDisplayComponent {
  @Input() attachments: ResolvedAttachment[] = [];
  @Input() compact = false;
  @Input() canDelete = false;
  @Input() crewId = 0;
  @Output() attachmentDeleted = new EventEmitter<string>();

  deleteAttachment(attachment: ResolvedAttachment) {
    if (!this.canDelete || !this.crewId) {
      return;
    }

    this.attachmentDeleted.emit(attachment.resourceId);
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
