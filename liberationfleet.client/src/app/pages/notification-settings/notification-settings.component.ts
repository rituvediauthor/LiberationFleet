import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { NotificationService } from '../../services/notification.service';
import { ToastService } from '../../components/toast/toast.component';
import { NotificationPreference } from '../../models/notification.model';

@Component({
  selector: 'app-notification-settings',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './notification-settings.component.html',
  styleUrl: './notification-settings.component.css'
})
export class NotificationSettingsComponent implements OnInit {
  preferences: NotificationPreference[] = [];
  loading = true;
  saving = false;
  errorMessage = '';
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;

  private router = inject(Router);
  private notificationService = inject(NotificationService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/profile/preferences'])
    };

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

    this.saving = true;
    this.updateSaveButton();
    this.notificationService.updatePreferences(this.preferences).subscribe({
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
