import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../toast/toast.component';
import { BACKUP_WRAP_LEGACY_PASSWORD } from '../../services/crypto/recovery-key.util';

@Component({
  selector: 'app-crypto-unlock-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './crypto-unlock-dialog.component.html',
  styleUrl: './crypto-unlock-dialog.component.css'
})
export class CryptoUnlockDialogComponent implements OnChanges {
  @Input() visible = false;
  @Output() unlocked = new EventEmitter<void>();

  recoveryPhrase = '';
  legacyPassword = '';
  rememberOnDevice = false;
  unlocking = false;
  legacyMode = false;
  backupWrapVersion: number | null = null;

  private authService = inject(AuthService);
  private toastService = inject(ToastService);

  ngOnChanges(changes: SimpleChanges) {
    if (changes['visible']?.currentValue === true) {
      void this.loadBackupInfo();
    }
  }

  async onUnlock() {
    if (this.unlocking) {
      return;
    }

    this.unlocking = true;
    try {
      if (this.legacyMode) {
        if (!this.legacyPassword) {
          return;
        }
        await this.authService.unlockWithLegacyPassword(this.legacyPassword, this.rememberOnDevice);
      } else {
        if (!this.recoveryPhrase.trim()) {
          return;
        }
        await this.authService.unlockWithRecoveryPhrase(this.recoveryPhrase, this.rememberOnDevice);
      }

      this.recoveryPhrase = '';
      this.legacyPassword = '';
      this.unlocked.emit();
    } catch {
      this.toastService.error(
        this.legacyMode
          ? 'Incorrect login password for legacy encryption backup.'
          : 'Invalid recovery key. Check all 12 words and try again.'
      );
    } finally {
      this.unlocking = false;
    }
  }

  toggleLegacyMode() {
    this.legacyMode = !this.legacyMode;
  }

  private async loadBackupInfo() {
    this.backupWrapVersion = await this.authService.getBackupWrapVersion();
    this.legacyMode = this.backupWrapVersion === BACKUP_WRAP_LEGACY_PASSWORD;
  }
}
