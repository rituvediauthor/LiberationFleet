import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { RecoveryKeyDisplayComponent } from '../../components/recovery-key-display/recovery-key-display.component';
import { DeleteAccountDialogComponent } from '../../components/delete-account-dialog/delete-account-dialog.component';
import { PaymentPlatformEditorComponent } from '../../components/payment-platform-editor/payment-platform-editor.component';
import { IdentityGroupsEditorComponent } from '../../components/identity-groups-editor/identity-groups-editor.component';
import { CharCounterComponent } from '../../components/char-counter/char-counter.component';
import { ProposalAttachmentPickerComponent } from '../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { CrewmateIdCardComponent } from '../../components/crewmate-id-card/crewmate-id-card.component';
import { CollapsibleSectionComponent } from '../../components/collapsible-section/collapsible-section.component';
import { PriorityScoreAlgorithmsComponent } from '../../components/priority-score-algorithms/priority-score-algorithms.component';
import { CryptoUnlockDialogComponent } from '../../components/crypto-unlock-dialog/crypto-unlock-dialog.component';
import { CountrySelectComponent } from '../../components/country-select/country-select.component';
import { AuthService } from '../../services/auth.service';
import { ProfileService } from '../../services/profile.service';
import { SecurityService } from '../../services/security.service';
import { ToastService } from '../../components/toast/toast.component';
import { CrewService } from '../../services/crew.service';
import { FleetService } from '../../services/fleet.service';
import { CryptoSessionService } from '../../services/crypto/crypto-session.service';
import { ProposalCryptoService } from '../../services/crypto/proposal-crypto.service';
import { EncryptedImageCacheService } from '../../services/encrypted-image-cache.service';
import { ProfileLocationService } from '../../services/profile-location.service';
import { CUSTOM_PLATFORM_OPTION_ID, EncryptedLocation, PaymentPlatformAccount, PaymentPlatformSnapshot, UserProfile } from '../../models/profile.model';
import { PaymentPlatformOption } from '../../models/gift.model';
import { PendingAttachment } from '../../models/proposal.model';
import { generateRecoveryPhrase } from '../../services/crypto/recovery-key.util';
import { navigateToDonate } from '../../utils/donation-nav.util';
import { formValuesChanged, valuesEqual } from '../../utils/save-button.util';
import { mergePaymentPlatformOptions } from '../../utils/payment-platform-options.util';
import { isControlInvalidForA11y } from '../../utils/a11y-form.util';
import { usernameValidators, USERNAME_MAX_LENGTH } from '../../utils/username.util';
import { normalizeIdentityGroups } from '../../utils/identity-groups.util';
import { pendingAttachmentsAllowSubmit } from '../../utils/pending-attachment.util';
import { isValidPostalCode, normalizePostalCode } from '../../constants/countries';

function optionalPostalCodeValidator(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '').trim();
  if (!value) {
    return null;
  }
  return isValidPostalCode(value) ? null : { postalCode: true };
}

function profileLocationValidator(group: AbstractControl): ValidationErrors | null {
  const zip = String(group.get('zipCode')?.value ?? '').trim();
  const country = group.get('countryCode')?.value;
  if (zip && !country) {
    return { countryRequiredForPostal: true };
  }
  return null;
}

function passwordStrengthValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (!value) {
    return null;
  }

  const hasUpperCase = /[A-Z]/.test(value);
  const hasLowerCase = /[a-z]/.test(value);
  const hasNumeric = /[0-9]/.test(value);
  const hasSpecialChar = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(value);
  const isLengthValid = value.length >= 8;
  const passwordValid = hasUpperCase && hasLowerCase && hasNumeric && hasSpecialChar && isLengthValid;
  return passwordValid ? null : { passwordStrength: true };
}

