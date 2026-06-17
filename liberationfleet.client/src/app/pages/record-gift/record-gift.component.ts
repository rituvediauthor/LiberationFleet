import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { GiftService } from '../../services/gift.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import {
  RecipientNeed,
  PaymentPlatformOption,
  RecordGiftRequest,
  CrewMember
} from '../../models/gift.model';

interface RecipientFormEntry {
  recipient: RecipientNeed;
  amount: number;
  middlemanId?: number;
}

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
  recordButton!: ActionBarButton;

  recipients: RecipientNeed[] = [];
  platforms: PaymentPlatformOption[] = [];
  crewMembers: CrewMember[] = [];
  showConfirmDialog = false;
  isRecording = false;
  loading = true;

  activeUserId = 0;
  customRecipientId: number | null = null;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private giftService = inject(GiftService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.activeUserId = user.id;
        this.loadData();
      }
    });

    this.form = this.fb.group({
      customAmount: [''],
      customRecipientId: [''],
      customPaymentPlatformId: [''],
      recipientGifts: this.fb.array([])
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/gift-log'])
    };

    this.updateRecordButton();
    this.form.valueChanges.subscribe(() => this.updateRecordButton());
  }

  private loadData() {
    this.loading = true;

    this.giftService.getPaymentPlatforms().subscribe({
      next: platforms => {
        this.platforms = platforms;
      },
      error: () => this.toastService.error('Failed to load payment platforms')
    });

    this.giftService.getCrewMembers(this.activeUserId).subscribe({
      next: members => {
        this.crewMembers = members;
      },
      error: () => this.toastService.error('Failed to load crew members')
    });

    this.giftService.getReceptionOrder(30).subscribe({
      next: response => {
        if (response.success) {
          this.recipients = response.recipients;
          this.initializeRecipientForms();
        } else {
          this.toastService.error(response.message);
        }
        this.loading = false;
      },
      error: () => {
        this.toastService.error('Failed to load reception order');
        this.loading = false;
      }
    });
  }

  private initializeRecipientForms() {
    const recipientArray = this.form.get('recipientGifts') as FormArray;
    recipientArray.clear();

    this.recipients.forEach(recipient => {
      recipientArray.push(this.fb.group({
        recipientId: [recipient.userId],
        amount: [''],
        paymentPlatformId: [this.platforms.length > 0 ? this.platforms[0].id : ''],
        middlemanId: [recipient.suggestedMiddlemanId || ''],
        isSurvivalThreshold: [recipient.isSurvivalThreshold]
      }));
    });
  }

  get recipientGifts(): FormArray {
    return this.form.get('recipientGifts') as FormArray;
  }

  getRecipientFormGroup(index: number): FormGroup {
    return this.recipientGifts.at(index) as FormGroup;
  }

  private updateRecordButton() {
    const hasEntries = this.hasGiftsToRecord();
    this.recordButton = {
      label: 'Record Gift(s)',
      type: 'primary',
      disabled: !hasEntries || this.isRecording,
      onClick: () => this.openConfirmDialog()
    };
  }

  private hasGiftsToRecord(): boolean {
    const customAmount = this.form.get('customAmount')?.value;
    if (customAmount && customAmount > 0) {
      return true;
    }

    for (let i = 0; i < this.recipientGifts.length; i++) {
      const group = this.recipientGifts.at(i) as FormGroup;
      const amount = group.get('amount')?.value;
      if (amount && amount > 0) {
        return true;
      }
    }

    return false;
  }

  openConfirmDialog() {
    this.showConfirmDialog = true;
  }

  onDismissDialog() {
    this.showConfirmDialog = false;
  }

  async onConfirmLog() {
    this.showConfirmDialog = false;
    this.isRecording = true;

    const giftsToRecord: RecordGiftRequest[] = [];

    const customAmount = this.form.get('customAmount')?.value;
    const customRecipientId = this.form.get('customRecipientId')?.value;
    const customPlatformId = this.form.get('customPaymentPlatformId')?.value;

    if (customAmount && customAmount > 0 && customRecipientId && customPlatformId) {
      giftsToRecord.push({
        amount: parseFloat(customAmount),
        recipientId: parseInt(customRecipientId),
        paymentPlatformId: parseInt(customPlatformId),
        isSurvivalThreshold: false
      });
    }

    for (let i = 0; i < this.recipientGifts.length; i++) {
      const group = this.recipientGifts.at(i) as FormGroup;
      const amount = group.get('amount')?.value;
      
      if (amount && amount > 0) {
        const recipientId = group.get('recipientId')?.value;
        const paymentPlatformId = group.get('paymentPlatformId')?.value;
        const middlemanId = group.get('middlemanId')?.value;
        const isSurvivalThreshold = group.get('isSurvivalThreshold')?.value;

        giftsToRecord.push({
          amount: parseFloat(amount),
          recipientId: parseInt(recipientId),
          paymentPlatformId: parseInt(paymentPlatformId),
          middlemanId: middlemanId ? parseInt(middlemanId) : undefined,
          isSurvivalThreshold: isSurvivalThreshold
        });
      }
    }

    let successCount = 0;
    let failCount = 0;

    for (const gift of giftsToRecord) {
      try {
        await this.giftService.recordGift(gift).toPromise();
        successCount++;
      } catch (error) {
        failCount++;
      }
    }

    this.isRecording = false;

    if (successCount > 0) {
      this.toastService.success(`Recorded ${successCount} gift(s)`);
      this.router.navigate(['/app/crew/gift-log']);
    }

    if (failCount > 0) {
      this.toastService.error(`Failed to record ${failCount} gift(s)`);
    }
  }

  getMiddlemanName(recipient: RecipientNeed): string {
    return recipient.suggestedMiddlemanName || 'Unknown';
  }
}
