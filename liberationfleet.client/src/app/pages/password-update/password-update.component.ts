import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { SecurityService } from '../../services/security.service';
import { ToastService } from '../../components/toast/toast.component';
import { isControlInvalidForA11y } from '../../utils/a11y-form.util';

function passwordStrengthValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (!value) {
    return null;
  }

  const hasUpperCase = /[A-Z]/.test(value);
  const hasLowerCase = /[a-z]/.test(value);
  const hasNumeric = /[0-9]/.test(value);
  const hasSpecialChar = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(value);
  const isLengthValid = value.length >= 8;

  const passwordValid = hasUpperCase && hasLowerCase && hasNumeric && hasSpecialChar && isLengthValid;
  return passwordValid ? null : { passwordStrength: true };
}

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword');
  const confirmPassword = control.get('confirmPassword');

  if (!password || !confirmPassword) {
    return null;
  }

  return password.value === confirmPassword.value ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-password-update',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './password-update.component.html',
  styleUrl: './password-update.component.css'
})
export class PasswordUpdateComponent {
  form: FormGroup;
  backButton: ActionBarButton;
  saveButton: ActionBarButton;
  isLoading = false;

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private securityService = inject(SecurityService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, passwordStrengthValidator]],
      confirmPassword: ['', Validators.required]
    }, {
      validators: passwordMatchValidator
    });

    this.backButton = this.navigation.createBackButton(['/app/profile/preferences/security']);

    this.saveButton = {
      label: 'Update password',
      type: 'primary',
      disabled: true,
      onClick: () => this.onSubmit()
    };

    this.form.statusChanges.subscribe(() => {
      this.saveButton.disabled = !this.form.valid || this.isLoading;
    });
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
  }

  hasPasswordRequirement(requirement: string): boolean {
    const password = this.form.get('newPassword')?.value;
    if (!password) {
      return false;
    }

    switch (requirement) {
      case 'uppercase':
        return /[A-Z]/.test(password);
      case 'lowercase':
        return /[a-z]/.test(password);
      case 'number':
        return /[0-9]/.test(password);
      case 'special':
        return /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password);
      case 'length':
        return password.length >= 8;
      default:
        return false;
    }
  }

  onSubmit() {
    if (this.form.invalid || this.isLoading) {
      return;
    }

    this.isLoading = true;
    this.saveButton.disabled = true;

    this.securityService.changePassword({
      currentPassword: this.form.get('currentPassword')?.value,
      newPassword: this.form.get('newPassword')?.value,
      confirmPassword: this.form.get('confirmPassword')?.value
    }).subscribe({
      next: response => {
        this.isLoading = false;
        if (response.success) {
          this.toastService.success(response.message || 'Password updated');
          this.form.reset();
          this.router.navigate(['/app/profile/preferences/security']);
        } else {
          this.toastService.error(response.message || 'Failed to update password');
          this.saveButton.disabled = !this.form.valid;
        }
      },
      error: error => {
        this.isLoading = false;
        this.toastService.error(error.error?.message || 'Failed to update password');
        this.saveButton.disabled = !this.form.valid;
      }
    });
  }
}
