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
  CrewMember,
  GiftRecordItem,
  PaymentPlatformOption,
  ReceptionOrderEntry
} from '../../models/gift.model';

interface EntryFormValue {
  amount: number | '';
  middlemanId: number | '';
  paymentPlatformId: number | '';
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

  receptionEntries: ReceptionOrderEntry[] = [];
  crewMembers: CrewMember[] = [];
  platforms: PaymentPlatformOption[] = [];
  showConfirmDialog = false;
  isRecording = false;
  loading = true;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private giftService = inject(GiftService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      customRecipientId: [''],
      customAmount: [''],
      customMiddlemanId: [''],
      customPaymentPlatformId: [''],
      entries: this.fb.array([])
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/gift-log'])
    };

    this.updateRecordButton();

    this.giftService.getPaymentPlatforms().subscribe({
      next: platforms => {
        this.platforms = platforms;
        this.loadReceptionOrder();
      },
      error: () => this.toastService.error('Failed to load payment platforms')
    });

    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.giftService.getCrewMembers(user.id).subscribe({
          next: members => this.crewMembers = members
        });
      }
    });

    this.form.statusChanges.subscribe(() => this.updateRecordButton());
    this.form.valueChanges.subscribe(() => this.updateRecordButton());
  }

  get entries(): FormArray {
    return this.form.get('entries') as FormArray;
  }

  entryTypeLabel(entry: ReceptionOrderEntry): string {
    return entry.entryType === 'survivalThreshold' ? 'Survival threshold' : 'Cycle';
  }

  needsMiddlemanSelector(entry: ReceptionOrderEntry): boolean {
    const hasDirect = entry.giverPlatformIds.some(id => entry.recipientPlatformIds.includes(id));
    return !hasDirect && entry.middlemanOptions.length > 0;
  }

  private loadReceptionOrder() {
    this.giftService.getReceptionOrder(30).subscribe({
      next: entries => {
        this.receptionEntries = entries;
        this.buildEntryForms(entries);
        this.loading = false;
        this.updateRecordButton();
      },
      error: () => {
        this.loading = false;
        this.toastService.error('Failed to load reception order');
      }
    });
  }

  private buildEntryForms(entries: ReceptionOrderEntry[]) {
    const defaultPlatformId = this.platforms[0]?.id ?? '';
    this.entries.clear();

    entries.forEach(entry => {
      const defaultMiddleman = entry.defaultMiddlemanId ?? '';
      this.entries.push(this.fb.group({
        amount: [''],
        middlemanId: [defaultMiddleman],
        paymentPlatformId: [defaultPlatformId]
      }));
    });

    if (this.platforms.length > 0) {
      this.form.patchValue({ customPaymentPlatformId: this.platforms[0].id }, { emitEvent: false });
    }
  }

  private updateRecordButton() {
    const disabled = this.isRecording || !this.hasGiftsToRecord();
    this.recordButton = {
      label: 'Record Gift(s)',
      type: 'primary',
      disabled,
      onClick: () => this.onRecordClick()
    };
  }

  private hasGiftsToRecord(): boolean {
    return this.buildGiftItems().length > 0;
  }

  onRecordClick() {
    if (!this.hasGiftsToRecord() || this.isRecording) return;
    this.showConfirmDialog = true;
  }

  onConfirmRecord() {
    this.showConfirmDialog = false;
    const gifts = this.buildGiftItems();
    if (gifts.length === 0 || this.isRecording) return;

    this.isRecording = true;
    this.updateRecordButton();

    this.giftService.recordGifts(gifts).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Gifts recorded');
          this.router.navigate(['/app/crew/gift-log']);
          return;
        }
        this.toastService.error(result.message || 'Failed to record gifts');
        this.isRecording = false;
        this.updateRecordButton();
      },
      error: error => {
        this.toastService.error(error?.error?.message || 'Failed to record gifts');
        this.isRecording = false;
        this.updateRecordButton();
      }
    });
  }

  onDismissDialog() {
    this.showConfirmDialog = false;
  }

  private buildGiftItems(): GiftRecordItem[] {
    const items: GiftRecordItem[] = [];
    const formValue = this.form.getRawValue();

    const customAmount = Number(formValue.customAmount);
    const customRecipientId = Number(formValue.customRecipientId);
    const customPlatformId = Number(formValue.customPaymentPlatformId);
    if (customAmount > 0 && customRecipientId > 0 && customPlatformId > 0) {
      const customMiddlemanId = Number(formValue.customMiddlemanId);
      items.push({
        amount: customAmount,
        paymentPlatformId: customPlatformId,
        recipientId: customRecipientId,
        middlemanId: customMiddlemanId > 0 ? customMiddlemanId : undefined,
        isCustom: true
      });
    }

    this.receptionEntries.forEach((entry, index) => {
      const row = formValue.entries[index] as EntryFormValue;
      const amount = Number(row.amount);
      const paymentPlatformId = Number(row.paymentPlatformId);
      if (amount <= 0 || !paymentPlatformId) return;

      const middlemanId = Number(row.middlemanId);
      items.push({
        amount,
        paymentPlatformId,
        recipientId: entry.userId,
        middlemanId: middlemanId > 0 ? middlemanId : undefined,
        isCustom: false,
        entryType: entry.entryType
      });
    });

    return items;
  }
}
