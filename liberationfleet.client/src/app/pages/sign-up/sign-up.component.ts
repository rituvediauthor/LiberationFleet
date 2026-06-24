import { Component, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { RecoveryKeyDisplayComponent } from '../../components/recovery-key-display/recovery-key-display.component';
import { AuthService } from '../../services/auth.service';
import { UserService } from '../../services/user.service';
import { ToastService } from '../../components/toast/toast.component';
import { generateRecoveryPhrase } from '../../services/crypto/recovery-key.util';
import { AuthResult } from '../../models/user.model';

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
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, RecoveryKeyDisplayComponent],
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
  showRecoveryKeyModal = false;
  pendingRecoveryPhrase = '';

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private userService = inject(UserService);
  private toastService = inject(ToastService);
  private cdr = inject(ChangeDetectorRef);

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
      this.updateSignUpButton();
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
      this.cdr.markForCheck();
    }
  }

  onSubmit() {
    if (this.form.invalid || this.isLoading) {
      return;
    }

    void this.completeSignUp();
  }

  private async completeSignUp() {
    this.isLoading = true;
    this.updateSignUpButton();
    this.cdr.detectChanges();

    const formData = {
      username: this.form.get('username')?.value,
      email: this.form.get('email')?.value,
      password: this.form.get('password')?.value,
      confirmPassword: this.form.get('confirmPassword')?.value
    };

    try {
      const response = await firstValueFrom(this.userService.create(formData));
      const authResult = this.normalizeAuthResult(response);

      if (!authResult.success || !authResult.token) {
        this.toastService.error(authResult.message || 'Sign up failed');
        return;
      }

      this.authService.establishSession(authResult);
      this.pendingRecoveryPhrase = generateRecoveryPhrase();
      await this.authService.setupNewAccountEncryption(this.pendingRecoveryPhrase, true);
      this.showRecoveryKeyModal = true;
    } catch (error: unknown) {
      const message = (error as { error?: { message?: string } })?.error?.message
        || (error instanceof Error ? error.message : null)
        || 'Sign up failed';
      this.toastService.error(message);
    } finally {
      this.isLoading = false;
      this.updateSignUpButton();
      this.cdr.detectChanges();
    }
  }

  private normalizeAuthResult(response: AuthResult): AuthResult {
    const raw = response as AuthResult & {
      Success?: boolean;
      Message?: string;
      Token?: string;
      User?: AuthResult['user'];
    };

    return {
      success: response.success ?? raw.Success ?? false,
      message: response.message ?? raw.Message,
      token: response.token ?? raw.Token,
      user: response.user ?? raw.User
    };
  }

  private updateSignUpButton() {
    this.signUpButton = {
      label: 'Sign Up',
      type: 'primary',
      disabled: !this.form.valid || this.isLoading,
      onClick: () => this.onSubmit()
    };
  }

  private navigateBack() {
    this.router.navigate(['/sign-in']);
  }

  async onRecoveryKeyConfirmed() {
    if (!this.pendingRecoveryPhrase) {
      return;
    }

    this.pendingRecoveryPhrase = '';
    this.showRecoveryKeyModal = false;
    this.toastService.success('Account created successfully!');
    this.router.navigate(['/app/crew']);
    this.cdr.detectChanges();
  }
}
