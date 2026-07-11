import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { LibraryItemCardComponent } from '../../../components/library-item-card/library-item-card.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { GiftLogCryptoService } from '../../../services/crypto/gift-log-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { LibraryRequestDetail } from '../../../models/library.model';
import { NavigationService } from '../../../services/navigation.service';
import { NotificationContentService } from '../../../services/notification-content.service';

@Component({
  selector: 'app-library-request-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, LibraryItemCardComponent],
  templateUrl: './library-request-detail.component.html',
  styleUrl: './library-request-detail.component.css'
})
export class LibraryRequestDetailComponent implements OnInit {
  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;
  secondaryButton: ActionBarButton | null = null;
  form!: FormGroup;
  detail: LibraryRequestDetail | null = null;
  loading = true;
  errorMessage = '';
  isSubmitting = false;
  crewId = 0;
  requestId = 0;

  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private notificationContent = inject(NotificationContentService);
  private navigation = inject(NavigationService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private giftLogCrypto = inject(GiftLogCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);

  ngOnInit() {
    this.form = this.fb.group({
      purpose: ['', [Validators.required, Validators.maxLength(5000)]],
      neededByStart: ['', Validators.required],
      neededByEnd: ['', Validators.required]
    });

    this.form.valueChanges.subscribe(() => this.updateButtons());
    this.form.statusChanges.subscribe(() => this.updateButtons());

    this.requestId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.requestId) {
      this.loading = false;
      this.errorMessage = 'Invalid request.';
      return;
    }

    this.notificationContent.markVisited(
      `/app/crew/library-of-things/requests/${this.requestId}`,
      this.requestId
    );

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

  get cardItem() {
    if (!this.detail) {
      return null;
    }

    return this.libraryCrypto.toListItem({
      unitId: this.detail.unitId,
      offeringId: this.detail.offeringId,
      holderUserId: this.detail.isPossessorView ? this.detail.requesterUserId : this.detail.holderUserId,
      holderUsername: this.detail.isPossessorView ? this.detail.requesterUsername : this.detail.holderUsername,
      title: this.detail.title,
      descriptionPreview: this.detail.descriptionPreview,
      categories: this.detail.categories,
      thumbnailResourceId: this.detail.thumbnailResourceId,
      thumbnailUrl: this.detail.thumbnailUrl,
      hasEncryptedContent: this.detail.hasEncryptedContent,
      unitStatus: '',
      valuePerUnit: 0,
      unitLabel: null,
      viewer: { isHolder: false, canRequest: false }
    });
  }

  get holderLabel(): string {
    return this.detail?.isPossessorView ? 'Requester' : 'From';
  }

  get pageTitle(): string {
    return this.detail?.isPossessorView ? 'Incoming Request' : 'My Request';
  }

  private loadDetail() {
    this.loading = true;
    this.errorMessage = '';

    this.libraryService.getRequestDetail(this.requestId).subscribe({
      next: detail => {
        void this.applyDetail(detail);
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load request';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private async applyDetail(detail: LibraryRequestDetail) {
    let enriched: LibraryRequestDetail;
    try {
      await this.encryptionContent.whenReady();
      enriched = await this.libraryCrypto.enrichRequestDetail(detail, this.crewId);
    } catch {
      enriched = detail;
    }

    this.detail = enriched;

    this.backButton = this.navigation.createBackButton([
      enriched.isPossessorView
        ? '/app/crew/library-of-things/requests'
        : '/app/crew/library-of-things/requests/mine'
    ]);

    this.form.patchValue({
      purpose: enriched.fullPurpose ?? enriched.purposePreview,
      neededByStart: this.toInputDate(enriched.neededByStart),
      neededByEnd: this.toInputDate(enriched.neededByEnd)
    });

    if (!enriched.canEdit) {
      this.form.disable();
    } else {
      this.form.enable();
    }

    this.updateButtons();
    this.loading = false;
  }

  private updateButtons() {
    if (!this.detail) {
      return;
    }

    if (this.detail.canComplete) {
      this.primaryButton = {
        label: 'Complete',
        type: 'primary',
        disabled: this.isSubmitting,
        onClick: () => this.completeRequest()
      };
    } else if (this.detail.canEdit) {
      this.primaryButton = {
        label: 'Save',
        type: 'primary',
        disabled: this.isSubmitting || this.form.invalid,
        onClick: () => this.saveRequest()
      };
    } else {
      this.primaryButton = {
        label: 'Save',
        type: 'primary',
        disabled: true,
        onClick: () => undefined
      };
    }

    if (this.detail.canMessage) {
      this.secondaryButton = {
        label: 'Message',
        type: 'secondary',
        disabled: this.isSubmitting,
        onClick: () => this.router.navigate(['/app/crew/library-of-things/requests', this.requestId, 'chat'])
      };
    } else if (this.detail.canCancel) {
      this.secondaryButton = {
        label: 'Cancel Request',
        type: 'secondary',
        disabled: this.isSubmitting,
        onClick: () => this.cancelRequest()
      };
    } else {
      this.secondaryButton = null;
    }
  }

  openActiveRequests() {
    if (!this.detail) {
      return;
    }

    this.router.navigate(['/app/crew/library-of-things/units', this.detail.unitId, 'active-requests']);
  }

  denyRequest() {
    if (!this.detail?.canDeny || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.updateButtons();

    this.libraryService.denyRequest(this.requestId).subscribe({
      next: response => {
        this.isSubmitting = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to deny request');
          this.updateButtons();
          return;
        }

        this.toastService.success('Request denied');
        this.loadDetail();
      },
      error: err => {
        this.isSubmitting = false;
        this.toastService.error(err?.message ?? 'Failed to deny request');
        this.updateButtons();
      }
    });
  }

  undenyRequest() {
    if (!this.detail?.canUndeny || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.updateButtons();

    this.libraryService.undenyRequest(this.requestId).subscribe({
      next: response => {
        this.isSubmitting = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to restore request');
          this.updateButtons();
          return;
        }

        this.toastService.success('Request restored');
        this.loadDetail();
      },
      error: err => {
        this.isSubmitting = false;
        this.toastService.error(err?.message ?? 'Failed to restore request');
        this.updateButtons();
      }
    });
  }

  private completeRequest() {
    if (!this.detail?.canComplete || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.updateButtons();

    this.libraryService.completeRequest(this.requestId).subscribe({
      next: response => {
        this.isSubmitting = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to complete request');
          this.updateButtons();
          return;
        }

        if (response.contributionGift) {
          void this.giftLogCrypto.encryptLibraryCreatorContribution(response.contributionGift, this.crewId);
        }
        if (response.completerGift) {
          void this.giftLogCrypto.encryptLibraryCompleterContribution(response.completerGift, this.crewId);
        }
        if (response.receptionGift) {
          void this.giftLogCrypto.encryptLibraryReceptionGift(response.receptionGift, this.crewId);
        }

        this.toastService.success('Request completed');
        this.router.navigate(['/app/crew/library-of-things/requests']);
      },
      error: err => {
        this.isSubmitting = false;
        this.toastService.error(err?.message ?? 'Failed to complete request');
        this.updateButtons();
      }
    });
  }

  private saveRequest() {
    if (!this.detail?.canEdit || this.isSubmitting || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.updateButtons();

    void this.encryptionContent.whenReady().then(async () => {
      try {
        const purpose = this.form.getRawValue().purpose as string;
        const encrypted = await this.libraryCrypto.encryptRequestPurpose(this.crewId, purpose);
        const payload = {
          purposePreview: encrypted.purposePreview,
          neededByStart: this.toApiDate(this.form.getRawValue().neededByStart),
          neededByEnd: this.toApiDate(this.form.getRawValue().neededByEnd),
          nonce: encrypted.nonce,
          ciphertext: encrypted.ciphertext
        };

        this.libraryService.updateRequest(this.requestId, payload).subscribe({
          next: response => {
            this.isSubmitting = false;
            if (!response.success) {
              this.toastService.error(response.message || 'Failed to update request');
              this.updateButtons();
              return;
            }

            this.toastService.success('Request updated');
            this.loadDetail();
          },
          error: err => {
            this.isSubmitting = false;
            this.toastService.error(err?.message ?? 'Failed to update request');
            this.updateButtons();
          }
        });
      } catch (err: unknown) {
        this.isSubmitting = false;
        this.toastService.error(err instanceof Error ? err.message : 'Encryption failed');
        this.updateButtons();
      }
    });
  }

  private cancelRequest() {
    if (!this.detail?.canCancel || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.updateButtons();

    this.libraryService.cancelRequest(this.requestId).subscribe({
      next: response => {
        this.isSubmitting = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to cancel request');
          this.updateButtons();
          return;
        }

        this.toastService.success('Request cancelled');
        this.router.navigate(['/app/crew/library-of-things/requests/mine']);
      },
      error: err => {
        this.isSubmitting = false;
        this.toastService.error(err?.message ?? 'Failed to cancel request');
        this.updateButtons();
      }
    });
  }

  private toInputDate(value: string): string {
    return value.slice(0, 10);
  }

  private toApiDate(value: string): string {
    return new Date(`${value}T00:00:00.000Z`).toISOString();
  }
}
