import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { CrewService } from '../../services/crew.service';
import { ToastService } from '../../components/toast/toast.component';
import { CrewPrivacy, CrewScope } from '../../models/crew.model';

@Component({
  selector: 'app-create-crew',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './create-crew.component.html',
  styleUrl: './create-crew.component.css'
})
export class CreateCrewComponent {
  form: FormGroup;
  backButton: ActionBarButton;
  createButton: ActionBarButton;
  isLoading = false;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      maxSize: [4, [Validators.required, Validators.min(2), Validators.max(100)]],
      privacy: ['Public' as CrewPrivacy, Validators.required],
      scope: ['Online' as CrewScope, Validators.required],
      zipCode: [''],
      radiusMiles: [25]
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.createButton = {
      label: 'Create',
      type: 'primary',
      disabled: true,
      onClick: () => this.onSubmit()
    };

    this.form.statusChanges.subscribe(() => this.updateCreateButton());
    this.form.get('scope')?.valueChanges.subscribe(() => this.updateLocalValidators());
    this.updateLocalValidators();
  }

  get isLocal(): boolean {
    return this.form.get('scope')?.value === 'Local';
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
      zip?.setValue('');
      radius?.setValue(25);
    }

    zip?.updateValueAndValidity({ emitEvent: false });
    radius?.updateValueAndValidity({ emitEvent: false });
    this.updateCreateButton();
  }

  private updateCreateButton() {
    this.createButton.disabled = !this.form.valid || this.isLoading;
  }

  onSubmit() {
    if (this.form.invalid || this.isLoading) {
      return;
    }

    this.isLoading = true;
    this.updateCreateButton();

    const scope = this.form.get('scope')?.value as CrewScope;
    const payload = {
      name: this.form.get('name')?.value,
      maxSize: Number(this.form.get('maxSize')?.value),
      privacy: this.form.get('privacy')?.value as CrewPrivacy,
      scope,
      zipCode: scope === 'Local' ? this.form.get('zipCode')?.value : undefined,
      radiusMiles: scope === 'Local' ? Number(this.form.get('radiusMiles')?.value) : undefined
    };

    this.crewService.create(payload).subscribe({
      next: (result) => {
        if (result.success) {
          this.toastService.success(result.message);
          this.router.navigate(['/app/crew']);
        } else {
          this.toastService.error(result.message);
          this.isLoading = false;
          this.updateCreateButton();
        }
      },
      error: (error) => {
        this.toastService.error(error.error?.message || 'Failed to create crew');
        this.isLoading = false;
        this.updateCreateButton();
      }
    });
  }
}
