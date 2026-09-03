import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';

@Component({
  selector: 'app-delete-account-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, AccessibleDialogDirective],
  templateUrl: './delete-account-dialog.component.html',
  styleUrl: './delete-account-dialog.component.css'
})
export class DeleteAccountDialogComponent {
  @Input() visible = false;
  @Input() errorMessage = '';
  @Input() deleting = false;

  @Output() confirmed = new EventEmitter<string>();
  @Output() dismissed = new EventEmitter<void>();

  password = '';

  readonly onEscape = () => {
    if (!this.deleting) {
      this.dismissed.emit();
    }
  };

  onConfirm() {
    if (!this.password.trim() || this.deleting) {
      return;
    }

    this.confirmed.emit(this.password);
  }

  onBackdropClick(event: MouseEvent) {
    if (this.deleting) {
      return;
    }

    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.dismissed.emit();
    }
  }

  reset() {
    this.password = '';
  }
}
