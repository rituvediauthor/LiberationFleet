import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { UserService } from '../../services/user.service';
import { NavigationService } from '../../services/navigation.service';
import { ToastService } from '../../components/toast/toast.component';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.css'
})
export class ForgotPasswordComponent {
  form: FormGroup;
  backButton: ActionBarButton;
  sendButton: ActionBarButton;
  isLoading = false;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private userService = inject(UserService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });

    this.backButton = {
      label: 'back',
      type: 'back',
      onClick: () => this.navigateBack()
    };

    this.sendButton = {
      label: 'Send Recovery Email',
      type: 'primary',
      disabled: true,
      onClick: () => this.onSubmit()
    };

    this.form.statusChanges.subscribe(() => {
      this.sendButton.disabled = !this.form.valid || this.isLoading;
    });
  }

  onSubmit() {
    if (this.form.invalid || this.isLoading) {
      return;
    }

    this.isLoading = true;
    this.sendButton.disabled = true;

    const email = this.form.get('email')?.value;

    this.userService.requestPasswordReset(email).subscribe({
      next: (response) => {
        this.toastService.success(response.message);
        this.form.reset();
        this.isLoading = false;
        this.sendButton.disabled = !this.form.valid;
      },
      error: (error) => {
        this.toastService.error(error.error?.message || 'Failed to send recovery email');
        this.isLoading = false;
        this.sendButton.disabled = !this.form.valid;
      }
    });
  }

  private navigateBack() {
    this.navigation.back(['/sign-in']);
  }
}
