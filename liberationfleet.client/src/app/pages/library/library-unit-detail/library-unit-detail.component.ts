import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { LibraryImageCarouselComponent } from '../../../components/library-image-carousel/library-image-carousel.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { GiftLogCryptoService } from '../../../services/crypto/gift-log-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { LibraryUnitDetail } from '../../../models/library.model';
import { AuthService } from '../../../services/auth.service';
import { ProfileService } from '../../../services/profile.service';
import { getUserIdFromToken } from '../../../utils/jwt.util';

type ConfirmAction = 'confirmBroken' | 'reportFixed' | 'reportLost' | null;

@Component({
  selector: 'app-library-unit-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, LibraryImageCarouselComponent, ConfirmDialogComponent],
  templateUrl: './library-unit-detail.component.html',
  styleUrl: './library-unit-detail.component.css'
})
export class LibraryUnitDetailComponent implements OnInit {
  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;
  secondaryButton: ActionBarButton | null = null;
  form!: FormGroup;
  maintenanceForm!: FormGroup;
  brokenForm!: FormGroup;
  detail: LibraryUnitDetail | null = null;
  loading = true;
  errorMessage = '';
  isSubmitting = false;
  crewId = 0;
  unitId = 0;
  backSection = 'durable';
  authorDisplayName = '';
  currentUserId: number | null = null;
  confirmVisible = false;
  confirmAction: ConfirmAction = null;
  confirmTitle = '';
  confirmMessage = '';

  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private giftLogCrypto = inject(GiftLogCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);

  ngOnInit() {
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;

    this.backSection = this.route.snapshot.queryParamMap.get('from') ?? 'durable';

    this.form = this.fb.group({
      quantity: [1, [Validators.required, Validators.min(1)]],
      purpose: ['', [Validators.required, Validators.maxLength(5000)]],
      neededByStart: ['', Validators.required],
      neededByEnd: ['', Validators.required]
    });

    this.maintenanceForm = this.fb.group({
      cost: [null, [Validators.required, Validators.min(0.01)]],
      notes: ['', [Validators.required, Validators.maxLength(5000)]]
    });

    this.brokenForm = this.fb.group({
      explanation: ['', [Validators.required, Validators.maxLength(5000)]]
    });

    this.form.valueChanges.subscribe(() => this.updateActionButtons());
    this.form.statusChanges.subscribe(() => this.updateActionButtons());

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate([`/app/crew/library-of-things/${this.backSection}`])
    };

