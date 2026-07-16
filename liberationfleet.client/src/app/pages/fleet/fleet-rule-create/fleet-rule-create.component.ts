import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetRuleOperationResponse } from '../../../models/fleet.model';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';

@Component({
  selector: 'app-fleet-rule-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './fleet-rule-create.component.html',
  styleUrl: './fleet-rule-create.component.css'
})
export class FleetRuleCreateComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  isSubmitting = false;
  requireApprovalForEdits = true;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(4000)]],
      isPublic: [false]
    });

    this.backButton = this.navigation.createBackButton(['/app/fleet/rules']);

    this.updateCreateButton();

    this.fleetService.getCurrent().subscribe({
      next: result => {
        if (result.success && result.fleet) {
          this.requireApprovalForEdits = result.fleet.requireApprovalForEdits ?? true;
          this.updateCreateButton();
        }
      }
    });

    this.form.statusChanges.subscribe(() => this.updateCreateButton());
    this.form.valueChanges.subscribe(() => this.updateCreateButton());
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form?.get(controlName));
  }

  onSubmit() {
    if (this.form.invalid || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    const { title, description, isPublic } = this.form.getRawValue();
    this.fleetService.createRule({
      isPublic: !!isPublic,
      title: title.trim(),
      description: description.trim()
    }).subscribe({
      next: result => this.handleCreateResult(result),
      error: (err: { error?: { message?: string } }) => {
        this.toastService.error(err?.error?.message || 'Failed to create rule');
        this.isSubmitting = false;
        this.updateCreateButton();
      }
    });
  }

  private handleCreateResult(result: FleetRuleOperationResponse) {
    if (result.success && result.proposalsSubmitted) {
      this.toastService.success(result.message || 'Proposal submitted for fleet approval');
      this.router.navigate(['/app/fleet/proposals']);
      return;
    }
    if (result.success) {
      this.toastService.success('Rule created');
      this.router.navigate(['/app/fleet/rules']);
      return;
    }
    this.toastService.error(result.message || 'Failed to create rule');
    this.isSubmitting = false;
    this.updateCreateButton();
  }

  private updateCreateButton() {
    this.createButton = {
      label: this.requireApprovalForEdits ? 'Submit proposal' : 'Create',
      type: 'primary',
      disabled: this.isSubmitting || this.form.invalid,
      onClick: () => this.onSubmit()
    };
  }
}
