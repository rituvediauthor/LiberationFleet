import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-settings-password-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings-password-dialog.component.html',
  styleUrl: './settings-password-dialog.component.css'
})
export class SettingsPasswordDialogComponent {
  @Input() visible = false;
  @Input() errorMessage = '';
  @Input() verifying = false;

  @Output() confirmed = new EventEmitter<string>();
  @Output() dismissed = new EventEmitter<void>();

  password = '';

  onConfirm() {
    if (!this.password.trim() || this.verifying) {
      return;
    }

    this.confirmed.emit(this.password);
  }

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.dismissed.emit();
    }
  }
}
