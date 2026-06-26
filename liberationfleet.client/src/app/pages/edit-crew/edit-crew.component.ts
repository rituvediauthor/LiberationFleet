import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { CrewService } from '../../services/crew.service';
import { ToastService } from '../../components/toast/toast.component';
import { CrewPrivacy, CrewScope, UpdateCrewRequest } from '../../models/crew.model';
import { isSaveActionDisabled } from '../../utils/save-button.util';

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
  loading = true;
  loadError = '';
  isSaving = false;
  showLeaveDialog = false;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  private initialFormValues: unknown = null;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      maxSize: [4, [Validators.required, Validators.min(2), Validators.max(100)]],
      privacy: ['Public' as CrewPrivacy, Validators.required],
      scope: ['Online' as CrewScope, Validators.required],
      zipCode: [''],
      radiusMiles: [25],
      allowSurvivalThresholds: [true],
      requireApprovalForEdits: [true],
      inNeedDefaultThreshold: [20, [Validators.required, Validators.min(0)]]
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

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
    requireApprovalForEdits?: boolean;
    inNeedDefaultThreshold?: number;
  }) {
    this.form.patchValue({
      name: crew.name,
      maxSize: crew.maxSize,
      privacy: crew.privacy,
      scope: crew.scope,
      zipCode: crew.zipCode ?? '',
      radiusMiles: crew.radiusMiles ?? 25,
      allowSurvivalThresholds: crew.allowSurvivalThresholds ?? true,
      requireApprovalForEdits: crew.requireApprovalForEdits ?? true,
      inNeedDefaultThreshold: crew.inNeedDefaultThreshold ?? 20
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
      requireApprovalForEdits: !!this.form.get('requireApprovalForEdits')?.value,
      inNeedDefaultThreshold: Number(this.form.get('inNeedDefaultThreshold')?.value)
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
      Validators.max(100)
    ]);
    maxSize?.updateValueAndValidity({ emitEvent: false });
    this.updateSaveButton();
  }
}
