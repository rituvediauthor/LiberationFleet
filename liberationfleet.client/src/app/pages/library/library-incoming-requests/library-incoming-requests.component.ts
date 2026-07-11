import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { LibraryItemCardComponent } from '../../../components/library-item-card/library-item-card.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { NotificationService } from '../../../services/notification.service';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { LibraryRequestListItem } from '../../../models/library.model';

@Component({
  selector: 'app-library-incoming-requests',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, LibraryItemCardComponent, ContentBadgeComponent],
  templateUrl: './library-incoming-requests.component.html',
  styleUrl: './library-incoming-requests.component.css'
})
export class LibraryIncomingRequestsComponent implements OnInit {
  backButton!: ActionBarButton;
  items: LibraryRequestListItem[] = [];
  loading = true;
  errorMessage = '';
  crewId = 0;
  resourceCounts: Record<string, number> = {};

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private notificationService = inject(NotificationService);
  private encryptionContent = inject(EncryptionContentService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things']);
    this.notificationService.refreshBadges();
    this.notificationService.resourceCounts$.subscribe(counts => {
      this.resourceCounts = counts;
    });

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

  requestBadgeCount(requestId: number): number {
    return this.resourceCounts[`library-request:${requestId}`] ?? 0;
  }

  openRequest(item: LibraryRequestListItem) {
    this.router.navigate(['/app/crew/library-of-things/requests', item.requestId]);
  }

  toCardItem(item: LibraryRequestListItem) {
    return this.libraryCrypto.toListItem({
      unitId: item.unitId,
      offeringId: item.offeringId,
      holderUserId: item.requesterUserId,
      holderUsername: item.requesterUsername,
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

  formatTimestamp(value: string): string {
    return new Date(value).toLocaleString();
  }

  private loadItems() {
    this.loading = true;
    this.errorMessage = '';

    this.libraryService.getIncomingRequests().subscribe({
      next: items => {
        void this.applyItems(items);
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load requests';
        this.toastService.error(this.errorMessage);
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
    }
  }
}
