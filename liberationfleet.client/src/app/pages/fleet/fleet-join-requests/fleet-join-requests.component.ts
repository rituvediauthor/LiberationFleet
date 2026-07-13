import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FleetService } from '../../../services/fleet.service';
import { ProposalService } from '../../../services/proposal.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetJoinRequestListItem } from '../../../models/fleet.model';

@Component({
  selector: 'app-fleet-join-requests',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './fleet-join-requests.component.html',
  styleUrl: './fleet-join-requests.component.css'
})
export class FleetJoinRequestsComponent implements OnInit {
  backButton: ActionBarButton;
  loading = true;
  errorMessage = '';
  items: FleetJoinRequestListItem[] = [];

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private proposalService = inject(ProposalService);
  private toastService = inject(ToastService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
  }

  ngOnInit() {
    this.loadRequests();
  }

  loadRequests() {
    this.loading = true;
    this.errorMessage = '';

    this.fleetService.getMyJoinRequests().subscribe({
      next: result => {
        this.loading = false;
        if (!result.success) {
          this.errorMessage = result.message;
          return;
        }
        this.items = result.items.map(item => ({
          ...item,
          approvalTimerEndsAt: item.approvalTimerEndsAt ?? null
        }));
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error.error?.message || 'Failed to load join requests';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  openRequest(item: FleetJoinRequestListItem) {
    void this.router.navigate(['/app/crew/proposals', item.proposalId]);
  }

  countdownText(item: FleetJoinRequestListItem): string | null {
    const countdown = this.proposalService.formatCountdown(
      item.approvalTimerEndsAt ? new Date(item.approvalTimerEndsAt) : null
    );
    return countdown || null;
  }

  formatCreatedAt(value: string): string {
    return new Date(value).toLocaleString();
  }
}
