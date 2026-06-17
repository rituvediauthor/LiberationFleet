import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { GiftService } from '../../services/gift.service';
import { ToastService } from '../../components/toast/toast.component';
import { PaymentPlatformOption } from '../../models/gift.model';

@Component({
  selector: 'app-complete-gift',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  template: `
    <app-page-layout [backButton]="backButton" [primaryButton]="completeButton">
      <div class="form-container">
        <div class="form-card">
          <h1>Complete Middleman Gift</h1>
          <p class="help-text">Select the payment platform you used to complete this gift transfer:</p>

          <form [formGroup]="form">
            <div class="form-group">
              <label for="paymentPlatformId">Payment Platform</label>
              <select id="paymentPlatformId" formControlName="paymentPlatformId" class="form-input">
                <option value="">Select platform</option>
                <option *ngFor="let platform of platforms" [ngValue]="platform.id">{{ platform.name }}</option>
              </select>
            </div>
          </form>
        </div>
      </div>
    </app-page-layout>
  `,
  styles: [`
    .form-container {
      display: flex;
      justify-content: center;
      width: 100%;
    }

    .form-card {
      width: 100%;
      max-width: 450px;
      background: white;
      padding: 32px;
      border-radius: 8px;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    }

    h1 {
      font-size: 28px;
      font-weight: 600;
      margin: 0 0 16px;
      color: #333;
    }

    .help-text {
      font-size: 14px;
      color: #666;
      margin-bottom: 24px;
    }

    .form-group {
      margin-bottom: 20px;
      display: flex;
      flex-direction: column;
    }

    label {
      font-size: 14px;
      font-weight: 600;
      margin-bottom: 8px;
      color: #333;
    }

    .form-input {
      padding: 12px;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 14px;
      transition: border-color 0.2s;
    }

    .form-input:focus {
      outline: none;
      border-color: #007AFF;
      box-shadow: 0 0 0 2px rgba(0, 122, 255, 0.1);
    }
  `]
})
export class CompleteGiftComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  completeButton!: ActionBarButton;
  platforms: PaymentPlatformOption[] = [];
  giftId: number | null = null;
  isCompleting = false;

  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private giftService = inject(GiftService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      this.giftId = params['giftId'] ? parseInt(params['giftId']) : null;
    });

    this.form = this.fb.group({
      paymentPlatformId: ['', Validators.required]
    });

    this.giftService.getPaymentPlatforms().subscribe({
      next: platforms => {
        this.platforms = platforms;
        if (platforms.length > 0) {
          this.form.patchValue({ paymentPlatformId: platforms[0].id });
        }
      },
      error: () => this.toastService.error('Failed to load payment platforms')
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/gift-log'])
    };

    this.updateCompleteButton();
    this.form.valueChanges.subscribe(() => this.updateCompleteButton());
  }

  private updateCompleteButton() {
    this.completeButton = {
      label: 'Complete Gift',
      type: 'primary',
      disabled: !this.form.valid || this.isCompleting || !this.giftId,
      onClick: () => this.onComplete()
    };
  }

  onComplete() {
    if (!this.form.valid || !this.giftId) return;

    this.isCompleting = true;
    const paymentPlatformId = this.form.get('paymentPlatformId')?.value;

    this.giftService.recordGift({
      amount: 0,
      completingGiftId: this.giftId,
      paymentPlatformId: paymentPlatformId
    }).subscribe({
      next: (response) => {
        if (response.success) {
          this.toastService.success('Gift completed successfully');
          this.router.navigate(['/app/crew/gift-log']);
        } else {
          this.toastService.error(response.message);
          this.isCompleting = false;
        }
      },
      error: (err) => {
        this.toastService.error(err?.error?.message || 'Failed to complete gift');
        this.isCompleting = false;
      }
    });
  }
}
