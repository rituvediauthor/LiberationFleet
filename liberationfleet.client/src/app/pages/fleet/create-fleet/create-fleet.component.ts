import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { HubLoadingComponent } from '../../../components/hub-loading/hub-loading.component';
import { FleetService } from '../../../services/fleet.service';
import { NavigationService } from '../../../services/navigation.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetPrivacy, FleetScope } from '../../../models/fleet.model';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';

@Component({
  selector: 'app-create-fleet',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, HubLoadingComponent],
  templateUrl: './create-fleet.component.html',
  styleUrl: './create-fleet.component.css'
})
export class CreateFleetComponent {
  form: FormGroup;
  backButton: ActionBarButton;
  createButton: ActionBarButton;
  isLoading = false;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      privacy: ['Public' as FleetPrivacy, Validators.required],
      scope: ['Online' as FleetScope, Validators.required],
      zipCode: [''],
      radiusMiles: [25]
    });

    this.backButton = this.navigation.createBackButton(['/app/fleet']);

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

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
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

    const scope = this.form.get('scope')?.value as FleetScope;
    const payload = {
      name: this.form.get('name')?.value,
      privacy: this.form.get('privacy')?.value as FleetPrivacy,
      scope,
      zipCode: scope === 'Local' ? this.form.get('zipCode')?.value : undefined,
      radiusMiles: scope === 'Local' ? Number(this.form.get('radiusMiles')?.value) : undefined
    };

    this.fleetService.create(payload).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message);
          this.router.navigate(['/app/fleet']);
        } else {
          this.toastService.error(result.message);
          this.isLoading = false;
          this.updateCreateButton();
        }
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to create fleet');
        this.isLoading = false;
        this.updateCreateButton();
      }
    });
  }
}
