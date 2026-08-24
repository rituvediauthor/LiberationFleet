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

  form: FormGroup;
  backButton: ActionBarButton;
  signInButton: ActionBarButton;
  isLoading = false;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private authService = inject(AuthService);
  private deviceIdentity = inject(DeviceIdentityService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      usernameOrEmail: ['', Validators.required],
      password: ['', Validators.required]
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
      onClick: () => this.onSubmit()
    };
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  onUsernameEnter(event: Event) {
    event.preventDefault();
    this.passwordInput?.nativeElement.focus();
  }

  onSubmit() {
    if (this.form.invalid || this.isLoading) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.signInButton.disabled = true;
    this.signInButton.label = 'Signing in…';

    const credentials = {
      ...this.form.value,
      deviceId: this.deviceIdentity.getDeviceId(),
      deviceName: this.deviceIdentity.getDeviceName(),
      userAgent: this.deviceIdentity.getUserAgent()
    };

    this.authService.login(credentials).subscribe({
      next: () => {
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

  private navigateBack() {
    this.navigation.back(['/']);
  }
}
