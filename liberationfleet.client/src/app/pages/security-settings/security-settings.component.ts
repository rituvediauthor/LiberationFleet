import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { SettingsPasswordDialogComponent } from '../../components/settings-password-dialog/settings-password-dialog.component';
import { SecurityService } from '../../services/security.service';
import { DeviceIdentityService } from '../../services/device-identity.service';
import { SavedRecoveryPhraseService } from '../../services/saved-recovery-phrase.service';
import { SettingsLockService } from '../../services/settings-lock.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import { RegisteredDeviceDto, SecuritySettingsDto } from '../../models/security.model';

@Component({
  selector: 'app-security-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent, SettingsPasswordDialogComponent],
  templateUrl: './security-settings.component.html',
  styleUrl: './security-settings.component.css'
})
export class SecuritySettingsComponent implements OnInit {
  settings: SecuritySettingsDto = {
    twoFactorEnabled: false,
    mfaAvailable: true,
    lockSettingsWithPassword: false,
    hasSettingsLockPassword: false
  };
  devices: RegisteredDeviceDto[] = [];
  rememberLogin = true;
  saveDecryptionKey = false;
  newSettingsLockPassword = '';
  currentSettingsLockPassword = '';
  loading = true;
  saving = false;
  deviceActionLoading = false;
  errorMessage = '';
  showPasswordDialog = false;
  passwordDialogError = '';
  passwordDialogVerifying = false;
  mfaBusy = false;
  mfaChallengeToken = '';
  mfaHint = '';
  mfaCode = '';
  mfaPendingEnable: boolean | null = null;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private securityService = inject(SecurityService);
  private deviceIdentity = inject(DeviceIdentityService);
  private savedRecoveryPhrase = inject(SavedRecoveryPhraseService);
  private settingsLockService = inject(SettingsLockService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private pendingSettingsPassword: string | undefined;

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/profile/preferences']);

    this.rememberLogin = this.authService.isRememberLoginEnabled();
    this.saveDecryptionKey = this.savedRecoveryPhrase.isSaveEnabled();
    this.updateSaveButton();
    this.loadData();
  }

  goToAlerts() {
    this.router.navigate(['/app/profile/preferences/security/alerts']);
  }

  goToPasswordUpdate() {
    this.router.navigate(['/app/profile/preferences/security/password']);
  }

  onRememberLoginChange(enabled: boolean) {
    this.rememberLogin = enabled;
    this.updateSaveButton();
  }

  onSaveDecryptionKeyChange(enabled: boolean) {
    this.saveDecryptionKey = enabled;
    this.updateSaveButton();
  }

  onLockSettingsChange(enabled: boolean) {
    this.settings.lockSettingsWithPassword = enabled;
    if (!enabled) {
      this.newSettingsLockPassword = '';
    }
    this.updateSaveButton();
  }

  onMfaToggle(enabled: boolean) {
    if (this.mfaBusy || this.mfaChallengeToken) {
      return;
    }

    if (enabled === this.settings.twoFactorEnabled) {
      return;
    }

    void this.beginMfaChange(enabled);
  }

