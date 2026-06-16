import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.css'
})
export class ConfirmDialogComponent {
  @Input() visible = false;
  @Input() title = 'Alert';
  @Input() message = '';
  @Input() confirmLabel = 'Okay';

  @Output() confirmed = new EventEmitter<void>();
  @Output() dismissed = new EventEmitter<void>();

  onConfirm() {
    this.confirmed.emit();
  }

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.dismissed.emit();
    }
  }
}
