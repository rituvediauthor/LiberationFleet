import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  CONTENT_REPORT_REASONS,
  ContentReportReason,
  ContentReportTargetType,
  ReportEvidenceSnapshot
} from '../../models/content-report.model';
import { ContentReportService } from '../../services/content-report.service';
import { ToastService } from '../toast/toast.component';

@Component({
  selector: 'app-report-content-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './report-content-dialog.component.html',
  styleUrl: './report-content-dialog.component.css'
})
export class ReportContentDialogComponent {
  @Input() visible = false;
  @Input() targetType: ContentReportTargetType = 'ChatMessage';
  @Input() targetResourceId: number | null = null;
  @Input() targetParentId: number | null = null;
  @Input() targetAuthorUserId: number | null = null;
  @Input() crewId: number | null = null;
  @Input() fleetId: number | null = null;
  @Input() evidenceTitle = '';
  @Input() evidenceText = '';
  @Input() evidenceAuthorUsername = '';
  @Input() evidenceMediaResourceIds: string[] = [];
  @Input() allowBlock = true;

  @Output() dismissed = new EventEmitter<void>();
  @Output() submitted = new EventEmitter<{ reason: ContentReportReason; blocked: boolean }>();

  readonly reasons = CONTENT_REPORT_REASONS;
  reason: ContentReportReason | '' = '';
  note = '';
  alsoBlockAuthor = true;
  involvesMinorConfirmed = false;
  submitting = false;

  constructor(
    private reportService: ContentReportService,
    private toastService: ToastService
  ) {}

  get isCsam(): boolean {
    return this.reason === 'ChildSexualExploitation';
  }

  get canSubmit(): boolean {
    if (!this.reason || this.submitting) {
      return false;
    }
    if (this.isCsam && !this.involvesMinorConfirmed) {
      return false;
    }
    return true;
  }

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('report-dialog-backdrop')) {
      this.close();
    }
  }

  close() {
    if (this.submitting) {
      return;
    }
    this.dismissed.emit();
  }

  submit() {
    if (!this.canSubmit || !this.reason) {
      return;
    }

    this.submitting = true;
    const snapshot: ReportEvidenceSnapshot = {
      text: this.evidenceText,
      title: this.evidenceTitle,
      authorUsername: this.evidenceAuthorUsername,
      mediaResourceIds: this.evidenceMediaResourceIds,
      attestation:
        'I decrypted this content with my authorized key (when encrypted) and believe this snapshot matches the reported item.',
      reportedAtClient: new Date().toISOString()
    };

    this.reportService.create({
      reason: this.reason,
      targetType: this.targetType,
      targetResourceId: this.targetResourceId,
      targetParentId: this.targetParentId,
      targetAuthorUserId: this.targetAuthorUserId,
      crewId: this.crewId,
      fleetId: this.fleetId,
      reporterNote: this.note.trim() || null,
      evidencePlaintextJson: this.reportService.buildEvidenceJson(snapshot),
      alsoBlockAuthor: this.allowBlock && this.alsoBlockAuthor
    }).subscribe({
      next: result => {
        this.submitting = false;
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to submit report');
          return;
        }
        this.toastService.success(result.message || 'Report received');
        this.submitted.emit({
          reason: this.reason as ContentReportReason,
          blocked: this.allowBlock && this.alsoBlockAuthor
        });
        this.resetForm();
      },
      error: err => {
        this.submitting = false;
        this.toastService.error(err?.error?.message || 'Failed to submit report');
      }
    });
  }

  private resetForm() {
    this.reason = '';
    this.note = '';
    this.alsoBlockAuthor = true;
    this.involvesMinorConfirmed = false;
  }
}
