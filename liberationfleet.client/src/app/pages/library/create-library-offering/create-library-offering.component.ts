import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { LibraryCategoryPickerComponent } from '../../../components/library-category-picker/library-category-picker.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { GiftLogCryptoService } from '../../../services/crypto/gift-log-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { PendingAttachment } from '../../../models/proposal.model';
import { LibraryCategory, LibraryFulfillmentMode, LibraryOfferingKind } from '../../../models/library.model';
import { AuthService } from '../../../services/auth.service';
import { getUserIdFromToken } from '../../../utils/jwt.util';

@Component({
  selector: 'app-create-library-offering',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, ProposalAttachmentPickerComponent, LibraryCategoryPickerComponent],
  templateUrl: './create-library-offering.component.html',
  styleUrl: './create-library-offering.component.css'
})
export class CreateLibraryOfferingComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  attachments: PendingAttachment[] = [];
  categories: LibraryCategory[] = [];
  selectedCategoryIds: number[] = [];
  isSubmitting = false;
  crewId = 0;
  authorDisplayName = '';
  currentUserId: number | null = null;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private giftLogCrypto = inject(GiftLogCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private authService = inject(AuthService);
  private destroy$ = new Subject<void>();

  ngOnInit() {
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;

    const initialKind = this.parseKind(this.route.snapshot.queryParamMap.get('kind'));
    const initialFulfillment = initialKind === 'Durable'
      ? 'OnRequest'
      : this.parseFulfillment(this.route.snapshot.queryParamMap.get('fulfillment'));

    this.form = this.fb.group({
      offeringKind: [initialKind, Validators.required],
      fulfillmentMode: [{ value: initialFulfillment, disabled: initialKind === 'Durable' }, Validators.required],
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(10000)]],
      valuePerUnit: [null, [Validators.required, Validators.min(0.01)]],
      unitLabel: ['', [Validators.maxLength(64)]],
      quantity: [1, [Validators.required, Validators.min(1), Validators.max(100)]],
      quantityNotApplicable: [initialKind === 'Service']
    });

    this.applyKindRules(initialKind);

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/library-of-things'])
    };

    this.updateCreateButton();

    this.libraryService.getCategories().subscribe({
      next: categories => {
        this.categories = categories;
      },
      error: () => this.toastService.error('Failed to load categories')
    });

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
      }
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    this.form.get('offeringKind')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(kind => this.applyKindRules(kind as LibraryOfferingKind));

    this.form.get('quantityNotApplicable')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyQuantityFieldState());

    this.form.statusChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.updateCreateButton());
    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.updateCreateButton());
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get offeringKind(): LibraryOfferingKind {
    return this.form.get('offeringKind')?.value ?? 'Durable';
  }

  get fulfillmentMode(): LibraryFulfillmentMode {
    return this.form.getRawValue().fulfillmentMode ?? 'OnRequest';
  }

  get showFulfillmentMode(): boolean {
    return this.offeringKind !== 'Durable';
  }

  get showQuantityNotApplicable(): boolean {
    return this.offeringKind === 'Consumable';
  }

  get quantityLabel(): string {
    return this.offeringKind === 'Durable' ? 'Quantity (units)' : 'Stock quantity';
  }

  get quantityHint(): string {
    if (this.offeringKind === 'Durable') {
      return 'Each unit is listed separately and can be passed between crewmates.';
    }
    if (this.form.get('quantityNotApplicable')?.value) {
      return 'Quantity varies — requesters receive whatever is available.';
    }
    return 'One listing covers all stock; items are not passed around individually.';
  }

  onCategoriesChange(categoryIds: number[]) {
    this.selectedCategoryIds = categoryIds;
    this.updateCreateButton();
  }

  onSubmit() {
    if (this.form.invalid || this.isSubmitting || this.crewId <= 0 || this.selectedCategoryIds.length === 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    const raw = this.form.getRawValue();
    const offeringKind = raw.offeringKind as LibraryOfferingKind;
    const quantityNotApplicable = offeringKind === 'Service' || !!raw.quantityNotApplicable;
    const quantity = offeringKind === 'Durable'
      ? Number(raw.quantity)
      : quantityNotApplicable
        ? 1
        : Number(raw.quantity);

    void this.encryptionContent.whenReady().then(async () => {
      try {
        const encrypted = await this.libraryCrypto.encryptOfferingPayload(
          this.crewId,
          {
            title: raw.title.trim(),
            description: raw.description.trim(),
            authorDisplayName: this.authorDisplayName
          },
          this.attachments
        );

        this.libraryService.createOffering({
          title: raw.title.trim(),
          descriptionPreview: encrypted.descriptionPreview,
          categoryIds: [...this.selectedCategoryIds],
          valuePerUnit: Number(raw.valuePerUnit),
          unitLabel: raw.unitLabel?.trim() || null,
          quantity,
          quantityNotApplicable,
          thumbnailResourceId: encrypted.thumbnailResourceId,
          kind: offeringKind,
          fulfillmentMode: raw.fulfillmentMode as LibraryFulfillmentMode,
          nonce: encrypted.nonce,
          ciphertext: encrypted.ciphertext
        }).subscribe({
          next: result => {
            if (result.success) {
              void this.encryptGiftLogEntry(
                result.giftId,
                raw.title.trim(),
                Number(raw.valuePerUnit),
                quantity
              );
              this.toastService.success(result.message || 'Offering created');
              this.router.navigate([this.successRoute(offeringKind)]);
              return;
            }
            this.toastService.error(result.message || 'Failed to create offering');
            this.isSubmitting = false;
            this.updateCreateButton();
          },
          error: err => {
            this.toastService.error(err?.error?.message || err?.message || 'Failed to create offering');
            this.isSubmitting = false;
            this.updateCreateButton();
          }
        });
      } catch {
        this.toastService.error('Failed to encrypt offering content.');
        this.isSubmitting = false;
        this.updateCreateButton();
      }
    });
  }

  private applyKindRules(kind: LibraryOfferingKind) {
    const fulfillmentControl = this.form.get('fulfillmentMode');
    const quantityControl = this.form.get('quantity');
    const quantityNaControl = this.form.get('quantityNotApplicable');

    if (kind === 'Durable') {
      fulfillmentControl?.setValue('OnRequest');
      fulfillmentControl?.disable();
      quantityNaControl?.setValue(false);
      quantityNaControl?.disable();
      quantityControl?.enable();
      return;
    }

    fulfillmentControl?.enable();

    if (kind === 'Service') {
      quantityNaControl?.setValue(true);
      quantityNaControl?.disable();
      quantityControl?.disable();
      return;
    }

    quantityNaControl?.enable();
    this.applyQuantityFieldState();
  }

  private applyQuantityFieldState() {
    const quantityControl = this.form.get('quantity');
    if (this.offeringKind === 'Service') {
      quantityControl?.disable();
      return;
    }

    if (this.form.get('quantityNotApplicable')?.value) {
      quantityControl?.disable();
    } else {
      quantityControl?.enable();
    }
  }

  private successRoute(kind: LibraryOfferingKind): string {
    switch (kind) {
      case 'Consumable':
        return '/app/crew/library-of-things/consumable';
      case 'Service':
        return '/app/crew/library-of-things/services';
      default:
        return '/app/crew/library-of-things/durable';
    }
  }

  private parseKind(value: string | null): LibraryOfferingKind {
    if (value === 'Consumable' || value === 'Service') {
      return value;
    }
    return 'Durable';
  }

  private parseFulfillment(value: string | null): LibraryFulfillmentMode {
    return value === 'OnDemand' ? 'OnDemand' : 'OnRequest';
  }

  private async encryptGiftLogEntry(
    giftId: number | undefined,
    title: string,
    valuePerUnit: number,
    quantity: number
  ) {
    if (!giftId || !this.currentUserId) {
      return;
    }

    const totalValue = valuePerUnit * quantity;
    const message = `${this.authorDisplayName} contributed "${title}" to the library. Valued at $${totalValue}`;
    try {
      await this.giftLogCrypto.encryptAndStoreEntry({
        id: giftId,
        type: 'direct',
        giverId: this.currentUserId,
        giverName: this.authorDisplayName,
        recipientId: this.currentUserId,
        recipientName: this.authorDisplayName,
        amount: totalValue,
        platform: 'In-kind (Library)',
        timestamp: new Date(),
        message,
        relatedUserIds: [this.currentUserId],
        hasEncryptedContent: false
      }, this.crewId);
    } catch {
      // Gift log encryption is best-effort; the server gift record still exists.
    }
  }

  private updateCreateButton() {
    this.createButton = {
      label: 'Create',
      type: 'primary',
      disabled: this.isSubmitting || this.form.invalid || this.selectedCategoryIds.length === 0,
      onClick: () => this.onSubmit()
    };
  }
}
