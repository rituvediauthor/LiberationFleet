import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { AuthService } from '../../services/auth.service';
import { ProfileService } from '../../services/profile.service';
import { ToastService } from '../../components/toast/toast.component';
import { PaymentPlatformAccount, UserProfile } from '../../models/profile.model';
import { GiftService } from '../../services/gift.service';
import { PaymentPlatformOption } from '../../models/gift.model';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css'
})
export class ProfileComponent implements OnInit {
  form!: FormGroup;
  profile: UserProfile | null = null;
  platformOptions: PaymentPlatformOption[] = [];
  isLoading = true;
  isSaving = false;
  loadError = '';
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private authService = inject(AuthService);
  private profileService = inject(ProfileService);
  private giftService = inject(GiftService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.giftService.getPaymentPlatforms().subscribe({
      next: platforms => this.platformOptions = platforms,
      error: () => this.toastService.error('Failed to load payment platforms')
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/profile'])
    };

    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled: true,
      onClick: () => this.onSave()
    };

    this.loadProfile();
  }

  get paymentPlatforms(): PaymentPlatformAccount[] {
    return this.profile?.paymentPlatforms ?? [];
  }

  get stats() {
    return this.profile?.stats;
  }

  addPaymentPlatform() {
    if (!this.profile) {
      return;
    }
    this.profileService.addPaymentPlatform(this.profile);
    this.updateSaveButton();
  }

  removePaymentPlatform(accountId: number) {
    if (!this.profile) {
      return;
    }
    this.profileService.removePaymentPlatform(this.profile, accountId);
    this.updateSaveButton();
  }

  onLogout() {
    this.authService.logout();
    this.router.navigate(['/sign-in']);
  }

  onSave() {
    if (!this.profile || !this.form || this.form.invalid || this.isSaving) {
      return;
    }

    const paymentPlatformError = this.getPaymentPlatformValidationError();
    if (paymentPlatformError) {
      this.toastService.error(paymentPlatformError);
      return;
    }

    this.isSaving = true;
    this.updateSaveButton();

    const v = this.form.getRawValue();
    const payload = {
      username: String(v.username).trim(),
      email: String(v.email).trim(),
      inNeedOfAid: !!v.inNeedOfAid,
      emergencyLevel: Number(v.emergencyLevel),
      needsSurvivalAid: !!v.needsSurvivalAid,
      paymentPlatforms: this.getPaymentPlatformsForSave()
    };

    this.profileService.updateProfile(payload).subscribe({
      next: (result) => {
        if (result.success && result.profile) {
          this.profile = result.profile;
          this.form.patchValue({
            username: result.profile.username,
            email: result.profile.email,
            inNeedOfAid: result.profile.inNeedOfAid,
            emergencyLevel: result.profile.emergencyLevel,
            needsSurvivalAid: result.profile.needsSurvivalAid
          });
          this.authService.updateCurrentUser({
            id: result.profile.id,
            username: result.profile.username,
            email: result.profile.email
          });
          this.toastService.success(result.message);
        } else {
          this.toastService.error(result.message || 'Failed to save profile');
        }
        this.isSaving = false;
        this.updateSaveButton();
      },
      error: (error) => {
        this.toastService.error(this.extractErrorMessage(error));
        this.isSaving = false;
        this.updateSaveButton();
      }
    });
  }

  onPaymentPlatformChange() {
    this.updateSaveButton();
  }

  private loadProfile() {
    this.isLoading = true;
    this.loadError = '';

    this.profileService.getProfile().subscribe({
      next: (profile) => {
        this.profile = profile;
        this.authService.updateCurrentUser({
          id: profile.id,
          username: profile.username,
          email: profile.email
        });
        this.buildForm(profile);
        this.isLoading = false;
        this.updateSaveButton();
      },
      error: () => {
        this.loadError = 'Unable to load profile. Please try again.';
        this.isLoading = false;
        this.updateSaveButton();
      }
    });
  }

  private buildForm(profile: UserProfile) {
    this.form = this.fb.group({
      username: [profile.username, [Validators.required, Validators.maxLength(256)]],
      email: [profile.email, [Validators.required, Validators.email]],
      inNeedOfAid: [profile.inNeedOfAid],
      emergencyLevel: [profile.emergencyLevel, [Validators.min(0), Validators.max(3)]],
      needsSurvivalAid: [profile.needsSurvivalAid]
    });

    this.form.statusChanges.subscribe(() => this.updateSaveButton());
    this.updateSaveButton();
  }

  private updateSaveButton() {
    const disabled = !this.form || this.form.invalid || this.isLoading || this.isSaving;
    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled,
      onClick: () => this.onSave()
    };
  }

  private getPaymentPlatformsForSave() {
    return this.paymentPlatforms
      .filter(p => p.platformId > 0 && p.handle.trim())
      .map(p => ({
        id: p.id > 0 ? p.id : 0,
        platformId: p.platformId,
        platform: this.platformOptions.find(option => option.id === p.platformId)?.name ?? p.platform,
        handle: p.handle.trim()
      }));
  }

  private getPaymentPlatformValidationError(): string | null {
    const hasPartial = this.paymentPlatforms.some(p => {
      const hasPlatform = p.platformId > 0;
      const hasHandle = !!p.handle.trim();
      return hasPlatform !== hasHandle;
    });

    return hasPartial ? 'Each payment platform needs both a platform and a handle.' : null;
  }

  private extractErrorMessage(error: { error?: { message?: string; errors?: Record<string, string[]> } }): string {
    const validationErrors = error.error?.errors;
    if (validationErrors) {
      if (validationErrors['command']?.[0]) {
        return 'Invalid profile data sent to the server. Check payment platforms and try again.';
      }

      const firstError = Object.values(validationErrors).flat()[0];
      if (firstError) {
        return firstError;
      }
    }

    return error.error?.message || 'Failed to save profile';
  }
}