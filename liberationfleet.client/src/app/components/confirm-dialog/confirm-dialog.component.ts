import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, AccessibleDialogDirective],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.css'
})
export class ConfirmDialogComponent {
  @Input() visible = false;
  @Input() title = 'Alert';
  @Input() message = '';
  @Input() confirmLabel = 'Okay';
  @Input() cancelLabel = '';

  @Output() confirmed = new EventEmitter<void>();
  @Output() dismissed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  readonly titleId = `confirm-dialog-title-${Math.random().toString(36).slice(2, 9)}`;
  readonly messageId = `confirm-dialog-message-${Math.random().toString(36).slice(2, 9)}`;

  readonly onEscape = () => {
    if (this.cancelLabel) {
      this.cancelled.emit();
    } else {
      this.dismissed.emit();
    }
  };

  onConfirm() {
    this.confirmed.emit();
  }

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.dismissed.emit();
    }
  }
}
