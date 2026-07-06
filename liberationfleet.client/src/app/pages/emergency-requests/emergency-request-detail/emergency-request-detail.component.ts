import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { EmergencyRequestService } from '../../../services/emergency-request.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EmergencyRequestDetail } from '../../../models/emergency-request.model';
import { PaymentPlatformOption } from '../../../models/gift.model';

type ResponseMode = 'recordGift' | 'splitCycle';

@Component({
  selector: 'app-emergency-request-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './emergency-request-detail.component.html',
  styleUrl: './emergency-request-detail.component.css'
})
export class EmergencyRequestDetailComponent implements OnInit {
  request: EmergencyRequestDetail | null = null;
  loading = true;
  errorMessage = '';
  submitting = false;
  responseMode: ResponseMode = 'recordGift';
  platforms: PaymentPlatformOption[] = [];
  giverPlatformIds: number[] = [];
  backButton!: ActionBarButton;
  submitButton!: ActionBarButton;
  form!: FormGroup;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  private emergencyRequestService = inject(EmergencyRequestService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private requestId = 0;

  ngOnInit() {
    this.form = this.fb.group({
      amount: [''],
      paymentPlatformId: [''],
      middlemanId: ['']
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/emergency-requests'])
    };

    this.updateSubmitButton();
    this.form.valueChanges.subscribe(() => this.updateSubmitButton());

    this.requestId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.requestId) {
      this.loading = false;
      this.errorMessage = 'Invalid emergency request.';
      return;
    }

    this.crewService.getPaymentPlatforms().subscribe({
      next: platforms => {
        this.platforms = platforms;
      }
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.giverPlatformIds = profile.paymentPlatforms.map(p => p.platformId).filter(id => id > 0);
      }
    });

    this.loadDetail();
  }

  setResponseMode(mode: ResponseMode) {
    this.responseMode = mode;
    this.updateSubmitButton();
  }

  platformOptions(): PaymentPlatformOption[] {
    if (!this.request) return [];
    const commonIds = this.request.commonPlatforms
      .filter(p => p.isSharedWithViewer)
      .map(p => p.platformId);
    return this.platforms.filter(p => commonIds.includes(p.id));
  }

  needsMiddleman(): boolean {
    return this.platformOptions().length === 0 && (this.request?.middlemanOptions.length ?? 0) > 0;
  }

  onAlreadyLogged() {
    if (!this.request || this.submitting || this.request.isSelfRequest) return;
    const amount = Number(this.form.get('amount')?.value);
    if (amount <= 0) {
      this.toastService.error('Enter an amount for the gift you already logged.');
      return;
    }

    this.submitting = true;
    this.updateSubmitButton();
    this.emergencyRequestService.markAlreadyLogged(this.requestId, amount).subscribe({
      next: result => this.handleSubmitResult(result),
      error: error => this.handleSubmitError(error)
    });
  }

  private loadDetail() {
    this.emergencyRequestService.getDetail(this.requestId).subscribe({
      next: response => {
        if (!response.success || !response.request) {
          this.errorMessage = response.message || 'Emergency request not found.';
          this.request = null;
        } else {
          this.request = response.request;
          const defaultPlatform = this.platformOptions()[0]?.id ?? '';
          this.form.patchValue({ paymentPlatformId: defaultPlatform });
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load emergency request.';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private updateSubmitButton() {
    const disabled = this.submitting
      || !this.request
      || this.request.isSelfRequest
      || this.request.status !== 'Open'
      || !this.canSubmit();
    this.submitButton = {
      label: this.responseMode === 'splitCycle' ? 'Submit Split' : 'Record Gift',
      type: 'primary',
      disabled,
      onClick: () => this.onSubmit()
    };
  }

  private canSubmit(): boolean {
    const amount = Number(this.form.get('amount')?.value);
    if (amount <= 0) return false;
    if (this.responseMode === 'splitCycle') return true;

    if (this.needsMiddleman()) {
      return Number(this.form.get('middlemanId')?.value) > 0;
    }

    return Number(this.form.get('paymentPlatformId')?.value) > 0;
  }

  private onSubmit() {
    if (!this.canSubmit() || !this.request || this.submitting) return;

    const amount = Number(this.form.get('amount')?.value);
    this.submitting = true;
    this.updateSubmitButton();

    if (this.responseMode === 'splitCycle') {
      this.emergencyRequestService.splitCycle(this.requestId, amount).subscribe({
        next: result => this.handleSubmitResult(result),
        error: error => this.handleSubmitError(error)
      });
      return;
    }

    const paymentPlatformId = Number(this.form.get('paymentPlatformId')?.value);
    const middlemanId = Number(this.form.get('middlemanId')?.value);
    this.emergencyRequestService.recordGift(
      this.requestId,
      amount,
      paymentPlatformId,
      middlemanId > 0 ? middlemanId : undefined
    ).subscribe({
      next: result => this.handleSubmitResult(result),
      error: error => this.handleSubmitError(error)
    });
  }

  private handleSubmitResult(result: { success: boolean; message?: string }) {
    if (result.success) {
      this.toastService.success(result.message || 'Saved');
      this.router.navigate(['/app/crew/emergency-requests']);
      return;
    }
    this.toastService.error(result.message || 'Failed to save');
    this.submitting = false;
    this.updateSubmitButton();
  }

  private handleSubmitError(error: { error?: { message?: string } }) {
    this.toastService.error(error?.error?.message || 'Failed to save');
    this.submitting = false;
    this.updateSubmitButton();
  }
}
