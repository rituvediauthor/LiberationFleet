import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
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

  private router = inject(Router);
  private crewService = inject(CrewService);
  private proposalService = inject(ProposalService);
  private toastService = inject(ToastService);

  constructor() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/join'])
    };
  }

  ngOnInit() {
    this.loadRequests();
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

  countdownText(item: JoinRequestListItem): string | null {
    const countdown = this.proposalService.formatCountdown(
      item.approvalTimerEndsAt ? new Date(item.approvalTimerEndsAt) : null
    );
    return countdown || null;
  }

  formatCreatedAt(value: string): string {
    return new Date(value).toLocaleString();
  }
}