function optionalPasswordChangeValidator(control: AbstractControl): ValidationErrors | null {
  const current = String(control.get('currentPassword')?.value ?? '');
  const next = String(control.get('newPassword')?.value ?? '');
  const confirm = String(control.get('confirmPassword')?.value ?? '');

  if (!current && !next && !confirm) {
    return null;
  }

  if (!current || !next || !confirm) {
    return { passwordIncomplete: true };
  }

  return next === confirm ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    PageLayoutComponent,
    RecoveryKeyDisplayComponent,
    DeleteAccountDialogComponent,
    PaymentPlatformEditorComponent,
    IdentityGroupsEditorComponent,
    ProposalAttachmentPickerComponent,
    CharCounterComponent,
    CrewmateIdCardComponent,
    CollapsibleSectionComponent,
    PriorityScoreAlgorithmsComponent,
    CryptoUnlockDialogComponent,
    CountrySelectComponent
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css'
})
export class ProfileComponent implements OnInit {
  form!: FormGroup;
  passwordForm!: FormGroup;
  readonly usernameMaxLength = USERNAME_MAX_LENGTH;
  profile: UserProfile | null = null;
  platformOptions: PaymentPlatformOption[] = [];
  readonly currentYear = new Date().getFullYear();
  isLoading = true;
  isSaving = false;
  loadError = '';
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  encryptionUnlocked = false;
  encryptionToggleBusy = false;
  showUnlockDialog = false;
  showRecoveryKeyModal = false;
  pendingRecoveryPhrase = '';
  rotatingRecoveryKey = false;
  showDeleteAccountDialog = false;
  deletingAccount = false;
  deleteAccountError = '';
  crewId = 0;
  crewName: string | null = null;
  fleetName: string | null = null;
  canToggleInNeedOff = true;
  inNeedToggleThreshold = 0;
  avatarAttachments: PendingAttachment[] = [];
  avatarResourceId: string | null = null;
  avatarPreviewUrl: string | null = null;
  private initialFormValues: unknown = null;
  private initialPaymentPlatforms: PaymentPlatformSnapshot[] = [];
  private initialAvatarResourceId: string | null = null;

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private authService = inject(AuthService);
  private profileService = inject(ProfileService);
  private securityService = inject(SecurityService);
  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
  private cryptoSession = inject(CryptoSessionService);
  private proposalCrypto = inject(ProposalCryptoService);
  private images = inject(EncryptedImageCacheService);
  private toastService = inject(ToastService);
  private profileLocation = inject(ProfileLocationService);

