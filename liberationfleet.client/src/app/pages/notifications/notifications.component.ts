import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { firstValueFrom, Subscription } from 'rxjs';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { ToastService } from '../../components/toast/toast.component';
import { NotificationService } from '../../services/notification.service';
import { NotificationHubService } from '../../services/notification-hub.service';
import { NotificationTargetService } from '../../services/notification-target.service';
import {
  NOTIFICATION_FILTER_OPTIONS,
  NotificationFilterCategory,
  NotificationItem
} from '../../models/notification.model';
import { CrewService } from '../../services/crew.service';
import { UserAvatarComponent } from '../../components/user-avatar/user-avatar.component';
import { NotificationCategoryMapperMatches } from '../../utils/notification-filter.util';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, NavLayoutComponent, UserAvatarComponent],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.css'
})
export class NotificationsComponent implements OnInit, OnDestroy {
  items: NotificationItem[] = [];
  loading = true;
  errorMessage = '';
  selectedFilter: NotificationFilterCategory = 'All';
  readonly filterOptions = NOTIFICATION_FILTER_OPTIONS;
  crewId = 0;

  private notificationService = inject(NotificationService);
  private notificationHub = inject(NotificationHubService);
  private notificationTargetService = inject(NotificationTargetService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private router = inject(Router);
  private subscription = new Subscription();

  ngOnInit() {
    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
      }
    });
    this.loadNotifications();
    this.subscription.add(
      this.notificationHub.notificationReceived$.subscribe(notification => {
        this.onNotificationReceived(notification);
      })
    );
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
  }

  /** Avatars only for notifications that have a known non-anonymous actor picture. */
  showActorAvatar(item: NotificationItem): boolean {
    return !!item.actorUserId && !!item.actorAvatarResourceId?.trim();
  }

  onFilterChange(value: string) {
    this.selectedFilter = value as NotificationFilterCategory;
    this.loadNotifications();
  }

  async openNotification(item: NotificationItem) {
    const available = item.isTargetAvailable ?? await firstValueFrom(
      this.notificationTargetService.isTargetAvailable(item.actionUrl)
    );
    item.isTargetAvailable = available;

    if (!available) {
      this.toastService.show('Content not available', 'info');
      return;
    }

    if (!item.isRead) {
      this.notificationService.markRead(item.id).subscribe();
      item.isRead = true;
    }

    const url = this.buildNavigationUrl(item);
    void this.router.navigateByUrl(url);
  }

  markAllRead() {
    this.notificationService.markAllRead().subscribe({
      next: () => {
        this.items = this.items.map(item => ({ ...item, isRead: true }));
      }
    });
  }

  goToPreferences() {
    void this.router.navigate(['/app/profile/preferences/notifications']);
  }

  formatWhen(value: string): string {
    return new Date(value).toLocaleString();
  }

  private onNotificationReceived(notification: NotificationItem) {
    if (!NotificationCategoryMapperMatches(notification.kind, this.selectedFilter)) {
      return;
    }

    if (this.items.some(item => item.id === notification.id)) {
      return;
    }

    this.items = [notification, ...this.items];
    this.notificationTargetService.isTargetAvailable(notification.actionUrl).subscribe({
      next: available => {
        notification.isTargetAvailable = available;
      },
      error: () => {
        notification.isTargetAvailable = false;
      }
    });
  }

  private loadNotifications() {
    this.loading = true;
    this.errorMessage = '';
    this.notificationService.getNotifications(this.selectedFilter).subscribe({
      next: response => {
        this.items = response.success ? response.items ?? [] : [];
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load notifications';
        }
        this.loading = false;
        this.validateTargets();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load notifications';
      }
    });
  }

  private validateTargets() {
    for (const item of this.items) {
      item.isTargetAvailable = null;
      this.notificationTargetService.isTargetAvailable(item.actionUrl).subscribe({
        next: available => {
          item.isTargetAvailable = available;
        },
        error: () => {
          item.isTargetAvailable = false;
        }
      });
    }
  }

  private buildNavigationUrl(item: NotificationItem): string {
    // Preserve full ActionUrl (highlightId / messageId / commentId) for deep-link scroll+highlight.
    return item.actionUrl;
  }
}
