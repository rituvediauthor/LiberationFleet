import { Component, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { CrewService } from '../../services/crew.service';
import { attachmentUnmetReason } from '../../utils/attach-permission-copy.util';

@Component({
  selector: 'app-attach-permission-note',
  standalone: true,
  imports: [CommonModule, ConfirmDialogComponent],
  template: `
    <p class="attach-permission-note">
      <span>Requirements to attach files unmet.</span>
      <button
        type="button"
        class="info-btn"
        aria-label="Why you cannot attach files"
        (click)="showReason = true">
        <i class="fa-solid fa-circle-info" aria-hidden="true"></i>
      </button>
    </p>
    <app-confirm-dialog
      [visible]="showReason"
      title="File attachments"
      [message]="reasonText"
      confirmLabel="Close"
      (confirmed)="showReason = false"
      (dismissed)="showReason = false">
    </app-confirm-dialog>
  `,
  styles: [`
    .attach-permission-note {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      margin: 0.25rem 0;
      font-size: 0.8125rem;
      color: var(--text-muted, #6b7280);
    }
    .info-btn {
      border: 0;
      background: transparent;
      color: inherit;
      padding: 0;
      cursor: pointer;
      line-height: 1;
    }
  `]
})
export class AttachPermissionNoteComponent implements OnInit {
  @Input() scope: 'crew' | 'fleet' = 'crew';

  showReason = false;
  reasonText = 'File attachments are disabled, request an override from a moderator.';

  private crewService = inject(CrewService);

  ngOnInit() {
    this.crewService.getMembership().subscribe({
      next: membership => {
        const isFleet = this.scope === 'fleet';
        this.reasonText = attachmentUnmetReason(
          isFleet ? membership.fleetAttachmentBlockReason : membership.crewAttachmentBlockReason,
          {
            scope: this.scope,
            tenureDays: isFleet
              ? membership.fleetAttachmentMinTenureDays ?? 0
              : membership.crewAttachmentMinTenureDays ?? 0,
            minContribution: isFleet
              ? membership.fleetAttachmentMinContribution ?? 0
              : membership.crewAttachmentMinContribution ?? 0
          }
        );
      }
    });
  }
}
