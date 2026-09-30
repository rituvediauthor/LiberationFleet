import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { AuthService } from '../../services/auth.service';
import { NavigationService } from '../../services/navigation.service';
import { DeviceIdentityService } from '../../services/device-identity.service';
import { ToastService } from '../../components/toast/toast.component';
import { describeLoadError } from '../../utils/http-error.util';

@Component({
  selector: 'app-sign-in',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, RouterLink],
  templateUrl: './sign-in.component.html',
  styleUrl: './sign-in.component.css'
})
export class SignInComponent {
  @ViewChild('passwordInput') passwordInput?: ElementRef<HTMLInputElement>;
  @ViewChild('mfaCodeInput') mfaCodeInput?: ElementRef<HTMLInputElement>;

  form: FormGroup;
  mfaForm: FormGroup;
  backButton: ActionBarButton;
  signInButton: ActionBarButton;
  isLoading = false;
  mfaStep = false;
  mfaChallengeToken = '';
  mfaHint = '';

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private authService = inject(AuthService);
  private deviceIdentity = inject(DeviceIdentityService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      usernameOrEmail: ['', Validators.required],
      password: ['', Validators.required],
      rememberMe: [this.authService.isRememberLoginEnabled()]
    });

    this.mfaForm = this.fb.group({
      code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]]
    });

    this.backButton = {
      label: 'back',
      type: 'back',
      onClick: () => this.navigateBack()
    };

    this.signInButton = {
      label: 'Sign In',
      type: 'primary',
      disabled: false,
      onClick: () => this.onPrimaryAction()
    };
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  isMfaInvalid(controlName: string): boolean {
    const control = this.mfaForm.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  onUsernameEnter(event: Event) {
    event.preventDefault();
    this.passwordInput?.nativeElement.focus();
  }

  onPrimaryAction() {
    if (this.mfaStep) {
      this.onVerifyMfa();
      return;
    }

    this.onSubmit();
  }

  onSubmit() {
    if (this.form.invalid || this.isLoading) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.signInButton.disabled = true;
    this.signInButton.label = 'Signing in…';

    const rememberMe = !!this.form.get('rememberMe')?.value;
    this.authService.setRememberLoginEnabled(rememberMe);

    const { usernameOrEmail, password } = this.form.value;
    const credentials = {
      usernameOrEmail,
      password,
      deviceId: this.deviceIdentity.getDeviceId(),
      deviceName: this.deviceIdentity.getDeviceName(),
      userAgent: this.deviceIdentity.getUserAgent()
    };

    this.authService.login(credentials).subscribe({
      next: response => {
        if (response.requiresMfa && response.mfaChallengeToken) {
          this.mfaStep = true;
          this.mfaChallengeToken = response.mfaChallengeToken;
          this.mfaHint = response.message || 'Enter the code we emailed you.';
          this.mfaForm.reset();
          this.isLoading = false;
          this.signInButton.disabled = false;
          this.signInButton.label = 'Verify code';
          setTimeout(() => this.mfaCodeInput?.nativeElement.focus(), 0);
          this.toastService.success(this.mfaHint);
          return;
        }

        void this.router.navigate(['/app/crew']);
      },
      error: (error) => {
        this.toastService.error(describeLoadError(error, 'Sign in failed'));
        this.isLoading = false;
        this.signInButton.disabled = false;
        this.signInButton.label = 'Sign In';
      }
    });
  }

  onVerifyMfa() {
    if (this.mfaForm.invalid || this.isLoading || !this.mfaChallengeToken) {
      this.mfaForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.signInButton.disabled = true;
    this.signInButton.label = 'Verifying…';

    const code = String(this.mfaForm.get('code')?.value || '').trim();
    this.authService.verifyMfa(this.mfaChallengeToken, code).subscribe({
      next: () => {
        void this.router.navigate(['/app/crew']);
      },
      error: (error) => {
        this.toastService.error(describeLoadError(error, 'Verification failed'));
        this.isLoading = false;
        this.signInButton.disabled = false;
        this.signInButton.label = 'Verify code';
      }
    });
  }

  resendMfaCode() {
    if (this.isLoading || !this.mfaChallengeToken) {
      return;
    }

    this.isLoading = true;
    this.authService.resendMfa(this.mfaChallengeToken).subscribe({
      next: response => {
        this.isLoading = false;
        if (response.mfaChallengeToken) {
          this.mfaChallengeToken = response.mfaChallengeToken;
        }
        this.toastService.success(response.message || 'Code resent.');
      },
      error: (error) => {
        this.isLoading = false;
        this.toastService.error(describeLoadError(error, 'Could not resend code'));
      }
    });
  }

  backToPassword() {
    this.mfaStep = false;
    this.mfaChallengeToken = '';
    this.mfaHint = '';
    this.mfaForm.reset();
    this.signInButton.label = 'Sign In';
    this.signInButton.disabled = false;
  }

  private navigateBack() {
    if (this.mfaStep) {
      this.backToPassword();
      return;
    }

    this.navigation.back(['/']);
  }
}
