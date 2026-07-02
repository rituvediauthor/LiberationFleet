import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { LibraryItemCardComponent } from '../../../components/library-item-card/library-item-card.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { LibraryRequestListItem } from '../../../models/library.model';

@Component({
  selector: 'app-library-unit-active-requests',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, LibraryItemCardComponent],
  templateUrl: './library-unit-active-requests.component.html',
  styleUrl: './library-unit-active-requests.component.css'
})
export class LibraryUnitActiveRequestsComponent implements OnInit {
  backButton!: ActionBarButton;
  items: LibraryRequestListItem[] = [];
  loading = true;
  errorMessage = '';
  crewId = 0;
  unitId = 0;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);

  ngOnInit() {
    this.unitId = Number(this.route.snapshot.paramMap.get('unitId'));
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/library-of-things/requests'])
    };

    if (!this.unitId) {
      this.loading = false;
      this.errorMessage = 'Invalid item.';
      return;
    }

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

  formatDateRange(item: LibraryRequestListItem): string {
    const start = new Date(item.neededByStart).toLocaleDateString();
    const end = new Date(item.neededByEnd).toLocaleDateString();
    return `${start} – ${end}`;
  }

  private loadItems() {
    this.loading = true;
    this.libraryService.getUnitActiveRequests(this.unitId).subscribe({
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
