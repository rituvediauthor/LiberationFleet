import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-kick-reason-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './kick-reason-dialog.component.html',
  styleUrl: './kick-reason-dialog.component.css'
})
export class KickReasonDialogComponent {
  @Input() visible = false;
  @Input() title = 'Kick crewmate?';
  @Input() message = '';
  @Input() confirmLabel = 'Submit kick proposal';

  @Output() confirmed = new EventEmitter<string>();
  @Output() cancelled = new EventEmitter<void>();
  @Output() dismissed = new EventEmitter<void>();

  reason = '';

  onConfirm() {
    const trimmed = this.reason.trim();
    if (!trimmed) {
      return;
    }
    this.confirmed.emit(trimmed);
    this.reason = '';
  }

  onCancel() {
    this.reason = '';
    this.cancelled.emit();
  }

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.reason = '';
      this.dismissed.emit();
    }
  }
}
