import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ContentPreferenceService } from '../../services/content-preference.service';
import { ToastService } from '../../components/toast/toast.component';
import {
  ADULT_CONTENT_PREFERENCE_OPTIONS,
  AdultContentPreference
} from '../../models/content-preference.model';

@Component({
  selector: 'app-content-settings',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './content-settings.component.html',
  styleUrl: './content-settings.component.css'
})
export class ContentSettingsComponent implements OnInit {
  readonly options = ADULT_CONTENT_PREFERENCE_OPTIONS;
  selectedPreference: AdultContentPreference = 'Block';
  loading = true;
  saving = false;
  errorMessage = '';
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;

  private router = inject(Router);
  private contentPreferenceService = inject(ContentPreferenceService);
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

  onPreferenceChange(value: string) {
    this.selectedPreference = value as AdultContentPreference;
    this.updateSaveButton();
  }

  onSave() {
    if (this.saving) {
      return;
    }

    this.saving = true;
    this.updateSaveButton();
    this.contentPreferenceService.updatePreferences({
      adultContentPreference: this.selectedPreference
    }).subscribe({
      next: response => {
        this.saving = false;
        if (response.success) {
          this.toastService.success(response.message || 'Content preferences saved');
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
    this.contentPreferenceService.getPreferences().subscribe({
      next: response => {
        if (response.success && response.preferences) {
          this.selectedPreference = response.preferences.adultContentPreference;
        } else {
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
