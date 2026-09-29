import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { ZipCodeListEditorComponent } from '../../../components/zip-code-list-editor/zip-code-list-editor.component';
import { CountrySelectComponent } from '../../../components/country-select/country-select.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { LibraryOfferingListItem, LibraryOfferingVisibility, UpdateLibraryOfferingRequest } from '../../../models/library.model';
import { PendingAttachment, ProposalEncryptedPayload } from '../../../models/proposal.model';
import { pendingAttachmentsAllowSubmit } from '../../../utils/pending-attachment.util';
import { valuesEqual } from '../../../utils/save-button.util';

@Component({
  selector: 'app-edit-library-offering',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PageLayoutComponent,
    ConfirmDialogComponent,
    ProposalAttachmentPickerComponent,
    CountrySelectComponent,
    ZipCodeListEditorComponent
  ],
  templateUrl: './edit-library-offering.component.html',
  styleUrl: './edit-library-offering.component.css'
})
export class EditLibraryOfferingComponent implements OnInit {
  readonly stockTierNumbers = [1, 2, 3, 4, 5, 6];
  readonly minimumViewerTierOptions = [1, 2, 3, 4, 5, 6];

  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  deleteButton!: ActionBarButton;
  offering: LibraryOfferingListItem | null = null;
  isOutOfStock = false;
  visibility: LibraryOfferingVisibility = 'CrewOnly';
  countryCode: string | null = null;
  allowedZipCodes: string[] = [];
  countryTouched = false;
  stockTiers: Record<number, number> = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0, 6: 0 };
  initialStockTiers: Record<number, number> = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0, 6: 0 };
  minimumViewerTier = 1;
  initialMinimumViewerTier = 1;
  initialIsOutOfStock = false;
  initialVisibility: LibraryOfferingVisibility = 'CrewOnly';
  initialCountryCode: string | null = null;
  initialAllowedZipCodes: string[] = [];
  loading = true;
  saving = false;
  deleting = false;
  errorMessage = '';
  offeringId = 0;
  crewId = 0;
  canAttachFiles = false;
  confirmDeleteVisible = false;
  downloadAttachments: PendingAttachment[] = [];
  existingDownloadNames: string[] = [];
  private existingPayload: ProposalEncryptedPayload | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);

  ngOnInit() {
    this.offeringId = Number(this.route.snapshot.paramMap.get('id'));
    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things/mine']);
    this.updateActionButtons();

    if (!this.offeringId) {
      this.loading = false;
      this.errorMessage = 'Invalid offering.';
      return;
    }

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.canAttachFiles = membership.canAttachFilesToCrewContent ?? false;
        this.loadOffering();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew membership.';
      }
    });
  }

  get isStockBased(): boolean {
    return this.offering?.offeringKind === 'Consumable'
      || this.offering?.offeringKind === 'Service'
      || this.offering?.offeringKind === 'Digital';
  }

  get isDigital(): boolean {
    return this.offering?.offeringKind === 'Digital';
  }

  get isService(): boolean {
    return this.offering?.offeringKind === 'Service';
  }

  get showPerTierStock(): boolean {
    return this.offering?.offeringKind === 'Consumable' && !this.offering?.quantityNotApplicable;
  }

  get canToggleOutOfStock(): boolean {
    return !!this.offering?.quantityNotApplicable && this.isStockBased;
  }

  get showTrackedStockNotice(): boolean {
    return this.isStockBased && !this.offering?.quantityNotApplicable && !this.showPerTierStock;
  }

  get tierStockTotal(): number {
    return this.stockTierNumbers.reduce((sum, tier) => sum + (this.stockTiers[tier] || 0), 0);
  }

  get hasChanges(): boolean {
    if (!this.offering) {
      return false;
    }

    const visibilityChanged = this.visibility !== this.initialVisibility;
    const countryChanged = this.countryCode !== this.initialCountryCode;
    const zipChanged = !valuesEqual(this.allowedZipCodes, this.initialAllowedZipCodes);
    const stockChanged = this.canToggleOutOfStock && this.isOutOfStock !== this.initialIsOutOfStock;
    const tierStockChanged = this.showPerTierStock
      && this.stockTierNumbers.some(tier => (this.stockTiers[tier] || 0) !== (this.initialStockTiers[tier] || 0));
    const minTierChanged = this.isService && this.minimumViewerTier !== this.initialMinimumViewerTier;
    const filesChanged = this.isDigital && this.downloadAttachments.length > 0;
    return visibilityChanged
      || countryChanged
      || zipChanged
      || stockChanged
      || tierStockChanged
      || minTierChanged
      || filesChanged;
  }

  get countryRequired(): boolean {
    return this.allowedZipCodes.length > 0;
  }

  get countryInvalid(): boolean {
    return this.countryRequired && !this.countryCode && this.countryTouched;
  }

  get canSave(): boolean {
    if (!this.hasChanges || (this.countryRequired && !this.countryCode)) {
      return false;
    }
    if (this.showPerTierStock) {
      return this.stockTierNumbers.every(tier => {
        const value = this.stockTiers[tier] ?? 0;
        return value >= 0 && value <= 100;
      });
    }
    return true;
  }

  toggleOutOfStock() {
    if (!this.canToggleOutOfStock) {
      return;
    }
    this.isOutOfStock = !this.isOutOfStock;
    this.updateActionButtons();
  }

  onVisibilityChange() {
    this.updateActionButtons();
  }

  onCountryCodeChange(code: string | null) {
    this.countryCode = code;
    this.countryTouched = true;
    this.updateActionButtons();
  }

  onAllowedZipCodesChange(zips: string[]) {
    this.allowedZipCodes = zips;
    if (zips.length === 0) {
      this.countryCode = null;
    }
    this.updateActionButtons();
  }

  onStockTierChange() {
    this.updateActionButtons();
  }

  onMinimumViewerTierChange() {
    this.updateActionButtons();
  }

  onDownloadsChange() {
    this.updateActionButtons();
  }

  openDeleteConfirm() {
    this.confirmDeleteVisible = true;
  }

  dismissDeleteConfirm() {
    this.confirmDeleteVisible = false;
  }

  confirmDelete() {
    this.confirmDeleteVisible = false;
    this.deleteOffering();
  }

  private loadOffering() {
    this.loading = true;
    this.errorMessage = '';

    this.libraryService.getOfferingForEdit(this.offeringId).subscribe({
      next: offering => {
        if (offering.offeringKind === 'Durable') {
          this.loading = false;
          this.errorMessage =
            'Durable listings are managed from each unit’s page (broken, lost, maintenance). Title and value stay fixed after create.';
          return;
        }

        this.offering = offering;
        this.isOutOfStock = !!offering.isOutOfStock;
        this.initialIsOutOfStock = this.isOutOfStock;
        this.visibility = offering.visibility === 'FleetWide' ? 'FleetWide' : 'CrewOnly';
        this.initialVisibility = this.visibility;
        this.countryCode = offering.countryCode ?? null;
        this.initialCountryCode = this.countryCode;
        this.allowedZipCodes = [...(offering.allowedZipCodes ?? [])];
        this.initialAllowedZipCodes = [...this.allowedZipCodes];
        this.minimumViewerTier = offering.minimumViewerTier ?? 1;
        this.initialMinimumViewerTier = this.minimumViewerTier;
        for (const tier of this.stockTierNumbers) {
          const key = `remainingStockTier${tier}` as keyof LibraryOfferingListItem;
          const value = Number(offering[key] ?? 0) || 0;
          this.stockTiers[tier] = value;
          this.initialStockTiers[tier] = value;
        }
        this.updateActionButtons();

        if (offering.offeringKind === 'Digital' && this.crewId > 0) {
          void this.loadExistingDigitalPayload(offering.offeringId);
          return;
        }

        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load offering';
      }
    });
  }

  private async loadExistingDigitalPayload(offeringId: number) {
    try {
      await this.encryptionContent.whenReady();
      this.existingPayload = await this.libraryCrypto.loadOfferingPayload(offeringId, this.crewId);
      this.existingDownloadNames = (this.existingPayload?.attachments ?? [])
        .filter(attachment => attachment.role === 'download' || (!attachment.role && attachment.type !== 'image'))
        .map(attachment => attachment.fileName || 'Download file');
    } catch {
      this.existingPayload = null;
    } finally {
      this.loading = false;
      this.updateActionButtons();
    }
  }

  private save() {
    if (!this.offering || this.saving || !this.canSave) {
      return;
    }

    if (this.countryRequired && !this.countryCode) {
      this.countryTouched = true;
      this.updateActionButtons();
      return;
    }

    if (this.isDigital && this.downloadAttachments.length > 0) {
      if (!this.canAttachFiles) {
        this.toastService.error('File attachment permission is required to replace digital files.');
        return;
      }
      if (!pendingAttachmentsAllowSubmit(this.downloadAttachments)) {
        this.toastService.error('Wait for attachments to finish processing, or cancel them.');
        return;
      }
    }

    this.saving = true;
    this.updateActionButtons();
    void this.saveAsync();
  }

  private async saveAsync() {
    if (!this.offering) {
      return;
    }

    const payload: UpdateLibraryOfferingRequest = {
      visibility: this.visibility,
      countryCode: this.allowedZipCodes.length > 0 ? this.countryCode : null,
      allowedZipCodes: [...this.allowedZipCodes]
    };
    if (this.canToggleOutOfStock) {
      payload.isOutOfStock = this.isOutOfStock;
    }
    if (this.showPerTierStock) {
      payload.stockTier1 = this.stockTiers[1] || 0;
      payload.stockTier2 = this.stockTiers[2] || 0;
      payload.stockTier3 = this.stockTiers[3] || 0;
      payload.stockTier4 = this.stockTiers[4] || 0;
      payload.stockTier5 = this.stockTiers[5] || 0;
      payload.stockTier6 = this.stockTiers[6] || 0;
    }
    if (this.isService) {
      payload.minimumViewerTier = this.minimumViewerTier;
    }

    try {
      if (this.isDigital && this.downloadAttachments.length > 0 && this.crewId > 0) {
        const existingDetail = (this.existingPayload?.attachments ?? [])
          .filter(attachment => attachment.role === 'detail' || (!attachment.role && attachment.type === 'image'));
        const encrypted = await this.libraryCrypto.encryptOfferingPayload(
          this.crewId,
          {
            title: this.existingPayload?.title || this.offering.title,
            description: this.existingPayload?.description || this.offering.descriptionPreview,
            authorDisplayName: this.existingPayload?.authorDisplayName || ''
          },
          this.downloadAttachments.map(attachment => ({ ...attachment, role: 'download' })),
          existingDetail
        );
        payload.nonce = encrypted.nonce;
        payload.ciphertext = encrypted.ciphertext;
        payload.thumbnailResourceId = encrypted.thumbnailResourceId;
        payload.keyVersion = 1;
      }

      this.libraryService.updateOffering(this.offeringId, payload).subscribe({
        next: response => {
          this.saving = false;
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to update offering');
            this.updateActionButtons();
            return;
          }

          this.toastService.success('Offering updated');
          this.router.navigate(['/app/crew/library-of-things/mine']);
        },
        error: err => {
          this.saving = false;
          this.toastService.error(err?.message ?? 'Failed to update offering');
          this.updateActionButtons();
        }
      });
    } catch (err: unknown) {
      this.saving = false;
      const message = err instanceof Error ? err.message : 'Failed to update offering';
      this.toastService.error(message);
      this.updateActionButtons();
    }
  }

  private deleteOffering() {
    if (!this.offering || this.deleting || !this.isStockBased) {
      return;
    }

    this.deleting = true;
    this.updateActionButtons();

    this.libraryService.deleteOffering(this.offeringId).subscribe({
      next: response => {
        this.deleting = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to delete offering');
          this.updateActionButtons();
          return;
        }

        this.toastService.success('Offering deleted');
        this.router.navigate(['/app/crew/library-of-things/mine']);
      },
      error: err => {
        this.deleting = false;
        this.toastService.error(err?.message ?? 'Failed to delete offering');
        this.updateActionButtons();
      }
    });
  }

  private updateActionButtons() {
    this.saveButton = {
      label: 'Save',
      type: 'primary',
      disabled: this.saving || this.deleting || !this.canSave,
      onClick: () => this.save()
    };

    this.deleteButton = {
      label: 'Delete',
      type: 'secondary',
      disabled: this.saving || this.deleting || !this.isStockBased,
      onClick: () => this.openDeleteConfirm()
    };
  }
}
