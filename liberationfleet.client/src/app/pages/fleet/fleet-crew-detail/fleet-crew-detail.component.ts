import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { KickReasonDialogComponent } from '../../../components/kick-reason-dialog/kick-reason-dialog.component';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetCrewDetail, FleetCrewmateSummary } from '../../../models/fleet.model';

@Component({
  selector: 'app-fleet-crew-detail',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, KickReasonDialogComponent],
  templateUrl: './fleet-crew-detail.component.html',
  styleUrl: './fleet-crew-detail.component.css'
})
export class FleetCrewDetailComponent implements OnInit {
  crew: FleetCrewDetail | null = null;
  loading = true;
  errorMessage = '';
  actionLoading = false;
  showKickDialog = false;
  backButton!: ActionBarButton;
  primaryButton: ActionBarButton | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);
  private crewId = 0;

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/fleet/crews']);
    this.crewId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.crewId) {
      this.loading = false;
      this.errorMessage = 'Invalid crew.';
      return;
    }
    this.loadCrew();
  }

  openCrewmate(crewmate: FleetCrewmateSummary) {
    this.router.navigate(['/app/fleet/crewmates', crewmate.userId]);
  }

  onKickCrew() {
    this.showKickDialog = true;
  }

  onConfirmKick(reason: string) {
    this.showKickDialog = false;
    if (this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.fleetService.kickCrew(this.crewId, reason).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to kick crew');
          return;
        }
        this.toastService.success(response.message || 'Kick proposal submitted');
        this.router.navigate(['/app/fleet/crews']);
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to kick crew');
      }
    });
  }

  onCancelKick() {
    this.showKickDialog = false;
  }

  private loadCrew() {
    this.loading = true;
    this.errorMessage = '';
    this.fleetService.getCrewDetail(this.crewId).subscribe({
      next: response => {
        if (!response.success || !response.crew) {
          this.errorMessage = response.message || 'Failed to load crew';
          this.crew = null;
        } else {
          this.crew = response.crew;
          this.updatePrimaryButton();
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private updatePrimaryButton() {
    if (!this.crew?.canJoin) {
      this.primaryButton = null;
      return;
    }

    this.primaryButton = {
      label: 'Join Crew',
      type: 'primary',
      disabled: this.actionLoading,
      onClick: () => this.onJoinCrew()
    };
  }

  private onJoinCrew() {
    if (this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.updatePrimaryButton();
    this.fleetService.joinCrew(this.crewId).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to join crew');
          this.updatePrimaryButton();
          return;
        }
        this.toastService.success(response.message || 'Join request submitted');
        this.updatePrimaryButton();
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to join crew');
        this.updatePrimaryButton();
      }
    });
  }
}
