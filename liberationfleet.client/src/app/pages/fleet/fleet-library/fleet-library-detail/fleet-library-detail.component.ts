import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { LibraryImageCarouselComponent } from '../../../../components/library-image-carousel/library-image-carousel.component';
import { FleetService } from '../../../../services/fleet.service';
import { CrewService } from '../../../../services/crew.service';
import { LibraryCryptoService } from '../../../../services/crypto/library-crypto.service';
import { EncryptionContentService } from '../../../../services/encryption-content.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { LibraryUnitDetail } from '../../../../models/library.model';
import { NavigationService } from '../../../../services/navigation.service';

@Component({
  selector: 'app-fleet-library-detail',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, LibraryImageCarouselComponent],
  templateUrl: './fleet-library-detail.component.html',
  styleUrl: './fleet-library-detail.component.css'
})
export class FleetLibraryDetailComponent implements OnInit {
  backButton!: ActionBarButton;
  requestButton: ActionBarButton | null = null;
  detail: LibraryUnitDetail | null = null;
  loading = true;
  errorMessage = '';
  unitId = 0;
  private crewId = 0;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private crewService = inject(CrewService);
  private libraryCrypto = inject(LibraryCryptoService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);

  ngOnInit() {
    const from = this.route.snapshot.queryParamMap.get('from') ?? 'durable';
    this.backButton = this.navigation.createBackButton([`/app/fleet/library/${from}`]);

    this.unitId = Number(this.route.snapshot.paramMap.get('unitId'));
    if (!this.unitId) {
      this.loading = false;
      this.errorMessage = 'Invalid item.';
      return;
    }

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.loadDetail();
      },
      error: () => this.loadDetail()
    });
  }

  get carouselImages(): string[] {
    if (this.detail?.imageUrls?.length) {
      return this.detail.imageUrls;
    }
    return this.detail?.thumbnailUrl ? [this.detail.thumbnailUrl] : [];
  }

  get holderLabel(): string {
    if (this.detail?.offeringKind === 'Service') {
      return 'Offered by';
    }
    if (this.detail?.offeringKind === 'Consumable') {
      return 'From';
    }
    return 'Holder';
  }

  openFullDetail() {
    void this.router.navigate(['/app/crew/library-of-things/units', this.unitId], {
      queryParams: { fromFleet: '1' }
    });
  }

  private loadDetail() {
    this.fleetService.getLibraryUnit(this.unitId).subscribe({
      next: item => {
        void this.applyDetail(item);
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load item.';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private async applyDetail(item: LibraryUnitDetail) {
    this.detail = item;
    this.loading = false;
    this.updateRequestButton();

    try {
      await this.encryptionContent.whenReady();
      const decryptCrewId = item.crewId && item.crewId > 0 ? item.crewId : this.crewId;
      if (decryptCrewId <= 0 || (item.crewId && item.crewId !== this.crewId && this.crewId > 0)) {
        // Cross-crew fleet offerings cannot be decrypted with the viewer's crew key.
        return;
      }
      this.detail = await this.libraryCrypto.enrichUnitDetail(item, this.crewId || decryptCrewId);
      this.updateRequestButton();
    } catch {
      // Keep plaintext detail if enrichment fails.
    }
  }

  private updateRequestButton() {
    if (!this.detail?.viewer?.canRequest) {
      this.requestButton = null;
      return;
    }

    this.requestButton = {
      label: 'Request',
      type: 'primary',
      onClick: () => this.openFullDetail()
    };
  }
}
