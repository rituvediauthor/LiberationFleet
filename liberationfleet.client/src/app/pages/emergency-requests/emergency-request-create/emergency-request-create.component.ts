import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { EmergencyRequestService } from '../../../services/emergency-request.service';
import { ToastService } from '../../../components/toast/toast.component';

@Component({
  selector: 'app-emergency-request-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './emergency-request-create.component.html',
  styleUrl: './emergency-request-create.component.css'
})
export class EmergencyRequestCreateComponent implements OnInit {
  form!: FormGroup;
  submitting = false;
  backButton!: ActionBarButton;
  submitButton!: ActionBarButton;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private emergencyRequestService = inject(EmergencyRequestService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      purpose: ['', [Validators.required, Validators.maxLength(2000)]],
      amountNeeded: ['', [Validators.required, Validators.min(0.01)]]
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/emergency-requests'])
    };

    this.updateSubmitButton();
    this.form.statusChanges.subscribe(() => this.updateSubmitButton());
  }

  private updateSubmitButton() {
    this.submitButton = {
      label: 'Submit Request',
      type: 'primary',
      disabled: this.submitting || this.form.invalid,
      onClick: () => this.onSubmit()
    };
  }

  private onSubmit() {
    if (this.form.invalid || this.submitting) return;

    const purpose = String(this.form.get('purpose')?.value ?? '').trim();
    const amountNeeded = Number(this.form.get('amountNeeded')?.value);
    if (!purpose || amountNeeded <= 0) return;

    this.submitting = true;
    this.updateSubmitButton();

    this.emergencyRequestService.create(purpose, amountNeeded).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Emergency request created');
          this.router.navigate(['/app/crew/emergency-requests']);
          return;
        }
        this.toastService.error(result.message || 'Failed to create request');
        this.submitting = false;
        this.updateSubmitButton();
      },
      error: error => {
        this.toastService.error(error?.error?.message || 'Failed to create request');
        this.submitting = false;
        this.updateSubmitButton();
      }
    });
  }
}
