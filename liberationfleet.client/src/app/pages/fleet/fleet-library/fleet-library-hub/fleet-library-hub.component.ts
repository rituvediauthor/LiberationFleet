import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { FleetService } from '../../../../services/fleet.service';
import { ToastService } from '../../../../components/toast/toast.component';

@Component({
  selector: 'app-fleet-library-hub',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './fleet-library-hub.component.html',
  styleUrl: './fleet-library-hub.component.css'
})
export class FleetLibraryHubComponent implements OnInit {
  backButton!: ActionBarButton;
  loading = true;
  libraryEnabled = false;
  errorMessage = '';

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
  }

  ngOnInit() {
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

  openSection(section: 'durable' | 'consumable' | 'services') {
    this.router.navigate(['/app/fleet/library', section]);
  }
}
