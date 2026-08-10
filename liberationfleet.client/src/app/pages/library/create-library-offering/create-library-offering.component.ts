import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { Subject, takeUntil } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { CharCounterComponent } from '../../../components/char-counter/char-counter.component';
import { LibraryCategoryPickerComponent } from '../../../components/library-category-picker/library-category-picker.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { PendingAttachment } from '../../../models/proposal.model';
import { LibraryCategory, LibraryFulfillmentMode, LibraryOfferingKind, LibraryOfferingVisibility } from '../../../models/library.model';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';
import { pendingAttachmentsAllowSubmit } from '../../../utils/pending-attachment.util';

@Component({
  selector: 'app-create-library-offering',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageLayoutComponent,
    ProposalAttachmentPickerComponent,
    LibraryCategoryPickerComponent,
    CharCounterComponent,
    ConfirmDialogComponent
  ],
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
  canAttachFiles = false;
  authorDisplayName = '';
  durableNoticeVisible = false;
  private durableNoticeShown = false;
  readonly durableNoticeMessage =
    'Listing a durable item does not count as a gift to the crew until another crewmate requests and acquires it. This prevents inflating priority scores by listing items nobody needs.';
  readonly titleMaxLength = 200;
  readonly descriptionMaxLength = 10000;
  readonly unitLabelMaxLength = 64;

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private route = inject(ActivatedRoute);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private destroy$ = new Subject<void>();

  ngOnInit() {
    const initialKind = this.parseKind(this.route.snapshot.queryParamMap.get('kind'));
    const initialFulfillment = initialKind === 'Durable'
      ? 'OnRequest'
      : initialKind === 'Digital'
        ? 'OnDemand'
        : this.parseFulfillment(this.route.snapshot.queryParamMap.get('fulfillment'));

    this.form = this.fb.group({
      offeringKind: [initialKind, Validators.required],
      fulfillmentMode: [{ value: initialFulfillment, disabled: initialKind === 'Durable' || initialKind === 'Digital' }, Validators.required],
      visibility: ['CrewOnly' as LibraryOfferingVisibility, Validators.required],
      title: ['', [Validators.required, Validators.maxLength(this.titleMaxLength)]],
      description: ['', [Validators.required, Validators.maxLength(this.descriptionMaxLength)]],
      valuePerUnit: [null, [Validators.required, Validators.min(0.01)]],
      unitLabel: ['', [Validators.maxLength(this.unitLabelMaxLength)]],
      quantity: [1, [Validators.required, Validators.min(1), Validators.max(100)]],
      quantityNotApplicable: [initialKind === 'Service' || initialKind === 'Digital']
    });

    this.applyKindRules(initialKind);
    if (initialKind === 'Durable') {
      this.showDurableNotice();
    }

    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things']);

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
        this.canAttachFiles = membership.canAttachFilesToCrewContent ?? false;
      }
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    this.form.get('offeringKind')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(kind => {
        const offeringKind = kind as LibraryOfferingKind;
        this.applyKindRules(offeringKind);
        if (offeringKind === 'Durable') {
          this.showDurableNotice();
        }
      });

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

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
  }

  get fulfillmentMode(): LibraryFulfillmentMode {
    return this.form.getRawValue().fulfillmentMode ?? 'OnRequest';
  }

  get showFulfillmentMode(): boolean {
    return this.offeringKind !== 'Durable' && this.offeringKind !== 'Digital';
  }

  get showQuantityNotApplicable(): boolean {
    return this.offeringKind === 'Consumable';
  }

  get valueLabel(): string {
    return this.offeringKind === 'Digital' ? 'Value per download ($)' : 'Value per unit ($)';
  }

  dismissDurableNotice() {
    this.durableNoticeVisible = false;
  }

  private showDurableNotice() {
    if (this.durableNoticeShown) {
      return;
    }
    this.durableNoticeShown = true;
    this.durableNoticeVisible = true;
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
    const raw = this.form.getRawValue();
    const offeringKind = raw.offeringKind as LibraryOfferingKind;
    if (offeringKind === 'Digital') {
      if (!this.canAttachFiles) {
        this.toastService.error('File attachment permission is required to list digital goods.');
        return;
      }
      if (this.attachments.length === 0) {
        this.toastService.error('Add at least one downloadable file.');
        return;
      }
    }
    if (!pendingAttachmentsAllowSubmit(this.attachments)) {
      this.toastService.error('Wait for attachments to finish processing, or cancel them.');
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    const quantityNotApplicable = offeringKind === 'Service'
      || offeringKind === 'Digital'
      || !!raw.quantityNotApplicable;
    const quantity = offeringKind === 'Durable'
      ? Number(raw.quantity)
      : quantityNotApplicable
        ? 1
        : Number(raw.quantity);
    const fulfillmentMode = offeringKind === 'Digital'
      ? 'OnDemand'
      : raw.fulfillmentMode as LibraryFulfillmentMode;

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
          fulfillmentMode,
          visibility: raw.visibility as LibraryOfferingVisibility,
          nonce: encrypted.nonce,
          ciphertext: encrypted.ciphertext
        }).subscribe({
          next: result => {
            if (result.success) {
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

    if (kind === 'Digital') {
      fulfillmentControl?.setValue('OnDemand');
      fulfillmentControl?.disable();
      quantityNaControl?.setValue(true);
      quantityNaControl?.disable();
      quantityControl?.disable();
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
    if (this.offeringKind === 'Service' || this.offeringKind === 'Digital') {
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
      case 'Digital':
        return '/app/crew/library-of-things/digital';
      default:
        return '/app/crew/library-of-things/durable';
    }
  }

  onAttachmentsChange() {
    this.updateCreateButton();
  }

  private parseKind(value: string | null): LibraryOfferingKind {
    if (value === 'Consumable' || value === 'Service' || value === 'Digital') {
      return value;
    }
    return 'Durable';
  }

  private parseFulfillment(value: string | null): LibraryFulfillmentMode {
    return value === 'OnDemand' ? 'OnDemand' : 'OnRequest';
  }

  private updateCreateButton() {
    const digitalBlocked = this.offeringKind === 'Digital'
      && (!this.canAttachFiles || this.attachments.length === 0);
    this.createButton = {
      label: 'Create',
      type: 'primary',
      disabled: this.isSubmitting
        || this.form.invalid
        || this.selectedCategoryIds.length === 0
        || digitalBlocked
        || !pendingAttachmentsAllowSubmit(this.attachments),
      onClick: () => this.onSubmit()
    };
  }
}
