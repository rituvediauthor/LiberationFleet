import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { CrewService } from '../../services/crew.service';
import { ToastService } from '../../components/toast/toast.component';
import { CrewPrivacy, CrewScope, CycleCapMode, UpdateCrewRequest } from '../../models/crew.model';
import { isSaveActionDisabled } from '../../utils/save-button.util';
import { isControlInvalidForA11y } from '../../utils/a11y-form.util';

@Component({
  selector: 'app-edit-crew',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, ConfirmDialogComponent],
  templateUrl: './edit-crew.component.html',
  styleUrl: './edit-crew.component.css'
})
export class EditCrewComponent implements OnInit {
  form!: FormGroup;
  joinCode = '';
  memberCount = 0;
  requireApprovalForEdits = true;
  monthlyGivingCapacity = 0;
  loading = true;
  loadError = '';
  isSaving = false;
  showLeaveDialog = false;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  private initialFormValues: unknown = null;

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      maxSize: [30, [Validators.required, Validators.min(2), Validators.max(50)]],
      privacy: ['Public' as CrewPrivacy, Validators.required],
      scope: ['Online' as CrewScope, Validators.required],
      zipCode: [''],
      radiusMiles: [25],
      allowSurvivalThresholds: [true],
      allowCrossCrewGiving: [false],
      requireApprovalForEdits: [true],
      inNeedDefaultThreshold: [20, [Validators.required, Validators.min(0)]],
      libraryOfThingsEnabled: [true],
      memberCycleCapMode: ['CapacityBased' as CycleCapMode, Validators.required],
      memberCycleCapFixedAmount: [0, [Validators.min(0)]],
      memberCycleCapMultiplier: [2, [Validators.required, Validators.min(0)]],
      nonMemberCycleCapMode: ['CapacityBased' as CycleCapMode, Validators.required],
      nonMemberCycleCapFixedAmount: [0, [Validators.min(0)]],
      nonMemberCycleCapMultiplier: [0.5, [Validators.required, Validators.min(0)]],
      allowCrewmateFileAttachments: [false],
      minimumCrewmateTenureDaysForAttachments: [0, [Validators.min(0)]],
      minimumContributionForAttachments: [0, [Validators.min(0)]],
      minimumCrewmateTenureDaysForProposals: [0, [Validators.min(0)]],
      minimumContributionForProposals: [0, [Validators.min(0)]]
    });

    this.backButton = this.navigation.createBackButton(['/app/crew']);

    this.updateSaveButton();

    this.form.statusChanges.subscribe(() => this.updateSaveButton());
    this.form.valueChanges.subscribe(() => this.updateSaveButton());
    this.form.get('scope')?.valueChanges.subscribe(() => this.updateLocalValidators());

    this.loadCrew();
  }

  get saveButtonLabel(): string {
    return this.requireApprovalForEdits ? 'Submit proposals' : 'Save';
  }

  get isLocal(): boolean {
    return this.form.get('scope')?.value === 'Local';
  }

  get isMemberCapacityBased(): boolean {
    return this.form.get('memberCycleCapMode')?.value === 'CapacityBased';
  }

  get isNonMemberCapacityBased(): boolean {
    return this.form.get('nonMemberCycleCapMode')?.value === 'CapacityBased';
  }

  get allowCrewmateFileAttachments(): boolean {
    return !!this.form.get('allowCrewmateFileAttachments')?.value;
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form?.get(controlName));
  }

  get memberCycleCapPreview(): number {
    if (this.isMemberCapacityBased) {
      return this.monthlyGivingCapacity * Number(this.form.get('memberCycleCapMultiplier')?.value ?? 0);
    }

    return Number(this.form.get('memberCycleCapFixedAmount')?.value ?? 0);
  }

  get nonMemberCycleCapPreview(): number {
    if (this.isNonMemberCapacityBased) {
      return this.monthlyGivingCapacity * Number(this.form.get('nonMemberCycleCapMultiplier')?.value ?? 0);
    }

    return Number(this.form.get('nonMemberCycleCapFixedAmount')?.value ?? 0);
  }

  onLeaveCrew() {
    this.showLeaveDialog = true;
  }

  onConfirmLeave() {
    this.showLeaveDialog = false;
    this.crewService.leaveCrew().subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message);
          this.router.navigate(['/app/crew']);
          return;
        }
        this.toastService.error(result.message || 'Failed to leave crew');
      },
      error: () => this.toastService.error('Failed to leave crew')
    });
  }

  onCancelLeave() {
    this.showLeaveDialog = false;
  }

  copyJoinCode() {
    if (!this.joinCode) {
      return;
    }

    navigator.clipboard.writeText(this.joinCode).then(
      () => this.toastService.success('Join code copied'),
      () => this.toastService.error('Failed to copy join code')
    );
  }

  onSave() {
    if (this.isSaveDisabled || this.isSaving) {
      return;
    }

    this.isSaving = true;
    this.updateSaveButton();

    const payload = this.buildPayload();
    this.crewService.updateCrew(payload).subscribe({
      next: result => {
        if (result.success && result.proposalsSubmitted) {
          this.toastService.success(result.message);
          if (result.crew) {
            this.joinCode = result.crew.joinCode;
            this.memberCount = result.crew.memberCount;
            this.requireApprovalForEdits = result.crew.requireApprovalForEdits ?? true;
            this.patchFormFromCrew(result.crew);
            this.updateMemberCountValidators();
          }
          this.captureInitialState();
          this.isSaving = false;
          this.updateSaveButton();
          this.router.navigate(['/app/crew/proposals/list/pending']);
          return;
        }

        if (result.success && result.crew) {
          this.toastService.success(result.message);
          this.joinCode = result.crew.joinCode;
          this.patchFormFromCrew(result.crew);
          this.captureInitialState();
          this.isSaving = false;
          this.updateSaveButton();
          return;
        }
        this.toastService.error(result.message || 'Failed to save crew');
        this.isSaving = false;
        this.updateSaveButton();
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to save crew');
        this.isSaving = false;
        this.updateSaveButton();
      }
    });
  }

  private get isSaveDisabled(): boolean {
    return isSaveActionDisabled({
      form: this.form,
      initialValues: this.initialFormValues,
      isLoading: this.loading,
      isSaving: this.isSaving
    });
  }

  private loadCrew() {
    this.loading = true;
    this.loadError = '';
    this.crewService.getCurrentCrew().subscribe({
      next: result => {
        if (!result.success || !result.crew) {
          this.loadError = result.message || 'Failed to load crew';
          this.loading = false;
          this.updateSaveButton();
          return;
        }
        this.joinCode = result.crew.joinCode;
        this.memberCount = result.crew.memberCount;
        this.monthlyGivingCapacity = result.crew.monthlyGivingCapacity ?? 0;
        this.requireApprovalForEdits = result.crew.requireApprovalForEdits ?? true;
        this.patchFormFromCrew(result.crew);
        this.updateLocalValidators();
        this.updateMemberCountValidators();
        this.captureInitialState();
        this.loading = false;
        this.updateSaveButton();
      },
      error: () => {
        this.loadError = 'Failed to load crew';
        this.loading = false;
        this.updateSaveButton();
      }
    });
  }

  private patchFormFromCrew(crew: {
    name: string;
    maxSize: number;
    privacy: CrewPrivacy | string;
    scope: CrewScope | string;
    zipCode?: string;
    radiusMiles?: number;
    allowSurvivalThresholds?: boolean;
    allowCrossCrewGiving?: boolean;
    requireApprovalForEdits?: boolean;
    inNeedDefaultThreshold?: number;
    libraryOfThingsEnabled?: boolean;
    memberCycleCapMode?: CycleCapMode | string;
    memberCycleCapFixedAmount?: number;
    memberCycleCapMultiplier?: number;
    nonMemberCycleCapMode?: CycleCapMode | string;
    nonMemberCycleCapFixedAmount?: number;
    nonMemberCycleCapMultiplier?: number;
    allowCrewmateFileAttachments?: boolean;
    minimumCrewmateTenureDaysForAttachments?: number;
    minimumContributionForAttachments?: number;
    minimumCrewmateTenureDaysForProposals?: number;
    minimumContributionForProposals?: number;
  }) {
    this.form.patchValue({
      name: crew.name,
      maxSize: crew.maxSize,
      privacy: crew.privacy,
      scope: crew.scope,
      zipCode: crew.zipCode ?? '',
      radiusMiles: crew.radiusMiles ?? 25,
      allowSurvivalThresholds: crew.allowSurvivalThresholds ?? true,
      allowCrossCrewGiving: crew.allowCrossCrewGiving ?? false,
      requireApprovalForEdits: crew.requireApprovalForEdits ?? true,
      inNeedDefaultThreshold: crew.inNeedDefaultThreshold ?? 20,
      libraryOfThingsEnabled: crew.libraryOfThingsEnabled ?? true,
      memberCycleCapMode: crew.memberCycleCapMode ?? 'CapacityBased',
      memberCycleCapFixedAmount: crew.memberCycleCapFixedAmount ?? 0,
      memberCycleCapMultiplier: crew.memberCycleCapMultiplier ?? 2,
      nonMemberCycleCapMode: crew.nonMemberCycleCapMode ?? 'CapacityBased',
      nonMemberCycleCapFixedAmount: crew.nonMemberCycleCapFixedAmount ?? 0,
      nonMemberCycleCapMultiplier: crew.nonMemberCycleCapMultiplier ?? 0.5,
      allowCrewmateFileAttachments: crew.allowCrewmateFileAttachments ?? false,
      minimumCrewmateTenureDaysForAttachments: crew.minimumCrewmateTenureDaysForAttachments ?? 0,
      minimumContributionForAttachments: crew.minimumContributionForAttachments ?? 0,
      minimumCrewmateTenureDaysForProposals: crew.minimumCrewmateTenureDaysForProposals ?? 0,
      minimumContributionForProposals: crew.minimumContributionForProposals ?? 0
    }, { emitEvent: false });
  }

  private captureInitialState() {
    this.initialFormValues = this.form.getRawValue();
  }

  private buildPayload(): UpdateCrewRequest {
    const scope = this.form.get('scope')?.value as CrewScope;
    return {
      name: String(this.form.get('name')?.value).trim(),
      maxSize: Number(this.form.get('maxSize')?.value),
      privacy: this.form.get('privacy')?.value as CrewPrivacy,
      scope,
      zipCode: scope === 'Local' ? String(this.form.get('zipCode')?.value).trim() : undefined,
      radiusMiles: scope === 'Local' ? Number(this.form.get('radiusMiles')?.value) : undefined,
      allowSurvivalThresholds: !!this.form.get('allowSurvivalThresholds')?.value,
      allowCrossCrewGiving: !!this.form.get('allowCrossCrewGiving')?.value,
      requireApprovalForEdits: !!this.form.get('requireApprovalForEdits')?.value,
      inNeedDefaultThreshold: Number(this.form.get('inNeedDefaultThreshold')?.value),
      libraryOfThingsEnabled: !!this.form.get('libraryOfThingsEnabled')?.value,
      memberCycleCapMode: this.form.get('memberCycleCapMode')?.value as CycleCapMode,
      memberCycleCapFixedAmount: Number(this.form.get('memberCycleCapFixedAmount')?.value),
      memberCycleCapMultiplier: Number(this.form.get('memberCycleCapMultiplier')?.value),
      nonMemberCycleCapMode: this.form.get('nonMemberCycleCapMode')?.value as CycleCapMode,
      nonMemberCycleCapFixedAmount: Number(this.form.get('nonMemberCycleCapFixedAmount')?.value),
      nonMemberCycleCapMultiplier: Number(this.form.get('nonMemberCycleCapMultiplier')?.value),
      allowCrewmateFileAttachments: !!this.form.get('allowCrewmateFileAttachments')?.value,
      minimumCrewmateTenureDaysForAttachments: Number(this.form.get('minimumCrewmateTenureDaysForAttachments')?.value),
      minimumContributionForAttachments: Number(this.form.get('minimumContributionForAttachments')?.value),
      minimumCrewmateTenureDaysForProposals: Number(this.form.get('minimumCrewmateTenureDaysForProposals')?.value),
      minimumContributionForProposals: Number(this.form.get('minimumContributionForProposals')?.value)
    };
  }

  private updateLocalValidators() {
    const zip = this.form.get('zipCode');
    const radius = this.form.get('radiusMiles');

    if (this.isLocal) {
      zip?.setValidators([Validators.required, Validators.pattern(/^\d{5}$/)]);
      radius?.setValidators([Validators.required, Validators.min(1), Validators.max(500)]);
    } else {
      zip?.clearValidators();
      radius?.clearValidators();
    }

    zip?.updateValueAndValidity({ emitEvent: false });
    radius?.updateValueAndValidity({ emitEvent: false });
    this.updateSaveButton();
  }

  private updateSaveButton() {
    this.saveButton = {
      label: this.saveButtonLabel,
      type: 'primary',
      disabled: this.isSaveDisabled,
      onClick: () => this.onSave()
    };
  }

  private updateMemberCountValidators() {
    const maxSize = this.form.get('maxSize');
    const minSize = Math.max(2, this.memberCount);
    maxSize?.setValidators([
      Validators.required,
      Validators.min(minSize),
      Validators.max(50)
    ]);
    maxSize?.updateValueAndValidity({ emitEvent: false });
    this.updateSaveButton();
  }
}
