import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResolvedAttachment } from '../../models/proposal.model';

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
}
