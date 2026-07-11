import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { RecoveryKeyDisplayComponent } from '../../components/recovery-key-display/recovery-key-display.component';
import { PaymentPlatformEditorComponent } from '../../components/payment-platform-editor/payment-platform-editor.component';
import { AuthService } from '../../services/auth.service';
import { ProfileService } from '../../services/profile.service';
import { ToastService } from '../../components/toast/toast.component';
import { CrewService } from '../../services/crew.service';
import { CryptoSessionService } from '../../services/crypto/crypto-session.service';
import { CUSTOM_PLATFORM_OPTION_ID, PaymentPlatformAccount, PaymentPlatformSnapshot, UserProfile } from '../../models/profile.model';
import { PaymentPlatformOption } from '../../models/gift.model';
import { generateRecoveryPhrase } from '../../services/crypto/recovery-key.util';
import { formValuesChanged, valuesEqual } from '../../utils/save-button.util';
import { mergePaymentPlatformOptions } from '../../utils/payment-platform-options.util';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, PageLayoutComponent, RecoveryKeyDisplayComponent, PaymentPlatformEditorComponent],
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
  encryptionUnlocked = false;
  showRecoveryKeyModal = false;
  pendingRecoveryPhrase = '';
  rotatingRecoveryKey = false;
  private initialFormValues: unknown = null;
  private initialPaymentPlatforms: PaymentPlatformSnapshot[] = [];

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private authService = inject(AuthService);
  private profileService = inject(ProfileService);
  private crewService = inject(CrewService);
  private cryptoSession = inject(CryptoSessionService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.loadPlatformOptions();

    this.backButton = this.navigation.createBackButton(['/app/profile']);

    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled: true,
      onClick: () => this.onSave()
    };

    this.loadProfile();
    void this.loadEncryptionStatus();
    this.cryptoSession.unlocked$.subscribe(unlocked => {
      this.encryptionUnlocked = unlocked;
    });
  }

  async startRecoveryKeyRotation() {
    if (!this.encryptionUnlocked || this.rotatingRecoveryKey) {
      this.toastService.error('Unlock encryption before changing your recovery key.');
      return;
    }

    this.rotatingRecoveryKey = true;
    try {
      this.pendingRecoveryPhrase = generateRecoveryPhrase();
      this.showRecoveryKeyModal = true;
    } catch {
      this.toastService.error('Failed to generate a new recovery key.');
    } finally {
      this.rotatingRecoveryKey = false;
    }
  }

  async onRecoveryKeyRotationConfirmed() {
    if (!this.pendingRecoveryPhrase) {
      return;
    }

    try {
      await this.authService.rotateRecoveryPhrase(this.pendingRecoveryPhrase);
      await this.authService.unlockWithRecoveryPhrase(this.pendingRecoveryPhrase, true);
      this.pendingRecoveryPhrase = '';
      this.showRecoveryKeyModal = false;
      this.toastService.success('Recovery key updated. Store the new key safely; the old one no longer works.');
    } catch {
      this.toastService.error('Failed to update recovery key.');
    }
  }

  get paymentPlatforms(): PaymentPlatformAccount[] {
    return this.profile?.paymentPlatforms ?? [];
  }

  get stats() {
    return this.profile?.stats;
  }

  get roles(): string[] {
    return this.profile?.roles ?? [];
  }

  onNominate() {
    this.toastService.error('Nominate is not available yet.');
  }

  onDemote() {
    this.toastService.error('Demote is not available yet.');
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
      peopleRepresentedCount: Number(v.peopleRepresentedCount),
      disabilityLevel: Number(v.disabilityLevel),
      needsSurvivalAid: !!v.needsSurvivalAid,
      paymentPlatforms: this.getPaymentPlatformsForSave()
    };

    this.profileService.updateProfile(payload).subscribe({
      next: (result) => {
        if (result.success && result.profile) {
          this.profile = result.profile;
          this.loadPlatformOptions();
          this.form.patchValue({
            username: result.profile.username,
            email: result.profile.email,
            inNeedOfAid: result.profile.inNeedOfAid,
            emergencyLevel: result.profile.emergencyLevel,
            peopleRepresentedCount: result.profile.peopleRepresentedCount,
            disabilityLevel: result.profile.disabilityLevel,
            needsSurvivalAid: result.profile.needsSurvivalAid
          });
          this.captureInitialState();
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

  setPreferredPlatform(accountId: number) {
    if (!this.profile) return;
    this.profileService.setPreferredPlatform(this.profile, accountId);
    this.updateSaveButton();
  }

  private async loadEncryptionStatus() {
    this.encryptionUnlocked = this.cryptoSession.isUnlocked();
  }

  private loadProfile() {
    this.isLoading = true;
    this.loadError = '';

    this.profileService.getProfile().subscribe({
      next: (profile) => {
        this.profile = profile;
        this.syncPlatformOptions();
        this.authService.updateCurrentUser({
          id: profile.id,
          username: profile.username,
          email: profile.email
        });
        this.buildForm(profile);
        this.captureInitialState();
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

  private loadPlatformOptions() {
    this.crewService.getPaymentPlatforms(false).subscribe({
      next: platforms => {
        this.platformOptions = mergePaymentPlatformOptions(platforms, this.profile?.paymentPlatforms ?? []);
      },
      error: () => this.toastService.error('Failed to load payment platforms')
    });
  }

  private syncPlatformOptions() {
    this.platformOptions = mergePaymentPlatformOptions(this.platformOptions, this.profile?.paymentPlatforms ?? []);
  }

  private buildForm(profile: UserProfile) {
    this.form = this.fb.group({
      username: [profile.username, [Validators.required, Validators.maxLength(256)]],
      email: [profile.email, [Validators.required, Validators.email]],
      inNeedOfAid: [profile.inNeedOfAid],
      emergencyLevel: [profile.emergencyLevel, [Validators.min(0), Validators.max(3)]],
      peopleRepresentedCount: [profile.peopleRepresentedCount ?? 1, [Validators.min(0), Validators.max(99)]],
      disabilityLevel: [profile.disabilityLevel ?? 0, [Validators.min(0), Validators.max(3)]],
      needsSurvivalAid: [profile.needsSurvivalAid]
    });

    this.form.statusChanges.subscribe(() => this.updateSaveButton());
    this.updateSaveButton();
  }

  private updateSaveButton() {
    const paymentPlatformError = this.getPaymentPlatformValidationError();
    const disabled = !this.form
      || this.isLoading
      || this.isSaving
      || this.form.invalid
      || !!paymentPlatformError
      || !this.hasProfileChanges();

    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled,
      onClick: () => this.onSave()
    };
  }

  private captureInitialState() {
    if (!this.form || !this.profile) {
      return;
    }

    this.initialFormValues = this.form.getRawValue();
    this.initialPaymentPlatforms = this.serializePlatforms(this.profile.paymentPlatforms);
  }

  private hasProfileChanges(): boolean {
    if (!this.form || !this.profile || this.initialFormValues === null) {
      return false;
    }

    const formChanged = formValuesChanged(this.form, this.initialFormValues);
    const platformsChanged = !valuesEqual(
      this.serializePlatforms(this.profile.paymentPlatforms),
      this.initialPaymentPlatforms
    );
    return formChanged || platformsChanged;
  }

  private serializePlatforms(platforms: PaymentPlatformAccount[]): PaymentPlatformSnapshot[] {
    return platforms
      .map(p => ({
        id: p.id,
        platformId: p.platformId,
        customPlatformName: p.customPlatformName?.trim() ?? '',
        handle: p.handle.trim(),
        isPreferred: !!p.isPreferred
      }))
      .sort((a, b) => a.id - b.id);
  }

  private getPaymentPlatformsForSave() {
    return this.paymentPlatforms
      .filter(p => p.handle.trim() && (p.platformId > 0 || p.customPlatformName?.trim()))
      .map(p => ({
        id: p.id > 0 ? p.id : 0,
        platformId: p.platformId === CUSTOM_PLATFORM_OPTION_ID ? 0 : p.platformId,
        customPlatformName: p.platformId === CUSTOM_PLATFORM_OPTION_ID ? p.customPlatformName?.trim() : undefined,
        platform: p.platformId === CUSTOM_PLATFORM_OPTION_ID
          ? (p.customPlatformName?.trim() ?? '')
          : (this.platformOptions.find(option => option.id === p.platformId)?.name ?? p.platform),
        handle: p.handle.trim(),
        isPreferred: !!p.isPreferred
      }));
  }

  private getPaymentPlatformValidationError(): string | null {
    const hasPartial = this.paymentPlatforms.some(p => {
      const hasHandle = !!p.handle.trim();
      const hasPlatform = p.platformId > 0 || !!p.customPlatformName?.trim();
      return hasPlatform !== hasHandle;
    });

    if (hasPartial) {
      return 'Each payment platform needs both a platform and a handle.';
    }

    const hasInvalidCustom = this.paymentPlatforms.some(
      p => p.platformId === CUSTOM_PLATFORM_OPTION_ID && p.handle.trim() && !p.customPlatformName?.trim()
    );
    if (hasInvalidCustom) {
      return 'Custom platforms need a platform name.';
    }

    return null;
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