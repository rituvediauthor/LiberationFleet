import { Component, HostListener, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { CrewService } from '../../services/crew.service';
import { ProposalService } from '../../services/proposal.service';
import { ToastService } from '../../components/toast/toast.component';
import { JoinRequestListItem } from '../../models/crew.model';

@Component({
  selector: 'app-my-join-requests',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './my-join-requests.component.html',
  styleUrl: './my-join-requests.component.css'
})
export class MyJoinRequestsComponent implements OnInit {
  backButton: ActionBarButton;
  loading = true;
  errorMessage = '';
  items: JoinRequestListItem[] = [];
  openMenuProposalId: number | null = null;
  cancellingProposalId: number | null = null;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private proposalService = inject(ProposalService);
  private toastService = inject(ToastService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/crew']);
  }

  ngOnInit() {
    this.loadRequests();
  }

  @HostListener('document:click')
  closeMenus() {
    this.openMenuProposalId = null;
  }

  loadRequests() {
    this.loading = true;
    this.errorMessage = '';

    this.crewService.getMyJoinRequests().subscribe({
      next: (result) => {
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
      error: (error) => {
        this.loading = false;
        this.errorMessage = error.error?.message || 'Failed to load join requests';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  toggleMenu(proposalId: number, event: Event) {
    event.stopPropagation();
    this.openMenuProposalId = this.openMenuProposalId === proposalId ? null : proposalId;
  }

  cancelRequest(item: JoinRequestListItem, event: Event) {
    event.stopPropagation();
    this.openMenuProposalId = null;

    if (this.cancellingProposalId != null) {
      return;
    }

    this.cancellingProposalId = item.proposalId;
    this.proposalService.deleteProposal(item.proposalId).subscribe({
      next: result => {
        this.cancellingProposalId = null;
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to cancel join request');
          return;
        }
        this.items = this.items.filter(existing => existing.proposalId !== item.proposalId);
        this.toastService.success('Join request cancelled');
      },
      error: () => {
        this.cancellingProposalId = null;
        this.toastService.error('Failed to cancel join request');
      }
    });
  }

  countdownText(item: JoinRequestListItem): string | null {
    const countdown = this.proposalService.formatCountdown(
      item.approvalTimerEndsAt ? this.proposalService.parseApiDate(item.approvalTimerEndsAt) : null
    );
    return countdown || null;
  }

  formatCreatedAt(value: string): string {
    return new Date(value).toLocaleString();
  }
}