  confirmMfaChange() {
    if (this.mfaBusy || !this.mfaChallengeToken || this.mfaPendingEnable === null) {
      return;
    }

    const code = this.mfaCode.trim();
    if (!/^\d{6}$/.test(code)) {
      this.toastService.error('Enter the 6-digit code from your email.');
      return;
    }

    this.mfaBusy = true;
    const request = {
      mfaChallengeToken: this.mfaChallengeToken,
      code,
      settingsPassword: this.pendingSettingsPassword
    };
    const call$ = this.mfaPendingEnable
      ? this.securityService.confirmEnableEmailMfa(request)
      : this.securityService.confirmDisableEmailMfa(request);

    call$.subscribe({
      next: response => {
        this.mfaBusy = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Verification failed');
          return;
        }

        if (response.settings) {
          this.settings = response.settings;
        } else {
          this.settings.twoFactorEnabled = !!this.mfaPendingEnable;
        }

        this.cancelMfaChallenge();
        this.toastService.success(response.message || 'MFA updated');
      },
      error: () => {
        this.mfaBusy = false;
        this.toastService.error('Verification failed');
      }
    });
  }

  resendMfaSettingsCode() {
    if (this.mfaBusy || !this.mfaChallengeToken) {
      return;
    }

    this.mfaBusy = true;
    this.securityService.resendEmailMfa({ mfaChallengeToken: this.mfaChallengeToken }).subscribe({
      next: response => {
        this.mfaBusy = false;
        if (response.mfaChallengeToken) {
          this.mfaChallengeToken = response.mfaChallengeToken;
        }
        if (!response.success) {
          this.toastService.error(response.message || 'Could not resend code');
          return;
        }
        this.mfaHint = response.message || this.mfaHint;
        this.toastService.success(response.message || 'Code resent');
      },
      error: () => {
        this.mfaBusy = false;
        this.toastService.error('Could not resend code');
      }
    });
  }

  cancelMfaChallenge() {
    this.mfaChallengeToken = '';
    this.mfaHint = '';
    this.mfaCode = '';
    this.mfaPendingEnable = null;
  }

  private async beginMfaChange(enable: boolean) {
    const lockEnabled = await this.settingsLockService.isLockEnabled();
    let settingsPassword = this.pendingSettingsPassword;
    if (lockEnabled && !settingsPassword) {
      this.showPasswordDialog = true;
      // Re-entry after password dialog is not wired for MFA; ask user to unlock via Save flow first is awkward.
      // Prompt inline: use verify dialog then continue.
      this.mfaPendingEnable = enable;
      return;
    }

    this.startMfaChallenge(enable, settingsPassword);
  }

  private startMfaChallenge(enable: boolean, settingsPassword?: string) {
    this.mfaBusy = true;
    this.mfaPendingEnable = enable;
    const body = { settingsPassword };
    const call$ = enable
      ? this.securityService.beginEnableEmailMfa(body)
      : this.securityService.beginDisableEmailMfa(body);

    call$.subscribe({
      next: response => {
        this.mfaBusy = false;
        if (!response.success || !response.mfaChallengeToken) {
          this.mfaPendingEnable = null;
          this.toastService.error(response.message || 'Could not start MFA change');
          return;
        }

        this.mfaChallengeToken = response.mfaChallengeToken;
        this.mfaHint = response.message || 'Enter the code we emailed you.';
        this.mfaCode = '';
        this.toastService.success(this.mfaHint);
      },
      error: () => {
        this.mfaBusy = false;
        this.mfaPendingEnable = null;
        this.toastService.error('Could not start MFA change');
      }
    });
  }

  onSave() {
    if (this.saving) {
      return;
    }

    void this.beginSave();
  }

  onPasswordDialogConfirmed(password: string) {
    this.passwordDialogVerifying = true;
    this.passwordDialogError = '';

    void this.settingsLockService.verifyPassword(password).then(result => {
      this.passwordDialogVerifying = false;
      if (!result.success) {
        this.passwordDialogError = result.message || 'Incorrect settings password.';
        return;
      }

      this.showPasswordDialog = false;
      this.pendingSettingsPassword = password;
      if (this.mfaPendingEnable !== null && !this.mfaChallengeToken) {
        this.startMfaChallenge(this.mfaPendingEnable, password);
        return;
      }

      this.performSave(password);
    });
  }

  onPasswordDialogDismissed() {
    this.showPasswordDialog = false;
    this.passwordDialogError = '';
    this.passwordDialogVerifying = false;
    if (!this.mfaChallengeToken) {
      this.mfaPendingEnable = null;
    }
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleString();
  }

  blockDevice(device: RegisteredDeviceDto) {
    if (this.deviceActionLoading || device.isCurrent) {
      return;
    }
    this.deviceActionLoading = true;
    this.securityService.blockDevice(device.id).subscribe({
      next: response => {
        this.deviceActionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to block device');
          return;
        }
        device.isBlocked = true;
        device.isTrusted = false;
        this.toastService.success(response.message || 'Device blocked');
      },
      error: () => {
        this.deviceActionLoading = false;
        this.toastService.error('Failed to block device');
      }
    });
  }

  unblockDevice(device: RegisteredDeviceDto) {
    if (this.deviceActionLoading || device.isCurrent) {
      return;
    }
    this.deviceActionLoading = true;
    this.securityService.unblockDevice(device.id).subscribe({
      next: response => {
        this.deviceActionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to unblock device');
          return;
        }
        device.isBlocked = false;
        this.toastService.success(response.message || 'Device unblocked');
      },
      error: () => {
        this.deviceActionLoading = false;
        this.toastService.error('Failed to unblock device');
      }
    });
  }

  private async beginSave() {
    const lockEnabled = await this.settingsLockService.isLockEnabled();
    if (lockEnabled && !this.pendingSettingsPassword) {
      this.showPasswordDialog = true;
      return;
    }

    this.performSave(this.pendingSettingsPassword);
  }

  private performSave(settingsPassword?: string) {
    if (this.settings.lockSettingsWithPassword && !this.settings.hasSettingsLockPassword && !this.newSettingsLockPassword.trim()) {
      this.toastService.error('Set a settings lock password before enabling lock.');
      return;
    }

    this.saving = true;
    this.updateSaveButton();

    this.authService.setRememberLoginEnabled(this.rememberLogin);
    this.savedRecoveryPhrase.setSaveEnabled(this.saveDecryptionKey);
    if (this.saveDecryptionKey) {
      const userId = this.authService.getCurrentUserId();
      const sessionPhrase = this.authService.getSessionRecoveryPhrase();
      if (userId && sessionPhrase) {
        this.savedRecoveryPhrase.savePhrase(userId, sessionPhrase);
      }
    }

    this.securityService.updateSettings({
      lockSettingsWithPassword: this.settings.lockSettingsWithPassword,
      newSettingsLockPassword: this.newSettingsLockPassword.trim() || undefined,
      currentSettingsLockPassword: this.currentSettingsLockPassword.trim() || undefined,
      settingsPassword
    }).subscribe({
      next: response => {
        this.saving = false;
        this.pendingSettingsPassword = undefined;
        if (response.success && response.settings) {
          this.settings = response.settings;
          this.newSettingsLockPassword = '';
          this.currentSettingsLockPassword = '';
          this.settingsLockService.refreshLockState();
          this.toastService.success(response.message || 'Security settings saved');
        } else {
          this.toastService.error(response.message || 'Failed to save security settings');
        }
        this.updateSaveButton();
      },
      error: () => {
        this.saving = false;
        this.pendingSettingsPassword = undefined;
        this.toastService.error('Failed to save security settings');
        this.updateSaveButton();
      }
    });
  }

  private loadData() {
    this.loading = true;
    const currentDeviceId = this.deviceIdentity.getDeviceId();

    this.securityService.getSettings().subscribe({
      next: settingsResponse => {
        if (settingsResponse.success && settingsResponse.settings) {
          this.settings = settingsResponse.settings;
        } else {
          this.errorMessage = settingsResponse.message || 'Failed to load security settings';
        }

        this.securityService.getDevices(currentDeviceId).subscribe({
          next: devicesResponse => {
            this.devices = devicesResponse.success ? devicesResponse.devices : [];
            this.loading = false;
            this.updateSaveButton();
          },
          error: () => {
            this.loading = false;
            this.errorMessage = this.errorMessage || 'Failed to load registered devices';
            this.updateSaveButton();
          }
        });
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load security settings';
        this.updateSaveButton();
      }
    });
  }

  private updateSaveButton() {
    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled: this.loading || this.saving,
      onClick: () => this.onSave()
    };
  }
}
