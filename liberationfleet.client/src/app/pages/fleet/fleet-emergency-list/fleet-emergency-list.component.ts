import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetEmergencyListItem } from '../../../models/fleet.model';

@Component({
  selector: 'app-fleet-emergency-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './fleet-emergency-list.component.html',
  styleUrl: './fleet-emergency-list.component.css'
})
export class FleetEmergencyListComponent implements OnInit {
  items: FleetEmergencyListItem[] = [];
  loading = true;
  errorMessage = '';
  backButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
    this.loadEmergencies();
  }

  formatCreatedAt(value: string): string {
    return new Date(value).toLocaleString();
  }

  openRequest(item: FleetEmergencyListItem) {
    void this.router.navigate(['/app/crew/emergency-requests', item.id]);
  }

  private loadEmergencies() {
    this.loading = true;
    this.errorMessage = '';
    this.fleetService.getEmergencies().subscribe({
      next: result => {
        this.loading = false;
        if (!result.success) {
          this.errorMessage = result.message || 'Failed to load emergency requests';
          this.items = [];
          return;
        }
        this.items = result.items ?? [];
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error.error?.message || 'Failed to load emergency requests';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
