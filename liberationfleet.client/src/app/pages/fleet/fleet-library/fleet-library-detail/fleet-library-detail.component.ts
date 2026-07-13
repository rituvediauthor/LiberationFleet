import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { LibraryImageCarouselComponent } from '../../../../components/library-image-carousel/library-image-carousel.component';
import { FleetService } from '../../../../services/fleet.service';
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
  detail: LibraryUnitDetail | null = null;
  loading = true;
  errorMessage = '';
  unitId = 0;

  private route = inject(ActivatedRoute);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
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

    this.fleetService.getLibraryUnit(this.unitId).subscribe({
      next: item => {
        this.detail = item;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load item.';
        this.toastService.error(this.errorMessage);
      }
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
}
