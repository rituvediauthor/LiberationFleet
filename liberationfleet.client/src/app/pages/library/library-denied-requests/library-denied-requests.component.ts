import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { LibraryItemCardComponent } from '../../../components/library-item-card/library-item-card.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { LibraryRequestListItem } from '../../../models/library.model';

@Component({
  selector: 'app-library-denied-requests',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, LibraryItemCardComponent, ConfirmDialogComponent],
  templateUrl: './library-denied-requests.component.html',
  styleUrl: './library-denied-requests.component.css'
})
export class LibraryDeniedRequestsComponent implements OnInit {
  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;
  items: LibraryRequestListItem[] = [];
  loading = true;
  dismissing = false;
  errorMessage = '';
  crewId = 0;
  showDismissAllDialog = false;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things/requests/mine']);
    this.updatePrimaryButton();

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.loadItems();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew membership.';
      }
    });
  }

  openRequest(item: LibraryRequestListItem) {
    this.router.navigate(['/app/crew/library-of-things/requests', item.requestId], {
      queryParams: { from: 'denied' }
    });
  }

  onConfirmDismissAll() {
    this.showDismissAllDialog = false;
    if (this.dismissing || this.items.length === 0) {
      return;
    }

    this.dismissing = true;
    this.updatePrimaryButton();
    this.libraryService.dismissAllDeniedRequests().subscribe({
      next: result => {
        this.dismissing = false;
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to dismiss denied requests');
          this.updatePrimaryButton();
          return;
        }
        this.toastService.success(result.message || 'Denied requests dismissed');
        this.router.navigate(['/app/crew/library-of-things/requests/mine']);
      },
      error: err => {
        this.dismissing = false;
        this.toastService.error(err?.error?.message || err?.message || 'Failed to dismiss denied requests');
        this.updatePrimaryButton();
      }
    });
  }

  onCancelDismissAll() {
    this.showDismissAllDialog = false;
  }

  toCardItem(item: LibraryRequestListItem) {
    return this.libraryCrypto.toListItem({
      unitId: item.unitId,
      offeringId: item.offeringId,
      holderUserId: item.holderUserId,
      holderUsername: item.holderUsername,
      title: item.title,
      descriptionPreview: item.fullPurpose ?? item.purposePreview,
      categories: item.categories,
      thumbnailResourceId: item.thumbnailResourceId,
      thumbnailUrl: item.thumbnailUrl,
      hasEncryptedContent: item.hasEncryptedContent,
      unitStatus: '',
      valuePerUnit: 0,
      unitLabel: null,
      viewer: { isHolder: false, canRequest: false }
    });
  }

  formatDateRange(item: LibraryRequestListItem): string {
    return `${this.formatDate(item.neededByStart)} – ${this.formatDate(item.neededByEnd)}`;
  }

  private formatDate(value: string): string {
    return new Date(value).toLocaleDateString();
  }

  private updatePrimaryButton() {
    this.primaryButton = {
      label: 'Dismiss all',
      type: 'primary',
      disabled: this.loading || this.dismissing || this.items.length === 0,
      onClick: () => {
        this.showDismissAllDialog = true;
      }
    };
  }

  private loadItems() {
    this.loading = true;
    this.errorMessage = '';
    this.updatePrimaryButton();

    this.libraryService.getMyRequests().subscribe({
      next: items => {
        void this.applyItems(items.filter(item => item.status === 'Denied'));
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load denied requests';
        this.toastService.error(this.errorMessage);
        this.updatePrimaryButton();
      }
    });
  }

  private async applyItems(items: LibraryRequestListItem[]) {
    try {
      await this.encryptionContent.whenReady();
      this.items = await this.libraryCrypto.enrichRequestListItems(items, this.crewId);
    } catch {
      this.items = items;
    } finally {
      this.loading = false;
      this.updatePrimaryButton();
    }
  }
}
