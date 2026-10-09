import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { PaymentPlatformEditorComponent } from '../../../components/payment-platform-editor/payment-platform-editor.component';
import { IdentityGroupsEditorComponent } from '../../../components/identity-groups-editor/identity-groups-editor.component';
import { CrewService } from '../../../services/crew.service';
import { CrewmateService } from '../../../services/crewmate.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { CUSTOM_PLATFORM_OPTION_ID, PaymentPlatformAccount } from '../../../models/profile.model';
import { PaymentPlatformOption } from '../../../models/gift.model';
import {
  AddPlaceholderCrewmateRequest,
  AidSeasonAccounting,
  AidSurvivalThresholdDraft
} from '../../../models/crewmate.model';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';
import { normalizeIdentityGroups } from '../../../utils/identity-groups.util';
import { CharCounterComponent } from '../../../components/char-counter/char-counter.component';
import { TextFieldLimits } from '../../../utils/text-field-limits';
import { EMERGENCY_LEVEL_HINT } from '../../../constants/emergency-level';

@Component({
  selector: 'app-create-placeholder',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    PageLayoutComponent,
    PaymentPlatformEditorComponent,
    IdentityGroupsEditorComponent,
    CharCounterComponent
  ],
  templateUrl: './create-placeholder.component.html',
  styleUrl: './create-placeholder.component.css'
})
export class CreatePlaceholderComponent implements OnInit {
  form!: FormGroup;
  paymentPlatforms: PaymentPlatformAccount[] = [];
  platformOptions: PaymentPlatformOption[] = [];
  saving = false;
  seasonStarted = false;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  readonly nameMaxLength = TextFieldLimits.placeholderDisplayName;
  readonly emergencyLevelHint = EMERGENCY_LEVEL_HINT;

