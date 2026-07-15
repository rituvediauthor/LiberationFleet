import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { NavigationService } from '../../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../../components/content-badge/content-badge.component';
import { FleetService } from '../../../../services/fleet.service';
import { NotificationService } from '../../../../services/notification.service';
import { ToastService } from '../../../../components/toast/toast.component';

@Component({
  selector: 'app-fleet-library-hub',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ContentBadgeComponent],
  templateUrl: './fleet-library-hub.component.html',
  styleUrl: './fleet-library-hub.component.css'
})
export class FleetLibraryHubComponent implements OnInit, OnDestroy {
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  loading = true;
  libraryEnabled = false;
  errorMessage = '';
  resourceCounts: Record<string, number> = {};

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private notificationService = inject(NotificationService);
  private toastService = inject(ToastService);
  private subscription?: Subscription;

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
    this.createButton = {
      label: 'Create Offering',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/library-of-things/offerings/create'])
    };
  }

  ngOnInit() {
    this.notificationService.refreshBadges();
    this.subscription = this.notificationService.resourceCounts$.subscribe(counts => {
      this.resourceCounts = counts;
    });

    this.fleetService.getLibraryStatus().subscribe({
      next: status => {
        this.loading = false;
        if (!status.success) {
          this.errorMessage = status.message || 'Failed to load library status.';
          return;
        }
        this.libraryEnabled = status.libraryOfThingsEnabled;
        if (!this.libraryEnabled) {
          this.errorMessage = 'Library of Things is not enabled for this fleet.';
        }
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.error?.message ?? err?.message ?? 'Failed to load library status.';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  ngOnDestroy() {
    this.subscription?.unsubscribe();
  }

  sectionBadgeCount(key: string): number {
    return this.resourceCounts[key] ?? 0;
  }

  openSection(section: 'durable' | 'consumable' | 'services') {
    this.router.navigate(['/app/fleet/library', section]);
  }

  openRequests() {
    this.router.navigate(['/app/crew/library-of-things/requests']);
  }

  openMyRequests() {
    this.router.navigate(['/app/crew/library-of-things/requests/mine']);
  }

  openMine() {
    this.router.navigate(['/app/crew/library-of-things/mine']);
  }
}
