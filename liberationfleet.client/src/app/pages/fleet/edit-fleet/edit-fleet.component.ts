import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetPrivacy, FleetScope, UpdateFleetRequest } from '../../../models/fleet.model';
import { isSaveActionDisabled } from '../../../utils/save-button.util';

@Component({
  selector: 'app-edit-fleet',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './edit-fleet.component.html',
  styleUrl: './edit-fleet.component.css'
})
export class EditFleetComponent implements OnInit {
  form!: FormGroup;
  joinCode = '';
  requireApprovalForEdits = true;
  loading = true;
  loadError = '';
  isSaving = false;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  private initialFormValues: unknown = null;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      privacy: ['Public' as FleetPrivacy, Validators.required],
      scope: ['Online' as FleetScope, Validators.required],
      zipCode: [''],
      radiusMiles: [25],
      requireApprovalForEdits: [true],
      libraryOfThingsEnabled: [true],
      allowCrewmateFileAttachments: [false],
      minimumCrewmateTenureDaysForAttachments: [0, [Validators.min(0)]],
      minimumContributionForAttachments: [0, [Validators.min(0)]],
      minimumCrewmateTenureDaysForProposals: [0, [Validators.min(0)]],
      minimumContributionForProposals: [0, [Validators.min(0)]]
    });

    this.backButton = this.navigation.createBackButton(['/app/fleet']);

    this.updateSaveButton();

    this.form.statusChanges.subscribe(() => this.updateSaveButton());
    this.form.valueChanges.subscribe(() => this.updateSaveButton());
    this.form.get('scope')?.valueChanges.subscribe(() => this.updateLocalValidators());

    this.loadFleet();
  }

  get saveButtonLabel(): string {
    return this.requireApprovalForEdits ? 'Submit proposals' : 'Save';
  }

  get isLocal(): boolean {
    return this.form.get('scope')?.value === 'Local';
  }

  get allowCrewmateFileAttachments(): boolean {
    return !!this.form.get('allowCrewmateFileAttachments')?.value;
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
    this.fleetService.updateCurrent(payload).subscribe({
      next: result => {
        if (result.success && result.proposalsSubmitted) {
          this.toastService.success(result.message);
          if (result.fleet) {
            this.joinCode = result.fleet.joinCode;
            this.requireApprovalForEdits = result.fleet.requireApprovalForEdits ?? true;
            this.patchFormFromFleet(result.fleet);
          }
          this.captureInitialState();
          this.isSaving = false;
          this.updateSaveButton();
          this.router.navigate(['/app/fleet/proposals']);
          return;
        }

        if (result.success && result.fleet) {
          this.toastService.success(result.message);
          this.joinCode = result.fleet.joinCode;
          this.patchFormFromFleet(result.fleet);
          this.captureInitialState();
          this.isSaving = false;
          this.updateSaveButton();
          return;
        }
        this.toastService.error(result.message || 'Failed to save fleet');
        this.isSaving = false;
        this.updateSaveButton();
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to save fleet');
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

  private loadFleet() {
    this.loading = true;
    this.loadError = '';
    this.fleetService.getCurrent().subscribe({
      next: result => {
        if (!result.success || !result.fleet) {
          this.loadError = result.message || 'Failed to load fleet';
          this.loading = false;
          this.updateSaveButton();
          return;
        }
        this.joinCode = result.fleet.joinCode;
        this.requireApprovalForEdits = result.fleet.requireApprovalForEdits ?? true;
        this.patchFormFromFleet(result.fleet);
        this.updateLocalValidators();
        this.captureInitialState();
        this.loading = false;
        this.updateSaveButton();
      },
      error: () => {
        this.loadError = 'Failed to load fleet';
        this.loading = false;
        this.updateSaveButton();
      }
    });
  }

  private patchFormFromFleet(fleet: {
    name: string;
    privacy: FleetPrivacy | string;
    scope: FleetScope | string;
    zipCode?: string;
    radiusMiles?: number;
    requireApprovalForEdits?: boolean;
    libraryOfThingsEnabled?: boolean;
    allowCrewmateFileAttachments?: boolean;
    minimumCrewmateTenureDaysForAttachments?: number;
    minimumContributionForAttachments?: number;
    minimumCrewmateTenureDaysForProposals?: number;
    minimumContributionForProposals?: number;
  }) {
    this.form.patchValue({
      name: fleet.name,
      privacy: fleet.privacy,
      scope: fleet.scope,
      zipCode: fleet.zipCode ?? '',
      radiusMiles: fleet.radiusMiles ?? 25,
      requireApprovalForEdits: fleet.requireApprovalForEdits ?? true,
      libraryOfThingsEnabled: fleet.libraryOfThingsEnabled ?? true,
      allowCrewmateFileAttachments: fleet.allowCrewmateFileAttachments ?? false,
      minimumCrewmateTenureDaysForAttachments: fleet.minimumCrewmateTenureDaysForAttachments ?? 0,
      minimumContributionForAttachments: fleet.minimumContributionForAttachments ?? 0,
      minimumCrewmateTenureDaysForProposals: fleet.minimumCrewmateTenureDaysForProposals ?? 0,
      minimumContributionForProposals: fleet.minimumContributionForProposals ?? 0
    }, { emitEvent: false });
  }

  private captureInitialState() {
    this.initialFormValues = this.form.getRawValue();
  }

  private buildPayload(): UpdateFleetRequest {
    const scope = this.form.get('scope')?.value as FleetScope;
    return {
      name: String(this.form.get('name')?.value).trim(),
      privacy: this.form.get('privacy')?.value as FleetPrivacy,
      scope,
      zipCode: scope === 'Local' ? String(this.form.get('zipCode')?.value).trim() : undefined,
      radiusMiles: scope === 'Local' ? Number(this.form.get('radiusMiles')?.value) : undefined,
      requireApprovalForEdits: !!this.form.get('requireApprovalForEdits')?.value,
      libraryOfThingsEnabled: !!this.form.get('libraryOfThingsEnabled')?.value,
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
}
