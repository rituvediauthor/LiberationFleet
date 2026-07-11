import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FriendService } from '../../../services/friend.service';
import { CrewmateService } from '../../../services/crewmate.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FriendRequestListItem } from '../../../models/friend.model';
import { formatLastActive } from '../../../models/crewmate.model';

@Component({
  selector: 'app-friend-requests',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './friend-requests.component.html',
  styleUrl: './friend-requests.component.css'
})
export class FriendRequestsComponent implements OnInit, OnDestroy {
  requests: FriendRequestListItem[] = [];
  loading = true;
  errorMessage = '';
  actionLoading = false;
  backButton!: ActionBarButton;
  activityTick = 0;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private friendService = inject(FriendService);
  private crewmateService = inject(CrewmateService);
  private toastService = inject(ToastService);
  private activityIntervalId?: ReturnType<typeof setInterval>;

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/friends']);

    this.activityIntervalId = setInterval(() => {
      this.activityTick++;
    }, 60000);

    this.loadRequests();
  }

  ngOnDestroy() {
    if (this.activityIntervalId) {
      clearInterval(this.activityIntervalId);
    }
  }

  get incomingRequests(): FriendRequestListItem[] {
    return this.requests.filter(r => r.direction === 'incoming');
  }

  get outgoingRequests(): FriendRequestListItem[] {
    return this.requests.filter(r => r.direction === 'outgoing');
  }

  formatActivity(request: FriendRequestListItem): string {
    void this.activityTick;
    return formatLastActive(request.lastLoginAt, false);
  }

  accept(request: FriendRequestListItem) {
    this.runAction(() => this.crewmateService.acceptFriendship(request.userId));
  }

  reject(request: FriendRequestListItem) {
    this.runAction(() => this.crewmateService.rejectFriendship(request.userId));
  }

  cancel(request: FriendRequestListItem) {
    this.runAction(() => this.crewmateService.cancelFriendshipRequest(request.userId));
  }

  private runAction(action: () => ReturnType<CrewmateService['acceptFriendship']>) {
    if (this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    action().subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Action failed');
          return;
        }
        this.toastService.success(response.message || 'Updated');
        this.loadRequests();
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Action failed');
      }
    });
  }

  private loadRequests() {
    this.loading = true;
    this.errorMessage = '';
    this.friendService.getRequests().subscribe({
      next: response => {
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load requests';
          this.requests = [];
        } else {
          this.requests = response.items ?? [];
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load requests';
      }
    });
  }
}
