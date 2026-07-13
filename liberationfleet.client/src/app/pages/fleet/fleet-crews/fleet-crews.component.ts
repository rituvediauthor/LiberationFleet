import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetCrewListItem } from '../../../models/fleet.model';

@Component({
  selector: 'app-fleet-crews',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './fleet-crews.component.html',
  styleUrl: './fleet-crews.component.css'
})
export class FleetCrewsComponent implements OnInit {
  crews: FleetCrewListItem[] = [];
  loading = true;
  errorMessage = '';
  backButton!: ActionBarButton;
  addButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
    this.addButton = {
      label: 'Add Crew',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/fleet/crews/invite'])
    };
    this.loadCrews();
  }

  openCrew(crew: FleetCrewListItem) {
    this.router.navigate(['/app/fleet/crews', crew.crewId]);
  }

  private loadCrews() {
    this.loading = true;
    this.errorMessage = '';

    this.fleetService.getCrews().subscribe({
      next: response => {
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load crews';
          this.crews = [];
        } else {
          this.crews = response.items ?? [];
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crews';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
