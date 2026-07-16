import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetRuleOperationResponse } from '../../../models/fleet.model';
import { isSaveActionDisabled } from '../../../utils/save-button.util';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';

@Component({
  selector: 'app-fleet-rule-edit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './fleet-rule-edit.component.html',
  styleUrl: './fleet-rule-edit.component.css'
})
export class FleetRuleEditComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  isSubmitting = false;
  isDeleting = false;
  loading = true;
  loadError = '';
  ruleId = 0;
  requireApprovalForEdits = true;
  private initialFormValues: { title: string; description: string; isPublic: boolean } | null = null;

  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.ruleId = Number(this.route.snapshot.paramMap.get('id'));

    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(4000)]],
      isPublic: [false]
    });

    this.backButton = this.navigation.createBackButton(['/app/fleet/rules']);

    this.updateSaveButton();

    this.fleetService.getCurrent().subscribe({
      next: result => {
        if (result.success && result.fleet) {
          this.requireApprovalForEdits = result.fleet.requireApprovalForEdits ?? true;
          this.updateSaveButton();
        }
      }
    });

    this.loadRule();

    this.form.statusChanges.subscribe(() => this.updateSaveButton());
    this.form.valueChanges.subscribe(() => this.updateSaveButton());
  }

  get saveButtonLabel(): string {
    return this.requireApprovalForEdits ? 'Submit proposal' : 'Save';
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form?.get(controlName));
  }

  onSubmit() {
    if (this.form.invalid || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.updateSaveButton();

    const { title, description, isPublic } = this.form.getRawValue();
    this.fleetService.updateRule(this.ruleId, {
      isPublic: !!isPublic,
      title: title.trim(),
      description: description.trim()
    }).subscribe({
      next: result => this.handleSaveResult(result),
      error: (err: { error?: { message?: string } }) => {
        this.toastService.error(err?.error?.message || 'Failed to save rule');
        this.isSubmitting = false;
        this.updateSaveButton();
      }
    });
  }

  deleteRule() {
    if (this.isDeleting || this.isSubmitting) {
      return;
    }

    this.isDeleting = true;
    this.fleetService.deleteRule(this.ruleId).subscribe({
      next: result => {
        this.isDeleting = false;
        if (result.success && result.proposalsSubmitted) {
          this.toastService.success(result.message || 'Proposal submitted for fleet approval');
          this.router.navigate(['/app/fleet/proposals']);
          return;
        }
        if (result.success) {
          this.toastService.success('Rule deleted');
          this.router.navigate(['/app/fleet/rules']);
          return;
        }
        this.toastService.error(result.message || 'Failed to delete rule');
      },
      error: () => {
        this.isDeleting = false;
        this.toastService.error('Failed to delete rule');
      }
    });
  }

  private handleSaveResult(result: FleetRuleOperationResponse) {
    if (result.success && result.proposalsSubmitted) {
      this.toastService.success(result.message || 'Proposal submitted for fleet approval');
      this.router.navigate(['/app/fleet/proposals']);
      return;
    }
    if (result.success) {
      this.toastService.success('Rule saved');
      this.router.navigate(['/app/fleet/rules']);
      return;
    }
    this.toastService.error(result.message || 'Failed to save rule');
    this.isSubmitting = false;
    this.updateSaveButton();
  }

  private loadRule() {
    this.loading = true;
    this.loadError = '';
    this.fleetService.getRule(this.ruleId).subscribe({
      next: response => {
        this.loading = false;
        if (!response.success || !response.rule) {
          this.loadError = response.message || 'Failed to load rule';
          this.updateSaveButton();
          return;
        }
        this.form.patchValue({
          title: response.rule.title,
          description: response.rule.description,
          isPublic: response.rule.isPublic
        });
        this.initialFormValues = this.form.getRawValue();
        this.updateSaveButton();
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Failed to load rule';
        this.toastService.error(this.loadError);
      }
    });
  }

  private updateSaveButton() {
    this.saveButton = {
      label: this.saveButtonLabel,
      type: 'primary',
      disabled: isSaveActionDisabled({
        form: this.form,
        initialValues: this.initialFormValues,
        isLoading: this.loading,
        isSaving: this.isSubmitting
      }),
      onClick: () => this.onSubmit()
    };
  }
}
