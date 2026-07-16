import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';

@Component({
  selector: 'app-recovery-key-display',
  standalone: true,
  imports: [CommonModule, FormsModule, AccessibleDialogDirective],
  templateUrl: './recovery-key-display.component.html',
  styleUrl: './recovery-key-display.component.css'
})
export class RecoveryKeyDisplayComponent {
  @Input() visible = false;
  @Input() recoveryPhrase = '';
  @Input() title = 'Save your recovery key';
  @Input() confirmLabel = 'I have saved my recovery key';
  @Output() confirmed = new EventEmitter<void>();

  acknowledged = false;
  copied = false;

  get words(): string[] {
    return this.recoveryPhrase.split(' ').filter(Boolean);
  }

  async copyPhrase() {
    if (!this.recoveryPhrase) {
      return;
    }

    try {
      await navigator.clipboard.writeText(this.recoveryPhrase);
      this.copied = true;
      setTimeout(() => {
        this.copied = false;
      }, 2000);
    } catch {
      this.copied = false;
    }
  }

  onConfirm() {
    if (!this.acknowledged) {
      return;
    }
    this.confirmed.emit();
  }
}
