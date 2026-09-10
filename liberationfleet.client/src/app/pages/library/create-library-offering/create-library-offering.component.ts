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
import { LibraryTierAudienceDialogComponent } from '../../../components/library-tier-audience-dialog/library-tier-audience-dialog.component';
import { ZipCodeListEditorComponent } from '../../../components/zip-code-list-editor/zip-code-list-editor.component';
import { CountrySelectComponent } from '../../../components/country-select/country-select.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { FleetService } from '../../../services/fleet.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { PendingAttachment } from '../../../models/proposal.model';
import {
  LibraryCategory,
  LibraryFulfillmentMode,
  LibraryOfferingKind,
  LibraryOfferingVisibility,
  LibraryPriorityTierAudienceMember
} from '../../../models/library.model';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';
import { TextFieldLimits } from '../../../utils/text-field-limits';
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
    ConfirmDialogComponent,
    LibraryTierAudienceDialogComponent,
    CountrySelectComponent,
    ZipCodeListEditorComponent
  ],
  templateUrl: './create-library-offering.component.html',
  styleUrl: './create-library-offering.component.css'
})
export class CreateLibraryOfferingComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  attachments: PendingAttachment[] = [];
  detailAttachments: PendingAttachment[] = [];
  downloadAttachments: PendingAttachment[] = [];
  categories: LibraryCategory[] = [];
  selectedCategoryIds: number[] = [];
  isSubmitting = false;
  crewId = 0;
  fleetId: number | null = null;
  canAttachFiles = false;
  authorDisplayName = '';
  /** Scope-average tier counts (fleet when in a fleet). */
  scopeTierCounts: number[] = [0, 0, 0, 0, 0, 0];
  /** Home-crew members only (same average as scope). */
  homeCrewTierCounts: number[] = [0, 0, 0, 0, 0, 0];
  /** Fleet-wide member tier counts when the crew is in a fleet. */
  fleetTierCounts: number[] = [0, 0, 0, 0, 0, 0];
  hasFleet = false;
  readonly stockTierNumbers = [1, 2, 3, 4, 5, 6];
  readonly minimumViewerTierOptions = [1, 2, 3, 4, 5, 6];
  /** Bound for template so visibility/min-tier changes always refresh the hint. */
  audienceCustomerHint = '';
  audienceDialogOpen = false;
  audienceDialogLoading = false;
  audienceDialogError: string | null = null;
  audienceDialogTitle = 'Who can see this';
  audienceDialogSubtitle: string | null = null;
  audienceDialogItems: LibraryPriorityTierAudienceMember[] = [];
  audienceDialogCrewId: number | null = null;
  audienceDialogFleetId: number | null = null;
  durableNoticeVisible = false;
  private durableNoticeShown = false;
  private audienceLoadSeq = 0;
  readonly durableNoticeMessage =
    'Listing a durable item does not count as a gift to the crew until another crewmate requests and acquires it. This prevents inflating priority scores by listing items nobody needs.';
  readonly titleMaxLength = TextFieldLimits.title;
  readonly descriptionMaxLength = TextFieldLimits.longBody;
  readonly unitLabelMaxLength = TextFieldLimits.shortLabel;

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private route = inject(ActivatedRoute);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
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
      countryCode: [null as string | null],
      allowedZipCodes: [[] as string[]],
      title: ['', [Validators.required, Validators.maxLength(this.titleMaxLength)]],
      description: ['', [Validators.required, Validators.maxLength(this.descriptionMaxLength)]],
      valuePerUnit: [null, [Validators.required, Validators.min(0.01)]],
      unitLabel: ['', [Validators.maxLength(this.unitLabelMaxLength)]],
      quantity: [1, [Validators.required, Validators.min(1), Validators.max(100)]],
      quantityNotApplicable: [initialKind === 'Service' || initialKind === 'Digital'],
      stockTier1: [0, [Validators.min(0), Validators.max(100)]],
      stockTier2: [0, [Validators.min(0), Validators.max(100)]],
      stockTier3: [0, [Validators.min(0), Validators.max(100)]],
      stockTier4: [0, [Validators.min(0), Validators.max(100)]],
      stockTier5: [0, [Validators.min(0), Validators.max(100)]],
      stockTier6: [0, [Validators.min(0), Validators.max(100)]],
      minimumViewerTier: [1, [Validators.required, Validators.min(1), Validators.max(6)]]
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

    this.crewService.getCurrentCrew().subscribe({
      next: result => {
        if (!result.success || !result.crew) {
          return;
        }
        this.scopeTierCounts = this.normalizeTierCounts(result.crew.libraryPriorityTierCounts);
        this.homeCrewTierCounts = this.normalizeTierCounts(
          result.crew.homeCrewLibraryPriorityTierCounts ?? result.crew.libraryPriorityTierCounts);
        if (!this.hasFleet) {
          this.fleetTierCounts = [...this.scopeTierCounts];
        }
        this.refreshAudienceHints();
      }
    });

    this.fleetService.getCurrent().subscribe({
      next: result => {
        if (!result.success || !result.fleet) {
          this.hasFleet = false;
          this.fleetId = null;
          this.fleetTierCounts = [...this.homeCrewTierCounts];
          this.refreshAudienceHints();
          return;
        }
        this.hasFleet = true;
        this.fleetId = result.fleet.id;
        this.fleetTierCounts = this.normalizeTierCounts(result.fleet.libraryPriorityTierCounts);
        this.refreshAudienceHints();
      },
      error: () => {
        this.hasFleet = false;
        this.fleetId = null;
        this.fleetTierCounts = [...this.homeCrewTierCounts];
        this.refreshAudienceHints();
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
        this.refreshAudienceHints();
      });

    this.form.get('visibility')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.refreshAudienceHints());

    this.form.get('minimumViewerTier')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.refreshAudienceHints());

    this.form.get('quantityNotApplicable')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyQuantityFieldState());

    this.form.statusChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.updateCreateButton());
    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => this.updateCreateButton());
    this.refreshAudienceHints();
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

  onCountryCodeChange(code: string | null) {
    this.form.patchValue({ countryCode: code });
    this.form.get('countryCode')?.markAsTouched();
    this.updatePostalCountryValidators();
    this.updateCreateButton();
  }

  onAllowedZipCodesChange(zips: string[]) {
    const patch: { allowedZipCodes: string[]; countryCode?: string | null } = { allowedZipCodes: zips };
    if (zips.length === 0) {
      patch.countryCode = null;
    }
    this.form.patchValue(patch);
    this.updatePostalCountryValidators();
    this.updateCreateButton();
  }

  private updatePostalCountryValidators() {
    const country = this.form.get('countryCode');
    const zips = this.form.get('allowedZipCodes')?.value as string[] | undefined;
    if ((zips?.length ?? 0) > 0) {
      country?.setValidators([Validators.required]);
    } else {
      country?.clearValidators();
    }
    country?.updateValueAndValidity({ emitEvent: false });
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

  get showPerTierStock(): boolean {
    return this.offeringKind === 'Consumable' && !this.form.get('quantityNotApplicable')?.value;
  }

  get showMinimumViewerTier(): boolean {
    return this.offeringKind === 'Service';
  }

  get visibility(): LibraryOfferingVisibility {
    return this.form.get('visibility')?.value ?? 'CrewOnly';
  }

  get audienceTierCounts(): number[] {
    if (this.visibility === 'FleetWide' && this.hasFleet) {
      return this.fleetTierCounts;
    }
    return this.homeCrewTierCounts;
  }

  get audienceLabel(): string {
    return this.visibility === 'FleetWide' && this.hasFleet ? 'fleet-mates' : 'crewmates';
  }

  customersForTier(tier: number): number {
    const index = Math.min(6, Math.max(1, tier)) - 1;
    return this.audienceTierCounts[index] ?? 0;
  }

  /** Visible when viewer tier >= minimum (selected tier and all higher tiers). */
  customersForMinimumTier(minimumTier: number): number {
    const min = Math.min(6, Math.max(1, minimumTier));
    return this.audienceTierCounts
      .slice(min - 1)
      .reduce((sum, count) => sum + (count || 0), 0);
  }

  tierAudienceLabel(tier: number): string {
    if (tier >= 4) {
      return 'crewmates';
    }
    return this.hasFleet ? 'fleet-mates (excl. your crew)' : 'fleet-mates';
  }

  tierBandHint(tier: number): string {
    return tier <= 3
      ? 'Fleet-mates outside your crew'
      : 'Crewmates of your crew';
  }

  get selectedMinimumViewerTier(): number {
    return Number(this.form.get('minimumViewerTier')?.value) || 1;
  }

  viewTierAudience(tier: number, matchMode: 'Exact' | 'MinimumOrHigher' = 'Exact'): void {
    const seq = ++this.audienceLoadSeq;
    const visibility = this.visibility;
    this.audienceDialogOpen = true;
    this.audienceDialogLoading = true;
    this.audienceDialogError = null;
    this.audienceDialogItems = [];
    this.audienceDialogCrewId = this.crewId > 0 ? this.crewId : null;
    this.audienceDialogFleetId = visibility === 'FleetWide' && this.hasFleet ? this.fleetId : null;
    this.audienceDialogTitle = matchMode === 'MinimumOrHigher'
      ? `Tier ${tier}+ eligible members`
      : `Tier ${tier} ${this.tierAudienceLabel(tier)}`;
    this.audienceDialogSubtitle = matchMode === 'MinimumOrHigher'
      ? `People who can see a service set to Tier ${tier} or higher (${visibility === 'FleetWide' && this.hasFleet ? 'fleet-wide' : 'crew only'}). Tier 1–3 = fleet-mates excl. your crew; Tier 4–6 = crewmates. Blocked mates are excluded.`
      : `People in Tier ${tier} who would see this stock pool (${this.tierBandHint(tier)}; ${visibility === 'FleetWide' && this.hasFleet ? 'fleet-wide' : 'crew only'}). Blocked mates are excluded.`;

    this.libraryService.getPriorityTierAudience({
      visibility,
      tier,
      matchMode
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        if (seq !== this.audienceLoadSeq) {
          return;
        }
        this.audienceDialogLoading = false;
        if (!result.success) {
          this.audienceDialogError = result.message || 'Failed to load audience';
          return;
        }
        this.audienceDialogItems = result.items ?? [];
        this.audienceDialogCrewId = result.crewId ?? this.audienceDialogCrewId;
        this.audienceDialogFleetId = result.fleetId ?? this.audienceDialogFleetId;
      },
      error: err => {
        if (seq !== this.audienceLoadSeq) {
          return;
        }
        this.audienceDialogLoading = false;
        this.audienceDialogError = err?.error?.message || err?.message || 'Failed to load audience';
      }
    });
  }

  closeAudienceDialog(): void {
    this.audienceLoadSeq++;
    this.audienceDialogOpen = false;
    this.audienceDialogLoading = false;
    this.audienceDialogError = null;
    this.audienceDialogItems = [];
  }

  private refreshAudienceHints(): void {
    const minTier = this.selectedMinimumViewerTier;
    const count = this.customersForMinimumTier(minTier);
    this.audienceCustomerHint =
      `Number of customers: ${count} ${this.audienceLabel} at Tier ${minTier} or higher`;
  }

  get tierStockTotal(): number {
    const raw = this.form.getRawValue();
    return this.stockTierNumbers.reduce((sum, tier) => sum + (Number(raw[`stockTier${tier}`]) || 0), 0);
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
      if (this.downloadAttachments.length === 0) {
        this.toastService.error('Add at least one downloadable file.');
        return;
      }
    }
    const submitAttachments = offeringKind === 'Digital'
      ? this.buildDigitalAttachments()
      : this.attachments;
    if (!pendingAttachmentsAllowSubmit(submitAttachments)) {
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
      : offeringKind === 'Consumable' && !quantityNotApplicable
        ? this.tierStockTotal
        : quantityNotApplicable
          ? 1
          : Number(raw.quantity);
    const fulfillmentMode = offeringKind === 'Digital'
      ? 'OnDemand'
      : raw.fulfillmentMode as LibraryFulfillmentMode;

    if (offeringKind === 'Consumable' && !quantityNotApplicable && this.tierStockTotal <= 0) {
      this.toastService.error('Set stock for at least one priority tier.');
      return;
    }

    void this.encryptionContent.whenReady().then(async () => {
      try {
        const encrypted = await this.libraryCrypto.encryptOfferingPayload(
          this.crewId,
          {
            title: raw.title.trim(),
            description: raw.description.trim(),
            authorDisplayName: this.authorDisplayName
          },
          submitAttachments
        );

        this.libraryService.createOffering({
          title: raw.title.trim(),
          descriptionPreview: encrypted.descriptionPreview,
          categoryIds: [...this.selectedCategoryIds],
          valuePerUnit: Number(raw.valuePerUnit),
          unitLabel: raw.unitLabel?.trim() || null,
          quantity,
          quantityNotApplicable,
          stockTier1: offeringKind === 'Consumable' && !quantityNotApplicable ? Number(raw.stockTier1) || 0 : null,
          stockTier2: offeringKind === 'Consumable' && !quantityNotApplicable ? Number(raw.stockTier2) || 0 : null,
          stockTier3: offeringKind === 'Consumable' && !quantityNotApplicable ? Number(raw.stockTier3) || 0 : null,
          stockTier4: offeringKind === 'Consumable' && !quantityNotApplicable ? Number(raw.stockTier4) || 0 : null,
          stockTier5: offeringKind === 'Consumable' && !quantityNotApplicable ? Number(raw.stockTier5) || 0 : null,
          stockTier6: offeringKind === 'Consumable' && !quantityNotApplicable ? Number(raw.stockTier6) || 0 : null,
          minimumViewerTier: offeringKind === 'Service' ? Number(raw.minimumViewerTier) || 1 : 1,
          thumbnailResourceId: encrypted.thumbnailResourceId,
          kind: offeringKind,
          fulfillmentMode,
          visibility: raw.visibility as LibraryOfferingVisibility,
          countryCode: (raw.allowedZipCodes?.length ?? 0) > 0 ? (raw.countryCode as string) : null,
          allowedZipCodes: [...(raw.allowedZipCodes ?? [])],
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
    const minTierControl = this.form.get('minimumViewerTier');

    if (kind === 'Durable') {
      fulfillmentControl?.setValue('OnRequest');
      fulfillmentControl?.disable();
      quantityNaControl?.setValue(false);
      quantityNaControl?.disable();
      minTierControl?.disable();
      this.applyQuantityFieldState();
      return;
    }

    if (kind === 'Digital') {
      fulfillmentControl?.setValue('OnDemand');
      fulfillmentControl?.disable();
      quantityNaControl?.setValue(true);
      quantityNaControl?.disable();
      quantityControl?.disable();
      minTierControl?.disable();
      this.applyQuantityFieldState();
      return;
    }

    fulfillmentControl?.enable();

    if (kind === 'Service') {
      quantityNaControl?.setValue(true);
      quantityNaControl?.disable();
      quantityControl?.disable();
      minTierControl?.enable();
      this.applyQuantityFieldState();
      return;
    }

    quantityNaControl?.enable();
    minTierControl?.disable();
    this.applyQuantityFieldState();
  }

  private applyQuantityFieldState() {
    const quantityControl = this.form.get('quantity');
    const tierControls = this.stockTierNumbers.map(t => this.form.get(`stockTier${t}`));
    if (this.offeringKind === 'Service' || this.offeringKind === 'Digital') {
      quantityControl?.disable();
      tierControls.forEach(c => c?.disable());
      return;
    }

    if (this.offeringKind === 'Consumable') {
      quantityControl?.disable();
      if (this.form.get('quantityNotApplicable')?.value) {
        tierControls.forEach(c => c?.disable());
      } else {
        tierControls.forEach(c => c?.enable());
      }
      return;
    }

    tierControls.forEach(c => c?.disable());
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

  private buildDigitalAttachments(): PendingAttachment[] {
    const detail = this.detailAttachments.map(a => ({ ...a, role: 'detail' as const }));
    const downloads = this.downloadAttachments.map(a => ({ ...a, role: 'download' as const }));
    return [...detail, ...downloads];
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

  private normalizeTierCounts(counts: number[] | null | undefined): number[] {
    const normalized = [0, 0, 0, 0, 0, 0];
    if (!counts?.length) {
      return normalized;
    }
    for (let i = 0; i < Math.min(6, counts.length); i++) {
      normalized[i] = Number(counts[i]) || 0;
    }
    return normalized;
  }

  private updateCreateButton() {
    const submitAttachments = this.offeringKind === 'Digital'
      ? this.buildDigitalAttachments()
      : this.attachments;
    const digitalBlocked = this.offeringKind === 'Digital'
      && (!this.canAttachFiles || this.downloadAttachments.length === 0);
      this.createButton = {
      label: 'Create',
      type: 'primary',
      disabled: this.isSubmitting
        || this.form.invalid
        || this.selectedCategoryIds.length === 0
        || digitalBlocked
        || (this.showPerTierStock && this.tierStockTotal <= 0)
        || !pendingAttachmentsAllowSubmit(submitAttachments),
      onClick: () => this.onSubmit()
    };
  }
}

