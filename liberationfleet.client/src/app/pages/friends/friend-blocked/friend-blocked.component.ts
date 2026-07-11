import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FriendService } from '../../../services/friend.service';
import { CrewmateService } from '../../../services/crewmate.service';
import { ToastService } from '../../../components/toast/toast.component';
import { BlockedUserListItem } from '../../../models/friend.model';

@Component({
  selector: 'app-friend-blocked',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './friend-blocked.component.html',
  styleUrl: './friend-blocked.component.css'
})
export class FriendBlockedComponent implements OnInit {
  blockedUsers: BlockedUserListItem[] = [];
  loading = true;
  errorMessage = '';
  actionLoading = false;
  backButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private friendService = inject(FriendService);
  private crewmateService = inject(CrewmateService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/friends']);

    this.loadBlocked();
  }

  unblock(user: BlockedUserListItem) {
    if (this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.crewmateService.unblockCrewmate(user.userId).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to unblock');
          return;
        }
        this.toastService.success(response.message || 'User unblocked');
        this.loadBlocked();
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to unblock');
      }
    });
  }

  private loadBlocked() {
    this.loading = true;
    this.errorMessage = '';
    this.friendService.getBlocked().subscribe({
      next: response => {
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load blocked users';
          this.blockedUsers = [];
        } else {
          this.blockedUsers = response.items ?? [];
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load blocked users';
      }
    });
  }
}
