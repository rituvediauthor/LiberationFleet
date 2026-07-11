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
  errorMessage = '';
  showPasswordDialog = false;
  passwordDialogError = '';
  passwordDialogVerifying = false;
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

  onTwoFactorChange(enabled: boolean) {
    this.settings.twoFactorEnabled = enabled;
    this.updateSaveButton();
  }

  onLockSettingsChange(enabled: boolean) {
    this.settings.lockSettingsWithPassword = enabled;
    if (!enabled) {
      this.newSettingsLockPassword = '';
    }
    this.updateSaveButton();
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
      this.performSave(password);
    });
  }

  onPasswordDialogDismissed() {
    this.showPasswordDialog = false;
    this.passwordDialogError = '';
    this.passwordDialogVerifying = false;
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleString();
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
      twoFactorEnabled: this.settings.twoFactorEnabled,
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