  aidDraft = {
    percentBoost: 0,
    estimatedMonthlyContribution: 0,
    lifetimeContributions: 0,
    receptionThisYear: 0,
    cycleReceived: 0,
    hasActiveCycle: false,
    receptionOrder: null as number | null,
    autoJoinSeasonOnStart: true,
    survivalThresholds: [] as AidSurvivalThresholdDraft[]
  };

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private crewmateService = inject(CrewmateService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(this.nameMaxLength)]],
      isFinancialMember: [false],
      inNeedOfAid: [true],
      needsSurvivalAid: [false],
      emergencyLevel: [0, [Validators.min(0), Validators.max(3)]],
      peopleRepresentedCount: [1, [Validators.min(1), Validators.max(99)]],
      disabilityLevel: [0, [Validators.min(0), Validators.max(3)]],
      identityGroups: [[]]
    });

    this.backButton = this.navigation.createBackButton(['/app/crew/crewmates']);
    this.updateSaveButton();

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.seasonStarted = !!membership.seasonStarted;
        if (!membership.canManagePlaceholders) {
          this.toastService.error('Only organizers and accountants can add placeholder crewmates here.');
          void this.router.navigate(['/app/crew/crewmates']);
        }
      }
    });

    this.crewService.getPaymentPlatforms().subscribe({
      next: platforms => {
        this.platformOptions = platforms;
        this.updateSaveButton();
      },
      error: () => this.toastService.error('Failed to load payment platforms')
    });

    this.form.valueChanges.subscribe(() => this.updateSaveButton());
  }

  get derivedSurvivalReceived(): number {
    return this.aidDraft.survivalThresholds.reduce((sum, row) => {
      const threshold = Math.max(0, Number(row.thresholdAmount) || 0);
      const remaining = Math.max(0, Number(row.amountRemaining) || 0);
      return sum + Math.max(0, threshold - remaining);
    }, 0);
  }

  get derivedTotalReception(): number {
    return Math.max(0, Number(this.aidDraft.cycleReceived) || 0) + this.derivedSurvivalReceived;
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
  }

  addPaymentPlatform() {
    this.paymentPlatforms = [
      ...this.paymentPlatforms,
      this.profileService.createPaymentPlatformAccount(this.platformOptions)
    ];
    this.updateSaveButton();
  }

  removePaymentPlatform(accountId: number) {
    this.paymentPlatforms = this.paymentPlatforms.filter(account => account.id !== accountId);
    this.updateSaveButton();
  }

  setPreferredPlatform(accountId: number) {
    this.paymentPlatforms = this.paymentPlatforms.map(account => ({
      ...account,
      isPreferred: account.id === accountId
    }));
    this.updateSaveButton();
  }

  onPaymentPlatformChange() {
    this.updateSaveButton();
  }

  onIdentityGroupsChange(groups: string[]) {
    this.form.patchValue({ identityGroups: normalizeIdentityGroups(groups) });
    this.updateSaveButton();
  }

  addSurvivalThresholdRow() {
    const maxOrder = this.aidDraft.survivalThresholds.reduce(
      (max, row) => Math.max(max, Number(row.order) || 0),
      0
    );
    this.aidDraft.survivalThresholds = [
      ...this.aidDraft.survivalThresholds,
      {
        id: null,
        thresholdAmount: 0,
        amountRemaining: 0,
        order: maxOrder + 1
      }
    ];
  }

  removeSurvivalThresholdRow(index: number) {
    this.aidDraft.survivalThresholds = this.aidDraft.survivalThresholds.filter((_, i) => i !== index);
  }

  private updateSaveButton() {
    this.saveButton = {
      label: 'Add placeholder',
      type: 'primary',
      disabled: this.saving || !this.canSave(),
      onClick: () => this.onSave()
    };
  }

  private canSave(): boolean {
    if (this.form.invalid) {
      return false;
    }

    const name = String(this.form.get('name')?.value ?? '').trim();
    if (!name) {
      return false;
    }

    return this.paymentPlatforms.some(
      account =>
        account.handle.trim() &&
        (account.platformId > 0 ||
          (account.platformId === CUSTOM_PLATFORM_OPTION_ID && account.customPlatformName?.trim()))
    );
  }

  private buildSeasonAccounting(): AidSeasonAccounting {
    return {
      cycleReceived: Math.max(0, Number(this.aidDraft.cycleReceived) || 0),
      hasActiveCycle: !!this.aidDraft.hasActiveCycle,
      receptionOrder: this.aidDraft.receptionOrder ? Math.max(1, Number(this.aidDraft.receptionOrder)) : null,
      autoJoinSeasonOnStart: !!this.aidDraft.autoJoinSeasonOnStart,
      survivalThresholds: this.aidDraft.survivalThresholds.map((row, index) => ({
        id: row.id ?? null,
        thresholdAmount: Math.max(0, Number(row.thresholdAmount) || 0),
        amountRemaining: Math.max(0, Number(row.amountRemaining) || 0),
        order: Math.max(1, Number(row.order) || index + 1)
      })),
      removedThresholdIds: []
    };
  }

  private onSave() {
    if (!this.canSave() || this.saving) {
      return;
    }

    const v = this.form.getRawValue();
    const name = String(v.name ?? '').trim();
    const platforms = this.paymentPlatforms
      .filter(
        account =>
          account.handle.trim() &&
          (account.platformId > 0 ||
            (account.platformId === CUSTOM_PLATFORM_OPTION_ID && account.customPlatformName?.trim()))
      )
      .map(account => ({
        platformId: account.platformId === CUSTOM_PLATFORM_OPTION_ID ? 0 : account.platformId,
        customPlatformName:
          account.platformId === CUSTOM_PLATFORM_OPTION_ID ? account.customPlatformName?.trim() : undefined,
        handle: account.handle.trim(),
        isPreferred: !!account.isPreferred
      }));

    if (!platforms.some(platform => platform.isPreferred)) {
      platforms[0].isPreferred = true;
    }

    const body: AddPlaceholderCrewmateRequest = {
      name,
      paymentPlatforms: platforms,
      emergencyLevel: Number(v.emergencyLevel),
      peopleRepresentedCount: Number(v.peopleRepresentedCount),
      disabilityLevel: Number(v.disabilityLevel),
      identityGroups: normalizeIdentityGroups(v.identityGroups),
      inNeedOfAid: !!v.inNeedOfAid,
      needsSurvivalAid: !!v.needsSurvivalAid,
      isFinancialMember: !!v.isFinancialMember,
      estimatedMonthlyContribution: Math.max(0, Number(this.aidDraft.estimatedMonthlyContribution) || 0),
      percentBoost: Math.max(0, Number(this.aidDraft.percentBoost) || 0),
      lifetimeContributionOverride: Math.max(0, Number(this.aidDraft.lifetimeContributions) || 0),
      receptionThisYearOverride: Math.max(0, Number(this.aidDraft.receptionThisYear) || 0),
      seasonAccounting: this.buildSeasonAccounting()
    };

    this.saving = true;
    this.updateSaveButton();

    this.crewmateService.addPlaceholderCrewmateRich(body).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Placeholder added');
          void this.router.navigate(['/app/crew/crewmates', result.userId]);
          return;
        }

        this.toastService.error(result.message || 'Failed to add placeholder');
        this.saving = false;
        this.updateSaveButton();
      },
      error: error => {
        this.toastService.error(error?.error?.message || 'Failed to add placeholder');
        this.saving = false;
        this.updateSaveButton();
      }
    });
  }
}
