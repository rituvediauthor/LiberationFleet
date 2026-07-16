import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { UserService } from '../../services/user.service';
import { NavigationService } from '../../services/navigation.service';
import { ToastService } from '../../components/toast/toast.component';
import { isControlInvalidForA11y } from '../../utils/a11y-form.util';

function passwordStrengthValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (!value) return null;

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

  if (!password || !confirmPassword) return null;
  return password.value === confirmPassword.value ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.css'
})
export class ResetPasswordComponent implements OnInit {
  form: FormGroup;
  backButton: ActionBarButton;
  resetButton: ActionBarButton;
  isLoading = false;
  tokenError: string | null = null;
  tokenEmail: string | null = null;
  resetToken: string | null = null;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private userService = inject(UserService);
  private toastService = inject(ToastService);
  private route = inject(ActivatedRoute);

  constructor() {
    this.form = this.fb.group({
      newPassword: ['', [Validators.required, passwordStrengthValidator]],
      confirmPassword: ['', Validators.required]
    }, {
      validators: passwordMatchValidator
    });

    this.backButton = {
      label: 'back',
      type: 'back',
      onClick: () => this.navigateBack()
    };

    this.resetButton = {
      label: 'Reset Password',
      type: 'primary',
      disabled: true,
      onClick: () => this.onSubmit()
    };

    this.form.statusChanges.subscribe(() => {
      this.resetButton.disabled = !this.form.valid || this.isLoading;
    });
  }

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      this.resetToken = params['token'];

      if (!this.resetToken) {
        this.tokenError = 'No reset token provided. Please use the link from the recovery email.';
        return;
      }

      this.userService.validateResetToken(this.resetToken).subscribe({
        next: (response) => {
          if (response.isValid) {
            this.tokenEmail = response.email ?? null;
            this.tokenError = null;
          } else {
            this.tokenError = response.message || 'This reset link has expired. Please request a new one.';
            this.form.disable();
          }
        },
        error: () => {
          this.tokenError = 'Invalid or expired reset token. Please request a new one.';
          this.form.disable();
        }
      });
    });
  }

  hasPasswordRequirement(requirement: string): boolean {
    const password = this.form.get('newPassword')?.value;
    if (!password) return false;

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

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
  }

  onSubmit() {
    if (this.form.invalid || this.isLoading || !this.resetToken) {
      return;
    }

    this.isLoading = true;
    this.resetButton.disabled = true;

    const resetData = {
      token: this.resetToken,
      newPassword: this.form.get('newPassword')?.value,
      confirmPassword: this.form.get('confirmPassword')?.value
    };

    this.userService.resetPassword(resetData).subscribe({
      next: () => {
        this.toastService.success('Password reset successfully!');
        this.router.navigate(['/sign-in']);
      },
      error: (error) => {
        this.toastService.error(error.error?.message || 'Failed to reset password');
        this.isLoading = false;
        this.resetButton.disabled = !this.form.valid;
      }
    });
  }

  private navigateBack() {
    this.navigation.back(['/sign-in']);
  }
}
