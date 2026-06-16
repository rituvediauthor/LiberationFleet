import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { GiftService } from '../../services/gift.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import {
  CrewMember,
  PaymentPlatformOption,
  PendingMiddlemanGift,
  RecordGiftRequest
} from '../../models/gift.model';

@Component({
  selector: 'app-record-gift',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, ConfirmDialogComponent],
  templateUrl: './record-gift.component.html',
  styleUrl: './record-gift.component.css'
})
export class RecordGiftComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  logButton!: ActionBarButton;

  crewMembers: CrewMember[] = [];
  middlemanOptions: CrewMember[] = [];
  pendingGifts: PendingMiddlemanGift[] = [];
  platforms: PaymentPlatformOption[] = [];
  showConfirmDialog = false;
  isRecording = false;

  activeUser: CrewMember = { id: 0, username: '' };

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private giftService = inject(GiftService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.activeUser = { id: user.id, username: user.username };
      }
      this.refreshOptions();
    });

    this.form = this.fb.group({
      amount: ['', [Validators.required, Validators.min(0.01)]],
      recipientId: [''],
      useMiddleman: [false],
      middlemanId: [''],
      completingAsMiddleman: [false],
      pendingGiftId: [''],
      paymentPlatformId: ['', Validators.required]
    });

    this.giftService.getPaymentPlatforms().subscribe({
      next: platforms => {
        this.platforms = platforms;
        if (platforms.length > 0 && !this.form.get('paymentPlatformId')?.value) {
          this.form.patchValue({ paymentPlatformId: platforms[0].id }, { emitEvent: false });
        }
      },
      error: () => this.toastService.error('Failed to load payment platforms')
    });

    this.form.get('useMiddleman')?.valueChanges.subscribe(() => this.onUseMiddlemanChange());
    this.form.get('completingAsMiddleman')?.valueChanges.subscribe(() => this.onCompletingChange());
    this.form.get('recipientId')?.valueChanges.subscribe(() => this.refreshMiddlemanOptions());
    this.form.get('middlemanId')?.valueChanges.subscribe(() => this.refreshRecipientOptions());
    this.form.get('pendingGiftId')?.valueChanges.subscribe(pendingGiftId => this.onPendingGiftSelected(pendingGiftId));

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/gift-log'])
    };

    this.updateLogButton();

    this.form.statusChanges.subscribe(() => this.updateLogButton());
    this.form.valueChanges.subscribe(() => this.updateLogButton());
    this.updateValidators();
    this.refreshOptions();
  }

  get isCompletingAsMiddleman(): boolean {
    return !!this.form.get('completingAsMiddleman')?.value;
  }

  get useMiddleman(): boolean {
    return !!this.form.get('useMiddleman')?.value && !this.isCompletingAsMiddleman;
  }

  private refreshOptions() {
    if (!this.activeUser.id) {
      return;
    }

    this.giftService.getCrewMembers(this.activeUser.id).subscribe({
      next: members => {
        this.crewMembers = members;
        this.refreshMiddlemanOptions();
        this.refreshRecipientOptions();
      }
    });

    this.giftService.getPendingMiddlemanGifts().subscribe({
      next: gifts => {
        this.pendingGifts = gifts;
      }
    });
  }

  private refreshRecipientOptions() {
    if (!this.activeUser.id || !this.form) {
      return;
    }

    const middlemanId = Number(this.form.get('middlemanId')?.value);
    this.giftService.getCrewMembers(this.activeUser.id).subscribe({
      next: members => {
        this.crewMembers = members.filter(m => !middlemanId || m.id !== middlemanId);
      }
    });
  }

  private refreshMiddlemanOptions() {
    if (!this.activeUser.id || !this.form) {
      return;
    }

    const recipientId = Number(this.form.get('recipientId')?.value);
    this.giftService.getCrewMembers(this.activeUser.id).subscribe({
      next: members => {
        this.middlemanOptions = members.filter(m => !recipientId || m.id !== recipientId);
      }
    });
  }

  private onUseMiddlemanChange() {
    if (this.form.get('useMiddleman')?.value) {
      this.form.patchValue({ completingAsMiddleman: false, pendingGiftId: '' }, { emitEvent: false });
    } else {
      this.form.patchValue({ middlemanId: '' }, { emitEvent: false });
    }
    this.updateValidators();
    this.updateLogButton();
  }

  private onCompletingChange() {
    if (this.form.get('completingAsMiddleman')?.value) {
      this.form.patchValue({
        useMiddleman: false,
        recipientId: '',
        middlemanId: ''
      }, { emitEvent: false });
    } else {
      this.form.patchValue({ pendingGiftId: '' }, { emitEvent: false });
    }
    this.updateValidators();
    this.updateLogButton();
  }

  private onPendingGiftSelected(pendingGiftId: string | number) {
    if (!this.isCompletingAsMiddleman || !pendingGiftId) {
      return;
    }

    const pendingGift = this.pendingGifts.find(g => g.id === Number(pendingGiftId));
    if (!pendingGift) {
      return;
    }

    this.form.patchValue({
      amount: pendingGift.amount,
      paymentPlatformId: this.platforms.find(p => p.name === pendingGift.platform)?.id
        ?? this.form.get('paymentPlatformId')?.value
        ?? ''
    }, { emitEvent: true });
  }

  private updateValidators() {
    const completing = this.isCompletingAsMiddleman;
    const recipient = this.form.get('recipientId');
    const middleman = this.form.get('middlemanId');
    const pending = this.form.get('pendingGiftId');

    if (completing) {
      recipient?.clearValidators();
      middleman?.clearValidators();
      pending?.setValidators([Validators.required]);
    } else {
      recipient?.setValidators([Validators.required]);
      pending?.clearValidators();
      if (this.useMiddleman) {
        middleman?.setValidators([Validators.required]);
      } else {
        middleman?.clearValidators();
      }
    }

    recipient?.updateValueAndValidity({ emitEvent: false });
    middleman?.updateValueAndValidity({ emitEvent: false });
    pending?.updateValueAndValidity({ emitEvent: false });
    this.updateLogButton();
  }

  private updateLogButton() {
    const disabled = !this.form || this.form.invalid || this.isRecording;
    this.logButton = {
      label: 'Log Gift',
      type: 'primary',
      disabled,
      onClick: () => this.onLogGiftClick()
    };
  }

  onLogGiftClick() {
    if (this.form.invalid || this.isRecording) {
      return;
    }
    this.showConfirmDialog = true;
  }

  onConfirmLog() {
    this.showConfirmDialog = false;
    if (this.form.invalid || this.isRecording) {
      return;
    }

    const request = this.buildRecordGiftRequest();
    if (!request) {
      this.toastService.error('Please complete all required gift fields.');
      return;
    }

    this.isRecording = true;
    this.updateLogButton();

    this.giftService.recordGift(request).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Gift recorded');
          this.router.navigate(['/app/crew/gift-log']);
          return;
        }
        this.toastService.error(result.message || 'Failed to record gift');
        this.isRecording = false;
        this.updateLogButton();
      },
      error: error => {
        this.toastService.error(this.extractErrorMessage(error));
        this.isRecording = false;
        this.updateLogButton();
      }
    });
  }

  onDismissDialog() {
    this.showConfirmDialog = false;
  }

  private buildRecordGiftRequest(): RecordGiftRequest | null {
    const v = this.form.getRawValue();
    const amount = Number(v.amount);
    const paymentPlatformId = Number(v.paymentPlatformId);

    if (!amount || amount <= 0 || !paymentPlatformId) {
      return null;
    }

    if (this.isCompletingAsMiddleman) {
      const completingGiftId = Number(v.pendingGiftId);
      if (!completingGiftId) {
        return null;
      }

      return {
        amount,
        paymentPlatformId,
        completingGiftId
      };
    }

    const recipientId = Number(v.recipientId);
    if (!recipientId) {
      return null;
    }

    const middlemanId = this.useMiddleman && v.middlemanId ? Number(v.middlemanId) : undefined;

    return {
      amount,
      paymentPlatformId,
      recipientId,
      middlemanId
    };
  }

  private extractErrorMessage(error: { error?: { message?: string; errors?: Record<string, string[]> } }): string {
    const validationErrors = error.error?.errors;
    if (validationErrors) {
      const firstError = Object.values(validationErrors).flat()[0];
      if (firstError) {
        return firstError;
      }
    }

    return error.error?.message || 'Failed to record gift';
  }
}