    this.unitId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.unitId) {
      this.loading = false;
      this.errorMessage = 'Invalid item.';
      return;
    }

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.loadDetail();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew membership.';
      }
    });
  }

  get carouselImages(): string[] {
    if (this.detail?.imageUrls?.length) {
      return this.detail.imageUrls;
    }
    return this.detail?.thumbnailUrl ? [this.detail.thumbnailUrl] : [];
  }

  get showRequestForm(): boolean {
    return !!this.detail?.viewer.canRequest;
  }

  get showAcquisitionForm(): boolean {
    return !!this.detail?.viewer.canRecordAcquisition;
  }

  get hasActiveRequest(): boolean {
    return !!this.detail?.viewer.activeRequestId;
  }

  get isService(): boolean {
    return this.detail?.offeringKind === 'Service';
  }

  get isDurable(): boolean {
    return this.detail?.offeringKind === 'Durable';
  }

  get showQuantityField(): boolean {
    return !this.isService && (this.showRequestForm || this.showAcquisitionForm);
  }

  get holderLabel(): string {
    if (this.detail?.offeringKind === 'Consumable' || this.detail?.offeringKind === 'Service') {
      return this.detail.offeringKind === 'Service' ? 'Offered by' : 'From';
    }
    return 'Holder';
  }

  openConfirm(action: ConfirmAction, title: string, message: string) {
    this.confirmAction = action;
    this.confirmTitle = title;
    this.confirmMessage = message;
    this.confirmVisible = true;
  }

  onConfirmDialog() {
    const action = this.confirmAction;
    this.confirmVisible = false;
    this.confirmAction = null;
    if (action === 'confirmBroken') {
      this.submitConfirmBroken();
    } else if (action === 'reportFixed') {
      this.submitReportFixed();
    } else if (action === 'reportLost') {
      this.submitReportLost();
    }
  }

  onDismissDialog() {
    this.confirmVisible = false;
    this.confirmAction = null;
  }

  submitReportBroken() {
    if (!this.detail || this.isSubmitting || this.brokenForm.invalid) {
      this.brokenForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.updateActionButtons();

    void this.encryptionContent.whenReady().then(async () => {
      try {
        const explanation = this.brokenForm.value.explanation as string;
        const encrypted = await this.libraryCrypto.encryptTextNote(this.crewId, explanation);
        this.libraryService.reportBroken(this.unitId, {
          explanationPreview: encrypted.preview,
          nonce: encrypted.nonce,
          ciphertext: encrypted.ciphertext
        }).subscribe({
          next: response => {
            this.isSubmitting = false;
            if (!response.success) {
              this.toastService.error(response.message || 'Failed to report broken');
              this.updateActionButtons();
              return;
            }
            this.toastService.success('Item reported as broken');
            this.brokenForm.reset();
            this.loadDetail();
          },
          error: err => {
            this.isSubmitting = false;
            this.toastService.error(err?.message ?? 'Failed to report broken');
            this.updateActionButtons();
          }
        });
      } catch (err: unknown) {
        this.isSubmitting = false;
        this.toastService.error(err instanceof Error ? err.message : 'Encryption failed');
        this.updateActionButtons();
      }
    });
  }

  submitMaintenance() {
    if (!this.detail || this.isSubmitting || this.maintenanceForm.invalid) {
      this.maintenanceForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.updateActionButtons();

    const cost = Number(this.maintenanceForm.value.cost);
    const notes = this.maintenanceForm.value.notes as string;

    void this.encryptionContent.whenReady().then(async () => {
      try {
        const encrypted = await this.libraryCrypto.encryptTextNote(this.crewId, notes);
        this.libraryService.recordMaintenance(this.unitId, {
          cost,
          nonce: encrypted.nonce,
          ciphertext: encrypted.ciphertext
        }).subscribe({
          next: response => {
            this.isSubmitting = false;
            if (!response.success) {
              this.toastService.error(response.message || 'Failed to record maintenance');
              this.updateActionButtons();
              return;
            }
            void this.encryptMaintenanceGift(response.giftId, cost, notes);
            this.toastService.success('Maintenance recorded');
            this.maintenanceForm.reset();
            this.updateActionButtons();
          },
          error: err => {
            this.isSubmitting = false;
            this.toastService.error(err?.message ?? 'Failed to record maintenance');
            this.updateActionButtons();
          }
        });
      } catch (err: unknown) {
        this.isSubmitting = false;
        this.toastService.error(err instanceof Error ? err.message : 'Encryption failed');
        this.updateActionButtons();
      }
    });
  }

  private submitConfirmBroken() {
    if (this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.libraryService.confirmBroken(this.unitId).subscribe({
      next: response => {
        this.isSubmitting = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to confirm broken');
          return;
        }
        this.toastService.success('Broken status confirmed');
        this.loadDetail();
      },
      error: err => {
        this.isSubmitting = false;
        this.toastService.error(err?.message ?? 'Failed to confirm broken');
      }
    });
  }

  private submitReportLost() {
    if (this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.libraryService.reportLost(this.unitId).subscribe({
      next: response => {
        this.isSubmitting = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to report lost');
          return;
        }
        this.toastService.success('Item reported lost');
        this.router.navigate([`/app/crew/library-of-things/${this.backSection}`]);
      },
      error: err => {
        this.isSubmitting = false;
        this.toastService.error(err?.message ?? 'Failed to report lost');
      }
    });
  }

  private submitReportFixed() {
    if (this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.libraryService.reportFixed(this.unitId).subscribe({
      next: response => {
        this.isSubmitting = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to report fixed');
          return;
        }
        this.toastService.success('Item reported as fixed');
        this.loadDetail();
      },
      error: err => {
        this.isSubmitting = false;
        this.toastService.error(err?.message ?? 'Failed to report fixed');
      }
    });
  }

  private async encryptMaintenanceGift(giftId: number | undefined, cost: number, notes: string) {
    if (!giftId || !this.currentUserId) {
      return;
    }

    const message = `${this.authorDisplayName} recorded $${cost} maintenance on "${this.detail?.title ?? 'library item'}". ${notes.trim().slice(0, 120)}`;
    try {
      await this.giftLogCrypto.encryptAndStoreEntry({
        id: giftId,
        type: 'direct',
        giverId: this.currentUserId,
        giverName: this.authorDisplayName,
        recipientId: this.currentUserId,
        recipientName: this.authorDisplayName,
        amount: cost,
        platform: 'In-kind (Library)',
        timestamp: new Date(),
        message,
        relatedUserIds: [this.currentUserId],
        hasEncryptedContent: false
      }, this.crewId);
    } catch {
      // Best-effort gift log encryption.
    }
  }

  private loadDetail() {
    this.loading = true;
    this.errorMessage = '';

    this.libraryService.getUnitDetail(this.unitId).subscribe({
      next: detail => {
        void this.applyDetail(detail);
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load item';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private async applyDetail(detail: LibraryUnitDetail) {
    try {
      await this.encryptionContent.whenReady();
      this.detail = await this.libraryCrypto.enrichUnitDetail(detail, this.crewId);
    } catch {
      this.detail = detail;
    }

    const maxQty = this.detail.viewer.maxRequestQuantity ?? this.detail.remainingStock ?? 1;
    const quantityControl = this.form.get('quantity');
    quantityControl?.setValidators([
      Validators.required,
      Validators.min(1),
      Validators.max(Math.max(1, maxQty))
    ]);
    quantityControl?.updateValueAndValidity({ emitEvent: false });

    if (this.isService) {
      quantityControl?.setValue(1, { emitEvent: false });
    }

    if (this.showAcquisitionForm) {
      const today = new Date().toISOString().slice(0, 10);
      this.form.patchValue({ neededByStart: today, neededByEnd: today }, { emitEvent: false });
    }

    this.updateActionButtons();
    this.loading = false;
  }

  private updateActionButtons() {
    if (this.detail?.viewer.canRequest) {
      this.primaryButton = {
        label: 'Request',
        type: 'primary',
        disabled: this.isSubmitting || this.form.invalid,
        onClick: () => this.submitRequest()
      };
      this.secondaryButton = null;
      return;
    }

    if (this.detail?.viewer.canRecordAcquisition) {
      this.primaryButton = {
        label: 'Record acquisition',
        type: 'primary',
        disabled: this.isSubmitting || this.form.invalid,
        onClick: () => this.submitAcquisition()
      };
      this.secondaryButton = null;
      return;
    }

    if (this.detail?.viewer.activeRequestId) {
      this.primaryButton = {
        label: 'View Request',
        type: 'primary',
        onClick: () => this.router.navigate([
          '/app/crew/library-of-things/requests',
          this.detail!.viewer.activeRequestId
        ])
      };
      this.secondaryButton = null;
      return;
    }

    this.primaryButton = {
      label: 'Request',
      type: 'primary',
      disabled: true,
      onClick: () => undefined
    };
    this.secondaryButton = null;
  }

  private submitRequest() {
    if (!this.detail || this.isSubmitting || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.updateActionButtons();

    void this.encryptionContent.whenReady().then(async () => {
      try {
        const purpose = this.form.value.purpose as string;
        const encrypted = await this.libraryCrypto.encryptRequestPurpose(this.crewId, purpose);
        const quantity = this.isService ? 1 : Number(this.form.value.quantity);
        const payload = {
          quantity,
          purposePreview: encrypted.purposePreview,
          neededByStart: this.toApiDate(this.form.value.neededByStart),
          neededByEnd: this.toApiDate(this.form.value.neededByEnd),
          nonce: encrypted.nonce,
          ciphertext: encrypted.ciphertext
        };

        this.libraryService.createRequest(this.unitId, payload).subscribe({
          next: response => {
            this.isSubmitting = false;
            if (!response.success) {
              this.toastService.error(response.message || 'Failed to submit request');
              this.updateActionButtons();
              return;
            }

            this.toastService.success('Request submitted');
            this.router.navigate(['/app/crew/library-of-things/requests', response.requestId]);
          },
          error: err => {
            this.isSubmitting = false;
            this.toastService.error(err?.message ?? 'Failed to submit request');
            this.updateActionButtons();
          }
        });
      } catch (err: unknown) {
        this.isSubmitting = false;
        this.toastService.error(err instanceof Error ? err.message : 'Encryption failed');
        this.updateActionButtons();
      }
    });
  }

  private submitAcquisition() {
    if (!this.detail || this.isSubmitting || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.updateActionButtons();

    void this.encryptionContent.whenReady().then(async () => {
      try {
        const purpose = this.form.value.purpose as string;
        const encrypted = await this.libraryCrypto.encryptRequestPurpose(this.crewId, purpose);
        const quantity = this.isService ? 1 : Number(this.form.value.quantity);
        const payload = {
          quantity,
          purposePreview: encrypted.purposePreview,
          nonce: encrypted.nonce,
          ciphertext: encrypted.ciphertext
        };

        this.libraryService.recordAcquisition(this.unitId, payload).subscribe({
          next: response => {
            this.isSubmitting = false;
            if (!response.success) {
              this.toastService.error(response.message || 'Failed to record acquisition');
              this.updateActionButtons();
              return;
            }

            this.toastService.success('Acquisition recorded');
            this.loadDetail();
          },
          error: err => {
            this.isSubmitting = false;
            this.toastService.error(err?.message ?? 'Failed to record acquisition');
            this.updateActionButtons();
          }
        });
      } catch (err: unknown) {
        this.isSubmitting = false;
        this.toastService.error(err instanceof Error ? err.message : 'Encryption failed');
        this.updateActionButtons();
      }
    });
  }

  private toApiDate(value: string): string {
    return new Date(`${value}T00:00:00.000Z`).toISOString();
  }
}
