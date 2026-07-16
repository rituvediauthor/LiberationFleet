import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../toast/toast.component';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';

@Component({
  selector: 'app-crypto-unlock-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, AccessibleDialogDirective],
  templateUrl: './crypto-unlock-dialog.component.html',
  styleUrl: './crypto-unlock-dialog.component.css'
})
export class CryptoUnlockDialogComponent {
  @Input() visible = false;
  @Output() unlocked = new EventEmitter<void>();

  recoveryPhrase = '';
  rememberOnDevice = false;
  unlocking = false;

  private authService = inject(AuthService);
  private toastService = inject(ToastService);

  async onUnlock() {
    if (this.unlocking || !this.recoveryPhrase.trim()) {
      return;
    }

    this.unlocking = true;
    try {
      await this.authService.unlockWithRecoveryPhrase(this.recoveryPhrase, this.rememberOnDevice);
      this.recoveryPhrase = '';
      this.toastService.success('Encryption unlocked');
      this.unlocked.emit();
    } catch (error: unknown) {
      const message = error instanceof Error
        ? error.message
        : 'Invalid recovery key. Check all 12 words and try again.';
      this.toastService.error(message);
    } finally {
      this.unlocking = false;
    }
  }
}
