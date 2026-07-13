import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { GiftService } from '../../services/gift.service';
import { CrewService } from '../../services/crew.service';
import { ProfileService } from '../../services/profile.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import {
  CrewMember,
  GiftRecordItem,
  MiddlemanOption,
  PaymentPlatformOption,
  PlatformAccount,
  ReceptionOrderEntry
} from '../../models/gift.model';

interface PlatformInfoLabel {
  prefix: 'Preferred' | 'Selected';
  name: string;
  handle: string;
}

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
  giverPlatformIds: number[] = [];
  activeUserId = 0;
  showConfirmDialog = false;
  isRecording = false;
  loading = true;

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private giftService = inject(GiftService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
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

    this.backButton = this.navigation.createBackButton(['/app/crew/gift-log']);

    this.updateRecordButton();

    this.crewService.getPaymentPlatforms().subscribe({
      next: platforms => {
        this.platforms = platforms;
        if (this.receptionEntries.length > 0) {
          this.buildEntryForms(this.receptionEntries);
        }
        this.updateRecordButton();
      },
      error: () => this.toastService.error('Failed to load payment platforms')
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.giverPlatformIds = profile.paymentPlatforms.map(p => p.platformId).filter(id => id > 0);
        this.activeUserId = profile.id;
        if (this.receptionEntries.length > 0) {
          this.receptionEntries = this.receptionEntries.filter(e => e.userId !== this.activeUserId);
          this.buildEntryForms(this.receptionEntries);
        }
        this.giftService.getCrewMembers(profile.id).subscribe({
          next: members => {
            this.crewMembers = members;
            this.updateRecordButton();
          }
        });
        this.updateRecordButton();
      },
      error: () => this.toastService.error('Failed to load profile')
    });

    this.authService.currentUser$.subscribe(user => {
      if (user?.id) {
        this.activeUserId = user.id;
      }
    });

    this.loadReceptionOrder();

    this.form.statusChanges.subscribe(() => this.updateRecordButton());
    this.form.valueChanges.subscribe(() => this.updateRecordButton());
  }

  get entries(): FormArray {
    return this.form.get('entries') as FormArray;
  }

  entryTypeLabel(entry: ReceptionOrderEntry): string {
    if (entry.entryType === 'survivalThreshold') {
      return 'Survival threshold';
    }
    if (entry.entryType === 'catchUp') {
      return 'Catch-up';
    }
    return 'Cycle';
  }

  platformsForEntry(entry: ReceptionOrderEntry): PaymentPlatformOption[] {
    const commonIds = entry.commonPlatformIds ?? [];
    return this.platforms.filter(p => commonIds.includes(p.id));
  }

  selectedMiddlemanOption(entry: ReceptionOrderEntry, index: number): MiddlemanOption | undefined {
    const middlemanId = Number(this.entries.at(index)?.get('middlemanId')?.value);
    if (!middlemanId) {
      return undefined;
    }
    return entry.middlemanOptions.find(mm => mm.userId === middlemanId);
  }

  middlemanPlatformOptions(entry: ReceptionOrderEntry, index: number): PaymentPlatformOption[] {
    const option = this.selectedMiddlemanOption(entry, index);
    if (!option?.commonPlatformIds?.length) {
      return [];
    }
    return this.platforms.filter(p => option.commonPlatformIds.includes(p.id));
  }

  platformOptionsForEntry(entry: ReceptionOrderEntry, index: number): PaymentPlatformOption[] {
    const direct = this.platformsForEntry(entry);
    if (direct.length > 0) {
      return direct;
    }
    return this.middlemanPlatformOptions(entry, index);
  }

  customCommonPlatforms(): PaymentPlatformOption[] {
    const recipientId = Number(this.form.get('customRecipientId')?.value);
    if (!recipientId) {
      return [];
    }
    const recipient = this.crewMembers.find(m => m.id === recipientId);
    if (!recipient) {
      return [];
    }
    const commonIds = this.giverPlatformIds.filter(id => recipient.platformIds.includes(id));
    return this.platforms.filter(p => commonIds.includes(p.id));
  }

  needsMiddlemanSelector(entry: ReceptionOrderEntry): boolean {
    return (entry.commonPlatformIds?.length ?? 0) === 0 && (entry.middlemanOptions?.length ?? 0) > 0;
  }

  hasNoViableMeans(entry: ReceptionOrderEntry): boolean {
    return entry.noSuitableMiddleman;
  }

  platformInfoLabel(entry: ReceptionOrderEntry, index: number): PlatformInfoLabel | null {
    const platformId = Number(this.entries.at(index)?.get('paymentPlatformId')?.value);
    if (platformId > 0) {
      const selected = this.lookupPlatformAccount(entry, index, platformId);
      if (selected) {
        return { prefix: 'Selected', name: selected.name, handle: selected.handle };
      }
      const platform = this.platforms.find(p => p.id === platformId);
      if (platform) {
        return { prefix: 'Selected', name: platform.name, handle: '' };
      }
    }

    if (entry.recipientPreferredPlatformName && entry.recipientPreferredPlatformHandle) {
      return {
        prefix: 'Preferred',
        name: entry.recipientPreferredPlatformName,
        handle: entry.recipientPreferredPlatformHandle
      };
    }

    return null;
  }

  private lookupPlatformAccount(
    entry: ReceptionOrderEntry,
    index: number,
    platformId: number
  ): PlatformAccount | undefined {
    if (this.needsMiddlemanSelector(entry)) {
      return this.selectedMiddlemanOption(entry, index)?.platformAccounts
        ?.find(account => account.platformId === platformId);
    }
    return entry.recipientPlatformAccounts?.find(account => account.platformId === platformId);
  }

  onMiddlemanChange(entry: ReceptionOrderEntry, index: number) {
    const group = this.entries.at(index);
    if (!group) {
      return;
    }
    const options = this.middlemanPlatformOptions(entry, index);
    group.patchValue({ paymentPlatformId: options[0]?.id ?? '' }, { emitEvent: true });
  }

  private loadReceptionOrder() {
    this.giftService.getReceptionOrder(30).subscribe({
      next: entries => {
        this.receptionEntries = this.activeUserId > 0
          ? entries.filter(e => e.userId !== this.activeUserId)
          : entries;
        this.buildEntryForms(this.receptionEntries);
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
    this.entries.clear();

    entries.forEach(entry => {
      const commonPlatforms = this.platformsForEntry(entry);
      const defaultMiddleman = entry.defaultMiddlemanId ?? '';
      let defaultPlatformId: number | '' = commonPlatforms[0]?.id ?? '';
      if (!defaultPlatformId && defaultMiddleman) {
        const middleman = entry.middlemanOptions.find(mm => mm.userId === defaultMiddleman);
        defaultPlatformId = middleman?.commonPlatformIds[0] ?? '';
      }
      this.entries.push(this.fb.group({
        amount: [''],
        middlemanId: [defaultMiddleman],
        paymentPlatformId: [defaultPlatformId]
      }));
    });
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

  onCustomRecipientChange() {
    const common = this.customCommonPlatforms();
    const defaultId = common[0]?.id ?? '';
    this.form.patchValue({ customPaymentPlatformId: defaultId }, { emitEvent: true });
  }

  onAddNonCrewmate() {
    this.router.navigate(['/app/crew/gift-log/record/add-non-crewmate']);
  }

  private resolvePlatformId(entry: ReceptionOrderEntry, index: number, row: EntryFormValue): number {
    let paymentPlatformId = Number(row.paymentPlatformId);
    if (paymentPlatformId > 0) {
      return paymentPlatformId;
    }

    const direct = this.platformsForEntry(entry);
    if (direct.length > 0) {
      return direct[0]?.id ?? 0;
    }

    const middlemanId = Number(row.middlemanId);
    if (middlemanId <= 0) {
      return 0;
    }

    const middleman = entry.middlemanOptions.find(mm => mm.userId === middlemanId);
    return middleman?.commonPlatformIds[0] ?? 0;
  }

  private buildGiftItems(): GiftRecordItem[] {
    const items: GiftRecordItem[] = [];
    const formValue = this.form.getRawValue();

    const customAmount = Number(formValue.customAmount);
    const customRecipientId = Number(formValue.customRecipientId);
    const customPlatformId = Number(formValue.customPaymentPlatformId);
    if (customAmount > 0 && customRecipientId > 0 && customPlatformId > 0 && customRecipientId !== this.activeUserId) {
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
      if (entry.userId === this.activeUserId) return;

      const row = formValue.entries[index] as EntryFormValue;
      const amount = Number(row.amount);
      if (amount <= 0) return;

      const needsMiddleman = this.needsMiddlemanSelector(entry);
      const middlemanId = Number(row.middlemanId);
      if (needsMiddleman && middlemanId <= 0) return;

      const paymentPlatformId = this.resolvePlatformId(entry, index, row);
      if (!paymentPlatformId) return;

      items.push({
        amount,
        paymentPlatformId,
        recipientId: entry.userId,
        middlemanId: middlemanId > 0 ? middlemanId : undefined,
        isCustom: false,
        entryType: entry.entryType,
        seasonCycleId: entry.seasonCycleId
      });
    });

    return items;
  }
}
