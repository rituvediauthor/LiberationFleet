import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { debounceTime, Subject, Subscription } from 'rxjs';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { FriendService } from '../../services/friend.service';
import { CrewmateService } from '../../services/crewmate.service';
import { NotificationService } from '../../services/notification.service';
import { ToastService } from '../../components/toast/toast.component';
import { FriendListItem } from '../../models/friend.model';
import { formatLastActive } from '../../models/crewmate.model';

@Component({
  selector: 'app-friends',
  standalone: true,
  imports: [CommonModule, FormsModule, NavLayoutComponent, ConfirmDialogComponent],
  templateUrl: './friends.component.html',
  styleUrl: './friends.component.css'
})
export class FriendsComponent implements OnInit, OnDestroy {
  friends: FriendListItem[] = [];
  loading = true;
  errorMessage = '';
  searchQuery = '';
  openMenuUserId: number | null = null;
  actionLoading = false;
  showBlockDialog = false;
  blockTarget: FriendListItem | null = null;
  activityTick = 0;

  private router = inject(Router);
  private friendService = inject(FriendService);
  private crewmateService = inject(CrewmateService);
  private notificationService = inject(NotificationService);
  private toastService = inject(ToastService);
  private searchChanged$ = new Subject<string>();
  private subscriptions: Subscription[] = [];
  private activityIntervalId?: ReturnType<typeof setInterval>;

  ngOnInit() {
    this.subscriptions.push(
      this.searchChanged$.pipe(debounceTime(250)).subscribe(query => this.loadFriends(query))
    );

    this.activityIntervalId = setInterval(() => {
      this.activityTick++;
    }, 60000);

    this.loadFriends();
  }

  ngOnDestroy() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    if (this.activityIntervalId) {
      clearInterval(this.activityIntervalId);
    }
  }

  @HostListener('document:click')
  closeMenus() {
    this.openMenuUserId = null;
  }

  onSearchChange() {
    this.searchChanged$.next(this.searchQuery);
  }

  formatActivity(friend: FriendListItem): string {
    void this.activityTick;
    return formatLastActive(friend.lastLoginAt, false);
  }

  openRequests() {
    this.router.navigate(['/app/friends/requests']);
  }

  openBlocked() {
    this.router.navigate(['/app/friends/blocked']);
  }

  openFindFriend() {
    this.router.navigate(['/app/friends/find']);
  }

  openFriend(friend: FriendListItem) {
    this.router.navigate(['/app/friends/messages', friend.userId]);
  }

  toggleMenu(userId: number, event: Event) {
    event.stopPropagation();
    this.openMenuUserId = this.openMenuUserId === userId ? null : userId;
  }

  muteFriend(friend: FriendListItem, event: Event) {
    event.stopPropagation();
    this.openMenuUserId = null;
    this.notificationService.setMute('Friend', friend.userId, !friend.isMuted).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update mute');
          return;
        }
        friend.isMuted = !friend.isMuted;
        this.toastService.success(friend.isMuted ? 'Friend muted' : 'Friend unmuted');
      },
      error: () => this.toastService.error('Failed to update mute')
    });
  }

  unfriend(friend: FriendListItem, event: Event) {
    event.stopPropagation();
    this.openMenuUserId = null;
    if (this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.crewmateService.unfriend(friend.userId).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to unfriend');
          return;
        }
        this.toastService.success(response.message || 'Unfriended');
        this.loadFriends(this.searchQuery);
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to unfriend');
      }
    });
  }

  blockFriend(friend: FriendListItem, event: Event) {
    event.stopPropagation();
    this.openMenuUserId = null;
    this.blockTarget = friend;
    this.showBlockDialog = true;
  }

  onConfirmBlock() {
    this.showBlockDialog = false;
    if (!this.blockTarget || this.actionLoading) {
      return;
    }

    const target = this.blockTarget;
    this.blockTarget = null;
    this.actionLoading = true;
    this.crewmateService.blockCrewmate(target.userId).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to block');
          return;
        }
        this.toastService.success(response.message || 'Friend blocked');
        this.loadFriends(this.searchQuery);
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to block');
      }
    });
  }

  onCancelBlock() {
    this.showBlockDialog = false;
    this.blockTarget = null;
  }

  private loadFriends(search = '') {
    this.loading = true;
    this.errorMessage = '';
    this.friendService.getFriends(search).subscribe({
      next: response => {
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load friends';
          this.friends = [];
        } else {
          this.friends = response.items ?? [];
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load friends';
      }
    });
  }
}
