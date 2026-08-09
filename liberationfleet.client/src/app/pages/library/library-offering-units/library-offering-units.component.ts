import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { LibraryItemCardComponent } from '../../../components/library-item-card/library-item-card.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { ToastService } from '../../../components/toast/toast.component';
import { LibraryUnitListItem } from '../../../models/library.model';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { NavigationService } from '../../../services/navigation.service';

@Component({
  selector: 'app-library-offering-units',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, LibraryItemCardComponent],
  templateUrl: './library-offering-units.component.html',
  styleUrl: './library-offering-units.component.css'
})
export class LibraryOfferingUnitsComponent implements OnInit {
  backButton!: ActionBarButton;
  items: LibraryUnitListItem[] = [];
  title = 'Durable Goods';
  loading = true;
  errorMessage = '';
  crewId = 0;

  private offeringId = 0;
  private navigation = inject(NavigationService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/crew/library-of-things/durable']);
  }

  ngOnInit() {
    this.offeringId = Number(this.route.snapshot.paramMap.get('offeringId')) || 0;

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.loadItems();
      },
      error: () => {
        this.crewId = 0;
        this.loadItems();
      }
    });
  }

  openUnit(item: LibraryUnitListItem) {
    this.router.navigate(['/app/crew/library-of-things/units', item.unitId]);
  }

  private loadItems() {
    this.loading = true;
    this.errorMessage = '';

    this.libraryService.getOfferingUnits(this.offeringId).subscribe({
      next: items => {
        void this.applyItems(items);
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load items';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private async applyItems(items: LibraryUnitListItem[]) {
    try {
      await this.encryptionContent.whenReady();
      const enriched = this.crewId > 0
        ? await this.libraryCrypto.enrichUnitListItems(items, this.crewId)
        : items;
      this.items = enriched;
    } catch {
      this.items = items;
    } finally {
      this.title = this.items[0]?.title || 'Durable Goods';
      this.loading = false;
    }
  }
}
