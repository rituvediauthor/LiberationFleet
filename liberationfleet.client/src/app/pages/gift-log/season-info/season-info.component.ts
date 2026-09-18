import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { PaymentPlatformEditorComponent } from '../../../components/payment-platform-editor/payment-platform-editor.component';
import { IdentityGroupsEditorComponent } from '../../../components/identity-groups-editor/identity-groups-editor.component';
import { GiftService } from '../../../services/gift.service';
import { ProfileService } from '../../../services/profile.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { PaymentPlatformOption, SeasonProfile } from '../../../models/gift.model';
import { PaymentPlatformAccount } from '../../../models/profile.model';
import { mergePaymentPlatformOptions } from '../../../utils/payment-platform-options.util';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';
import { normalizeIdentityGroups } from '../../../utils/identity-groups.util';
import { EMERGENCY_LEVEL_HINT } from '../../../constants/emergency-level';
import { formValuesChanged, valuesEqual } from '../../../utils/save-button.util';

type PaymentPlatformSnapshot = {
  id: number;
  platformId: number;
  customPlatformName: string;
  handle: string;
  isPreferred: boolean;
};

@Component({
  selector: 'app-season-info',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    PageLayoutComponent,
    PaymentPlatformEditorComponent,
    IdentityGroupsEditorComponent
  ],
  templateUrl: './season-info.component.html',
  styleUrl: './season-info.component.css'
})
export class SeasonInfoComponent implements OnInit {
  readonly emergencyLevelHint = EMERGENCY_LEVEL_HINT;
  form!: FormGroup;
  profile: SeasonProfile | null = null;
  platformOptions: PaymentPlatformOption[] = [];
  private basePlatformOptions: PaymentPlatformOption[] = [];
  loading = true;
  isSaving = false;
  canToggleInNeedOff = false;
  canEditEstimatedContribution = true;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  private initialFormValues: unknown = null;
  private initialPaymentPlatforms: PaymentPlatformSnapshot[] = [];

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private giftService = inject(GiftService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      estimatedMonthlyContribution: [0, [Validators.required, Validators.min(0)]],
      emergencyLevel: [0, [Validators.min(0), Validators.max(3)]],
      peopleRepresentedCount: [1, [Validators.min(1), Validators.max(99)]],
      disabilityLevel: [0, [Validators.min(0), Validators.max(3)]],
      identityGroups: [[]],
      inNeedOfAid: [true],
      needsSurvivalAid: [false]
    });

    this.backButton = this.navigation.createBackButton(['/app/crew/gift-log']);
    this.updateSaveButton();

    this.crewService.getPaymentPlatforms(true).subscribe({
      next: platforms => {
        this.basePlatformOptions = platforms;
        this.syncPlatformOptions();
      }
    });

    this.giftService.getSeasonProfile().subscribe({
      next: profile => {
        this.applyProfile(profile);
        this.captureInitialState();
        this.loading = false;
        this.updateSaveButton();
      },
      error: err => {
        this.loading = false;
        this.toastService.error(err?.message ?? 'Failed to load season profile');
      }
    });

    this.form.statusChanges.subscribe(() => this.updateSaveButton());
    this.form.valueChanges.subscribe(() => this.updateSaveButton());
  }

  get paymentPlatforms(): PaymentPlatformAccount[] {
    return this.profile?.paymentPlatforms ?? [];
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
  }

  addPaymentPlatform() {
    if (!this.profile) {
      return;
    }
    const account = this.profileService.createPaymentPlatformAccount(this.platformOptions);
    this.profile = {
      ...this.profile,
      paymentPlatforms: [...this.profile.paymentPlatforms, account]
    };
    this.syncPlatformOptions();
    this.updateSaveButton();
  }

  removePaymentPlatform(accountId: number) {
    if (!this.profile) {
      return;
    }
    this.profile = {
      ...this.profile,
      paymentPlatforms: this.profile.paymentPlatforms.filter(p => p.id !== accountId)
    };
    this.syncPlatformOptions();
    this.updateSaveButton();
  }

  onPaymentPlatformChange() {
    this.updateSaveButton();
  }

  onIdentityGroupsChange(groups: string[]) {
    this.form.patchValue({ identityGroups: normalizeIdentityGroups(groups) });
    this.updateSaveButton();
  }

  onSave() {
    if (!this.profile || this.form.invalid || this.isSaving || !this.hasChanges()) {
      return;
    }

    const hasPlatforms = this.paymentPlatforms.length > 0
      && this.paymentPlatforms.every(p => {
        const hasHandle = !!p.handle.trim();
        const hasPlatform = p.platformId > 0 || !!p.customPlatformName?.trim();
        return hasHandle && hasPlatform;
      });
    if (!hasPlatforms) {
      this.toastService.error('Add at least one valid payment platform.');
      return;
    }

    const v = this.form.getRawValue();
    const updatedProfile: SeasonProfile = {
      ...this.profile,
      estimatedMonthlyContribution: Number(v.estimatedMonthlyContribution),
      emergencyLevel: Number(v.emergencyLevel),
      peopleRepresentedCount: Number(v.peopleRepresentedCount),
      disabilityLevel: Number(v.disabilityLevel),
      identityGroups: normalizeIdentityGroups(v.identityGroups),
      inNeedOfAid: this.canToggleInNeedOff ? !!v.inNeedOfAid : true,
      needsSurvivalAid: !!v.needsSurvivalAid
    };

    this.isSaving = true;
    this.updateSaveButton();
    this.giftService.updateSeasonProfile(this.giftService.buildSeasonProfileRequest(updatedProfile)).subscribe({
      next: result => {
        this.isSaving = false;
        if (!result.success || !result.profile) {
          this.toastService.error(result.message || 'Failed to save season profile');
          this.updateSaveButton();
          return;
        }
        this.applyProfile(result.profile);
        this.captureInitialState();
        this.toastService.success(result.message || 'Season profile saved');
        this.updateSaveButton();
      },
      error: () => {
        this.isSaving = false;
        this.toastService.error('Failed to save season profile');
        this.updateSaveButton();
      }
    });
  }

  private applyProfile(profile: SeasonProfile) {
    this.profile = profile;
    this.canToggleInNeedOff = profile.canToggleInNeedOff;
    this.canEditEstimatedContribution = profile.canEditEstimatedContribution;
    this.form.patchValue({
      estimatedMonthlyContribution: profile.estimatedMonthlyContribution,
      emergencyLevel: profile.emergencyLevel,
      peopleRepresentedCount: profile.peopleRepresentedCount,
      disabilityLevel: profile.disabilityLevel,
      identityGroups: normalizeIdentityGroups(profile.identityGroups),
      inNeedOfAid: this.canToggleInNeedOff ? profile.inNeedOfAid : true,
      needsSurvivalAid: profile.needsSurvivalAid
    });
    this.syncInNeedControl(this.canToggleInNeedOff ? profile.inNeedOfAid : true);
    this.syncEstimatedControl();
    this.syncPlatformOptions();
  }

  private captureInitialState() {
    this.initialFormValues = this.form.getRawValue();
    this.initialPaymentPlatforms = this.serializePlatforms(this.paymentPlatforms);
  }

  private hasChanges(): boolean {
    if (!this.form || this.initialFormValues === null) {
      return false;
    }

    const formChanged = formValuesChanged(this.form, this.initialFormValues);
    const platformsChanged = !valuesEqual(
      this.serializePlatforms(this.paymentPlatforms),
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

  private syncInNeedControl(inNeedValue: boolean) {
    const ctrl = this.form.get('inNeedOfAid');
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

  private syncEstimatedControl() {
    const ctrl = this.form.get('estimatedMonthlyContribution');
    if (!ctrl) {
      return;
    }
    if (!this.canEditEstimatedContribution) {
      ctrl.disable({ emitEvent: false });
      return;
    }
    ctrl.enable({ emitEvent: false });
  }

  private updateSaveButton() {
    const hasPlatforms = this.paymentPlatforms.length > 0
      && this.paymentPlatforms.every(p => {
        const hasHandle = !!p.handle.trim();
        const hasPlatform = p.platformId > 0 || !!p.customPlatformName?.trim();
        return hasHandle && hasPlatform;
      });
    const disabled = this.isSaving || this.form.invalid || !this.hasChanges() || !hasPlatforms;
    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled,
      onClick: () => this.onSave()
    };
  }

  private syncPlatformOptions() {
    this.platformOptions = mergePaymentPlatformOptions(
      this.basePlatformOptions,
      this.profile?.paymentPlatforms ?? []
    );
  }
}