  ngOnInit() {
    this.loadPlatformOptions();
    this.buildPasswordForm();

    this.backButton = this.navigation.createBackButton(['/app/profile']);

    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled: true,
      onClick: () => void this.onSave()
    };

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.crewName = membership.crewName ?? null;
        void this.refreshAvatarPreview();
      }
    });

    this.fleetService.getStatus().subscribe({
      next: status => {
        this.fleetName = status.fleetName ?? null;
      }
    });

    this.loadProfile();
    void this.loadEncryptionStatus();
    this.cryptoSession.unlocked$.subscribe(unlocked => {
      this.encryptionUnlocked = unlocked;
      if (unlocked) {
        this.showUnlockDialog = false;
        void this.decryptLocationIntoForm();
      } else {
        this.profileLocation.clear();
      }
      void this.refreshAvatarPreview();
      this.updateSaveButton();
    });
  }

  get canEditAvatar(): boolean {
    return this.encryptionUnlocked;
  }

  get displayAvatarUrl(): string | null {
    return this.avatarAttachments[0]?.previewUrl ?? this.avatarPreviewUrl;
  }

  get hasPostalCode(): boolean {
    return !!String(this.form?.get('zipCode')?.value || '').trim();
  }

  get countryRequiredForPostalError(): boolean {
    return !!(this.form?.hasError('countryRequiredForPostal')
      && (this.form.get('countryCode')?.touched || this.form.get('zipCode')?.touched));
  }

  clearAvatar() {
    this.avatarAttachments = [];
    this.avatarResourceId = null;
    this.avatarPreviewUrl = null;
    this.updateSaveButton();
  }

  onAvatarAttachmentsChange() {
    this.updateSaveButton();
  }

  async onEncryptionUnlockedToggle(event: Event) {
    const input = event.target as HTMLInputElement;
    const wantsUnlocked = input.checked;

    if (wantsUnlocked) {
      if (this.encryptionUnlocked) {
        const phrase = this.authService.getSessionRecoveryPhrase();
        if (phrase) {
          this.encryptionToggleBusy = true;
          try {
            await this.authService.unlockWithRecoveryPhrase(phrase, true);
            this.toastService.success('Recovery key saved on this device.');
          } catch {
            this.toastService.error('Could not save the recovery key on this device.');
          } finally {
            this.encryptionToggleBusy = false;
          }
        }
        return;
      }

      this.showUnlockDialog = true;
      return;
    }

    this.encryptionToggleBusy = true;
    try {
      this.authService.lockEncryptionOnThisDevice();
      this.toastService.success('Encryption locked. Recovery key removed from this device.');
    } finally {
      this.encryptionToggleBusy = false;
    }
  }

  onEncryptionUnlockDialogCompleted() {
    this.showUnlockDialog = false;
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

  async onRecoveryKeyRotationConfirmed(result: { rememberOnDevice: boolean }) {
    if (!this.pendingRecoveryPhrase) {
      return;
    }

    try {
      await this.authService.rotateRecoveryPhrase(this.pendingRecoveryPhrase);
      await this.authService.unlockWithRecoveryPhrase(
        this.pendingRecoveryPhrase,
        result.rememberOnDevice
      );
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

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form?.get(controlName));
  }

  isPasswordInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.passwordForm?.get(controlName));
  }

  hasPasswordRequirement(requirement: string): boolean {
    const password = this.passwordForm?.get('newPassword')?.value;
    if (!password) {
      return false;
    }

    switch (requirement) {
      case 'uppercase':
        return /[A-Z]/.test(password);
      case 'lowercase':
        return /[a-z]/.test(password);
      case 'number':
        return /[0-9]/.test(password);
      case 'special':
        return /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password);
      case 'length':
        return password.length >= 8;
      default:
        return false;
    }
  }

  addPaymentPlatform() {
    if (!this.profile) {
      return;
    }
    this.profileService.addPaymentPlatform(this.profile, this.platformOptions);
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

  goToDonate(event?: Event) {
    event?.preventDefault();
    navigateToDonate(this.router);
  }

  openDeleteAccountDialog() {
    this.deleteAccountError = '';
    this.deletingAccount = false;
    this.showDeleteAccountDialog = true;
  }

  onDeleteAccountDismissed() {
    if (this.deletingAccount) {
      return;
    }
    this.showDeleteAccountDialog = false;
    this.deleteAccountError = '';
  }

  async onDeleteAccountConfirmed(password: string) {
    if (this.deletingAccount) {
      return;
    }

    this.deletingAccount = true;
    this.deleteAccountError = '';

    try {
      const result = await firstValueFrom(
        this.securityService.deleteAccount({ currentPassword: password })
      );

      if (!result.success) {
        this.deleteAccountError = result.message || 'Failed to delete account.';
        return;
      }

      this.showDeleteAccountDialog = false;
      this.toastService.success(result.message || 'Your account has been deleted.');
      this.authService.logout();
      await this.router.navigate(['/sign-in']);
    } catch (error) {
      this.deleteAccountError = this.extractErrorMessage(
        error as { error?: { message?: string; errors?: Record<string, string[]> } }
      );
    } finally {
      this.deletingAccount = false;
    }
  }

  async onSave() {
    if (!this.profile || !this.form || this.form.invalid || this.isSaving) {
      return;
    }

    const changingPassword = this.hasPasswordChanges();
    if (changingPassword && this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      this.toastService.error('Fix the password fields before saving.');
      return;
    }

    if (!this.hasProfileChanges() && !changingPassword) {
      return;
    }

    const paymentPlatformError = this.getPaymentPlatformValidationError();
    if (paymentPlatformError) {
      this.toastService.error(paymentPlatformError);
      return;
    }

    if (this.avatarAttachments.length > 0 && !this.canEditAvatar) {
      this.toastService.error('Unlock encryption before uploading an avatar.');
      return;
    }

    this.isSaving = true;
    this.updateSaveButton();

    try {
      if (changingPassword) {
        const passwordValues = this.passwordForm.getRawValue();
        const passwordResult = await firstValueFrom(
          this.securityService.changePassword({
            currentPassword: String(passwordValues.currentPassword ?? ''),
            newPassword: String(passwordValues.newPassword ?? ''),
            confirmPassword: String(passwordValues.confirmPassword ?? '')
          })
        );

        if (!passwordResult.success) {
          this.toastService.error(passwordResult.message || 'Failed to update password');
          return;
        }

        this.passwordForm.reset({
          currentPassword: '',
          newPassword: '',
          confirmPassword: ''
        });
        this.toastService.success(passwordResult.message || 'Password updated');
      }

      if (!this.hasProfileChanges()) {
        return;
      }

      let avatarResourceId = this.avatarResourceId;
      if (this.avatarAttachments.length > 0) {
        avatarResourceId = await this.proposalCrypto.uploadImageAttachment(
          this.avatarCryptoScope(),
          this.avatarAttachments[0],
          'ProfileAvatar'
        );
        this.avatarResourceId = avatarResourceId;
        this.avatarPreviewUrl = this.avatarAttachments[0].previewUrl ?? this.avatarPreviewUrl;
        this.avatarAttachments = [];
      }

      const v = this.form.getRawValue();
      const zipNormalized = normalizePostalCode(String(v.zipCode ?? ''));
      const countryRaw = (v.countryCode as string | null) || null;
      let encryptedLocation: EncryptedLocation | null | undefined;
      let clearLocation = false;
      if (!this.encryptionUnlocked && (zipNormalized || countryRaw)) {
        this.toastService.error('Unlock encryption to save your country and postal code.');
        return;
      }
      if (this.encryptionUnlocked) {
        if (!zipNormalized && !countryRaw) {
          clearLocation = true;
          encryptedLocation = null;
        } else {
          encryptedLocation = await this.profileLocation.encrypt(countryRaw, zipNormalized);
        }
      }
      // When locked, omit location fields so an empty form does not wipe ciphertext.

      const payload = {
        username: String(v.username).trim(),
        email: String(v.email).trim(),
        avatarResourceId,
        inNeedOfAid: !!v.inNeedOfAid,
        emergencyLevel: Number(v.emergencyLevel),
        peopleRepresentedCount: Number(v.peopleRepresentedCount),
        disabilityLevel: Number(v.disabilityLevel),
        identityGroups: normalizeIdentityGroups(v.identityGroups),
        needsSurvivalAid: !!v.needsSurvivalAid,
        encryptedLocation,
        clearLocation,
        paymentPlatforms: this.crewId > 0 ? this.getPaymentPlatformsForSave() : []
      };

      const result = await firstValueFrom(this.profileService.updateProfile(payload));
      if (result.success && result.profile) {
        if (avatarResourceId) {
          this.images.invalidate(avatarResourceId, 'ProfileAvatar');
        }
        if (this.initialAvatarResourceId && this.initialAvatarResourceId !== avatarResourceId) {
          this.images.invalidate(this.initialAvatarResourceId, 'ProfileAvatar');
        }
        this.profile = result.profile;
        this.canToggleInNeedOff = result.profile.canToggleInNeedOff ?? true;
        this.inNeedToggleThreshold = result.profile.inNeedToggleThreshold ?? 0;
        this.avatarResourceId = result.profile.avatarResourceId ?? null;
        this.loadPlatformOptions();
        this.form.patchValue({
          username: result.profile.username,
          email: result.profile.email,
          inNeedOfAid: result.profile.inNeedOfAid,
          emergencyLevel: result.profile.emergencyLevel,
          peopleRepresentedCount: result.profile.peopleRepresentedCount,
          disabilityLevel: result.profile.disabilityLevel,
          identityGroups: normalizeIdentityGroups(result.profile.identityGroups),
          needsSurvivalAid: result.profile.needsSurvivalAid
        });
        if (clearLocation) {
          this.form.patchValue({ countryCode: null, zipCode: '' });
          this.profileLocation.clear();
        } else if (encryptedLocation && countryRaw && zipNormalized) {
          this.profileLocation.setPlaintext({
            countryCode: countryRaw.toUpperCase(),
            zipCode: zipNormalized
          });
        }
        this.syncInNeedControl(result.profile.inNeedOfAid);
        this.captureInitialState();
        void this.refreshAvatarPreview();
        this.authService.updateCurrentUser({
          id: result.profile.id,
          username: result.profile.username,
          email: result.profile.email
        });
        this.toastService.success(result.message);
      } else {
        this.toastService.error(result.message || 'Failed to save profile');
      }
    } catch (error) {
      this.toastService.error(
        this.extractErrorMessage(error as { error?: { message?: string; errors?: Record<string, string[]> } })
      );
    } finally {
      this.isSaving = false;
      this.updateSaveButton();
    }
  }

  onPaymentPlatformChange() {
    this.updateSaveButton();
  }

  onIdentityGroupsChange(groups: string[]) {
    this.form.patchValue({ identityGroups: normalizeIdentityGroups(groups) });
    this.updateSaveButton();
  }

  onCountryCodeChange(code: string | null) {
    this.form.patchValue({ countryCode: code });
    this.form.get('countryCode')?.markAsTouched();
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
        this.canToggleInNeedOff = profile.canToggleInNeedOff ?? true;
        this.inNeedToggleThreshold = profile.inNeedToggleThreshold ?? 0;
        this.avatarResourceId = profile.avatarResourceId ?? null;
        this.syncPlatformOptions();
        this.authService.updateCurrentUser({
          id: profile.id,
          username: profile.username,
          email: profile.email
        });
        this.buildForm(profile);
        this.captureInitialState();
        void this.refreshAvatarPreview();
        void this.decryptLocationIntoForm();
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

  private async decryptLocationIntoForm(): Promise<void> {
    const envelope = this.profile?.encryptedLocation;
    const hadEnvelope = !!envelope?.nonce?.trim() && !!envelope?.ciphertext?.trim();
    if (!hadEnvelope || !this.encryptionUnlocked || !this.form) {
      return;
    }

    try {
      const location = await this.profileLocation.decrypt(envelope);
      if (location) {
        this.form.patchValue({
          countryCode: location.countryCode,
          zipCode: location.zipCode
        }, { emitEvent: false });
        this.captureInitialState();
        this.updateSaveButton();
        return;
      }
    } catch {
      // Encryption not fully ready yet — skip without toasting.
      return;
    }

    // Unreadable or empty payload — leave fields blank and drop the bad envelope
    // so unlock/sign-in does not keep failing.
    this.form.patchValue({ countryCode: null, zipCode: '' }, { emitEvent: false });
    if (this.profile) {
      this.profile = { ...this.profile, encryptedLocation: null };
    }
    void firstValueFrom(this.profileService.updateLocation({ clearLocation: true }))
      .then(result => {
        if (result?.profile) {
          this.profile = result.profile;
        }
      })
      .catch(() => undefined);
  }

  private avatarCryptoScope(): { crewId?: number } {
    // Profile avatars stay on the personal user content key so leaving a crew cannot wipe them.
    return {};
  }

  private async refreshAvatarPreview() {
    if (!this.avatarResourceId || !this.encryptionUnlocked) {
      if (!this.avatarAttachments.length) {
        this.avatarPreviewUrl = null;
      }
      return;
    }

    this.avatarPreviewUrl = await this.images.getDataUrl(
      this.avatarCryptoScope(),
      this.avatarResourceId,
      'ProfileAvatar'
    );
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
    const cached = this.profileLocation.current;
    this.form = this.fb.group({
      username: [profile.username, usernameValidators()],
      email: [profile.email, [Validators.required, Validators.email]],
      countryCode: [cached?.countryCode ?? null],
      zipCode: [cached?.zipCode ?? '', [optionalPostalCodeValidator]],
      inNeedOfAid: [this.canToggleInNeedOff ? profile.inNeedOfAid : true],
      emergencyLevel: [profile.emergencyLevel, [Validators.min(0), Validators.max(3)]],
      peopleRepresentedCount: [profile.peopleRepresentedCount ?? 1, [Validators.min(1), Validators.max(99)]],
      disabilityLevel: [profile.disabilityLevel ?? 0, [Validators.min(0), Validators.max(3)]],
      identityGroups: [normalizeIdentityGroups(profile.identityGroups)],
      needsSurvivalAid: [profile.needsSurvivalAid]
    }, { validators: [profileLocationValidator] });
    this.syncInNeedControl(this.canToggleInNeedOff ? profile.inNeedOfAid : true);

    this.form.statusChanges.subscribe(() => this.updateSaveButton());
    this.form.valueChanges.subscribe(() => this.updateSaveButton());
    this.updateSaveButton();
  }

  private syncInNeedControl(inNeedValue: boolean) {
    const ctrl = this.form?.get('inNeedOfAid');
    if (!ctrl) {
      return;
    }

    if (!this.canToggleInNeedOff) {
      ctrl.setValue(true, { emitEvent: false });
      ctrl.disable({ emitEvent: false });
      return;
    }

    ctrl.enable({ emitEvent: false });
    ctrl.setValue(!!inNeedValue, { emitEvent: false });
  }

  private buildPasswordForm() {
    this.passwordForm = this.fb.group(
      {
        currentPassword: [''],
        newPassword: ['', passwordStrengthValidator],
        confirmPassword: ['']
      },
      { validators: optionalPasswordChangeValidator }
    );

    this.passwordForm.statusChanges.subscribe(() => this.updateSaveButton());
    this.passwordForm.valueChanges.subscribe(() => this.updateSaveButton());
  }

  private updateSaveButton() {
    const paymentPlatformError = this.getPaymentPlatformValidationError();
    const changingPassword = this.hasPasswordChanges();
    const passwordReady = !changingPassword || this.passwordForm?.valid;
    const disabled = !this.form
      || !this.passwordForm
      || this.isLoading
      || this.isSaving
      || this.form.invalid
      || !passwordReady
      || !!paymentPlatformError
      || !pendingAttachmentsAllowSubmit(this.avatarAttachments)
      || (!this.hasProfileChanges() && !changingPassword);

    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled,
      onClick: () => void this.onSave()
    };
  }

  private hasPasswordChanges(): boolean {
    if (!this.passwordForm) {
      return false;
    }

    const values = this.passwordForm.getRawValue();
    return !!(
      String(values.currentPassword ?? '').length
      || String(values.newPassword ?? '').length
      || String(values.confirmPassword ?? '').length
    );
  }

  private captureInitialState() {
    if (!this.form || !this.profile) {
      return;
    }

    this.initialFormValues = this.form.getRawValue();
    this.initialPaymentPlatforms = this.serializePlatforms(this.profile.paymentPlatforms);
    this.initialAvatarResourceId = this.avatarResourceId;
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
    const avatarChanged = this.avatarAttachments.length > 0
      || (this.avatarResourceId ?? null) !== (this.initialAvatarResourceId ?? null);
    return formChanged || platformsChanged || avatarChanged;
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
      .filter(p => {
        const name = this.resolvePlatformName(p);
        return !!p.handle.trim() && (!!name || p.platformId > 0);
      })
      .map(p => {
        const platformName = this.resolvePlatformName(p);
        const knownInCrew = p.platformId > 0
          && this.platformOptions.some(option => option.id === p.platformId);
        return {
          id: p.id > 0 ? p.id : 0,
          platformId: knownInCrew ? p.platformId : 0,
          customPlatformName: knownInCrew ? undefined : platformName,
          platform: platformName,
          handle: p.handle.trim(),
          isPreferred: !!p.isPreferred
        };
      });
  }

  private resolvePlatformName(platform: PaymentPlatformAccount): string {
    if (platform.platformId === CUSTOM_PLATFORM_OPTION_ID) {
      return platform.customPlatformName?.trim() ?? '';
    }
    const optionName = this.platformOptions.find(option => option.id === platform.platformId)?.name;
    if (optionName?.trim()) {
      return optionName.trim();
    }
    if (typeof platform.platform === 'string') {
      return platform.platform.trim();
    }
    return platform.customPlatformName?.trim() ?? '';
  }

  private getPaymentPlatformValidationError(): string | null {
    if (this.crewId <= 0) {
      return null;
    }

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