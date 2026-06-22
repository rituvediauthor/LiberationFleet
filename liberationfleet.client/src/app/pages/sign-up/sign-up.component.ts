import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { AuthService } from '../../services/auth.service';
import { UserService } from '../../services/user.service';
import { ToastService } from '../../components/toast/toast.component';

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
  const password = control.get('password');
  const confirmPassword = control.get('confirmPassword');

  if (!password || !confirmPassword) return null;
  return password.value === confirmPassword.value ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-sign-up',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './sign-up.component.html',
  styleUrl: './sign-up.component.css'
})
export class SignUpComponent {
  form: FormGroup;
  backButton: ActionBarButton;
  signUpButton: ActionBarButton;
  isLoading = false;
  showPrivacyPolicyModal = false;
  privacyPolicyText = '';
  privacyPolicyLoading = false;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private userService = inject(UserService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      username: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, passwordStrengthValidator]],
      confirmPassword: ['', Validators.required],
      privacyPolicyAccepted: [false, Validators.requiredTrue]
    }, {
      validators: passwordMatchValidator
    });

    this.backButton = {
      label: 'back',
      type: 'back',
      onClick: () => this.navigateBack()
    };

    this.signUpButton = {
      label: 'Sign Up',
      type: 'primary',
      disabled: true,
      onClick: () => this.onSubmit()
    };

    this.form.statusChanges.subscribe(() => {
      this.signUpButton.disabled = !this.form.valid || this.isLoading;
    });
  }

  hasPasswordRequirement(requirement: string): boolean {
    const password = this.form.get('password')?.value;
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

  showPrivacyPolicy(event: Event) {
    event.preventDefault();
    void this.openPrivacyPolicy();
  }

  closePrivacyPolicy() {
    this.showPrivacyPolicyModal = false;
  }

  onPrivacyBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('privacy-dialog-backdrop')) {
      this.closePrivacyPolicy();
    }
  }

  private async openPrivacyPolicy() {
    this.showPrivacyPolicyModal = true;

    if (this.privacyPolicyText) {
      return;
    }

    this.privacyPolicyLoading = true;
    try {
      this.privacyPolicyText = await firstValueFrom(
        this.http.get('/assets/privacy-policy.txt', { responseType: 'text' })
      );
    } catch {
      this.privacyPolicyText = 'Unable to load the Privacy Policy. Please try again later.';
    } finally {
      this.privacyPolicyLoading = false;
    }
  }

  onSubmit() {
    if (this.form.invalid || this.isLoading) {
      return;
    }

    this.isLoading = true;
    this.signUpButton.disabled = true;

    const formData = {
      username: this.form.get('username')?.value,
      email: this.form.get('email')?.value,
      password: this.form.get('password')?.value,
      confirmPassword: this.form.get('confirmPassword')?.value
    };

    this.userService.create(formData).subscribe({
      next: async (response) => {
        this.authService.establishSession(response);
        try {
          await this.authService.initializeEncryption(formData.password, true);
        } catch {
          this.toastService.error('Account created, but encryption setup failed.');
        }
        this.toastService.success('Account created successfully!');
        this.router.navigate(['/app/crew']);
      },
      error: (error) => {
        this.toastService.error(error.error?.message || 'Sign up failed');
        this.isLoading = false;
        this.signUpButton.disabled = !this.form.valid;
      }
    });
  }

  private navigateBack() {
    this.router.navigate(['/sign-in']);
  }
}
