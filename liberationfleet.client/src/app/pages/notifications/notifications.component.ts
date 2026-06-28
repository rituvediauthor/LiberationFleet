import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { NotificationService } from '../../services/notification.service';
import {
  NOTIFICATION_FILTER_OPTIONS,
  NotificationFilterCategory,
  NotificationItem
} from '../../models/notification.model';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, NavLayoutComponent],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.css'
})
export class NotificationsComponent implements OnInit {
  items: NotificationItem[] = [];
  loading = true;
  errorMessage = '';
  selectedFilter: NotificationFilterCategory = 'All';
  readonly filterOptions = NOTIFICATION_FILTER_OPTIONS;

  private notificationService = inject(NotificationService);
  private router = inject(Router);

  ngOnInit() {
    this.loadNotifications();
  }

  onFilterChange(value: string) {
    this.selectedFilter = value as NotificationFilterCategory;
    this.loadNotifications();
  }

  openNotification(item: NotificationItem) {
    if (!item.isRead) {
      this.notificationService.markRead(item.id).subscribe();
      item.isRead = true;
    }

    const url = this.buildNavigationUrl(item);
    this.router.navigateByUrl(url);
  }

  markAllRead() {
    this.notificationService.markAllRead().subscribe({
      next: () => {
        this.items = this.items.map(item => ({ ...item, isRead: true }));
      }
    });
  }

  goToPreferences() {
    this.router.navigate(['/app/profile/preferences/notifications']);
  }

  formatWhen(value: string): string {
    return new Date(value).toLocaleString();
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
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load notifications';
      }
    });
  }

  private buildNavigationUrl(item: NotificationItem): string {
    if (item.actionUrl.includes('?')) {
      const [path, query] = item.actionUrl.split('?');
      const params = new URLSearchParams(query);
      const commentId = params.get('commentId');
      if (commentId) {
        return `${path}?commentId=${commentId}`;
      }
    }

    return item.actionUrl;
  }
}
