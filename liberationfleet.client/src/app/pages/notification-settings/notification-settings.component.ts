import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { SettingsPasswordDialogComponent } from '../../components/settings-password-dialog/settings-password-dialog.component';
import { NotificationService } from '../../services/notification.service';
import { SettingsLockService } from '../../services/settings-lock.service';
import { ToastService } from '../../components/toast/toast.component';
import { NotificationPreference } from '../../models/notification.model';

@Component({
  selector: 'app-notification-settings',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, SettingsPasswordDialogComponent],
  templateUrl: './notification-settings.component.html',
  styleUrl: './notification-settings.component.css'
})
export class NotificationSettingsComponent implements OnInit {
  preferences: NotificationPreference[] = [];
  loading = true;

  get crewPreferences(): NotificationPreference[] {
    return this.preferences.filter(p => (p.category || 'Crew') === 'Crew');
  }

  get fleetPreferences(): NotificationPreference[] {
    return this.preferences.filter(p => p.category === 'Fleet');
  }
  saving = false;
  errorMessage = '';
  showPasswordDialog = false;
  passwordDialogError = '';
  passwordDialogVerifying = false;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private notificationService = inject(NotificationService);
  private settingsLockService = inject(SettingsLockService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/profile/preferences']);

    this.updateSaveButton();
    this.loadPreferences();
  }

  togglePreference(preference: NotificationPreference) {
    preference.isEnabled = !preference.isEnabled;
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
      this.performSave(password);
    });
  }

  onPasswordDialogDismissed() {
    this.showPasswordDialog = false;
    this.passwordDialogError = '';
    this.passwordDialogVerifying = false;
  }

  private async beginSave() {
    const lockEnabled = await this.settingsLockService.isLockEnabled();
    if (lockEnabled) {
      this.showPasswordDialog = true;
      return;
    }

    this.performSave();
  }

  private performSave(settingsPassword?: string) {
    this.saving = true;
    this.updateSaveButton();
    this.notificationService.updatePreferences(this.preferences, settingsPassword).subscribe({
      next: response => {
        this.saving = false;
        if (response.success) {
          this.toastService.success(response.message || 'Notification preferences saved');
        } else {
          this.toastService.error(response.message || 'Failed to save preferences');
        }
        this.updateSaveButton();
      },
      error: () => {
        this.saving = false;
        this.toastService.error('Failed to save preferences');
        this.updateSaveButton();
      }
    });
  }

  private loadPreferences() {
    this.loading = true;
    this.notificationService.getPreferences().subscribe({
      next: response => {
        this.preferences = response.success ? response.preferences ?? [] : [];
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load preferences';
        }
        this.loading = false;
        this.updateSaveButton();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load preferences';
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
