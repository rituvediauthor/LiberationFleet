import { Component, Input, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Subscription } from 'rxjs';
import { FallibleFooterComponent } from '../fallible-footer/fallible-footer.component';
import { BrandLogoComponent } from '../brand-logo/brand-logo.component';
import { ContentBadgeComponent } from '../content-badge/content-badge.component';
import { NotificationService } from '../../services/notification.service';
import { NotificationHubService } from '../../services/notification-hub.service';

export type NavTab = 'crew' | 'fleet' | 'friends' | 'notifications' | 'profile';

@Component({
  selector: 'app-nav-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, FallibleFooterComponent, BrandLogoComponent, ContentBadgeComponent],
  templateUrl: './nav-layout.component.html',
  styleUrl: './nav-layout.component.css'
})
export class NavLayoutComponent implements OnInit, OnDestroy {
  @Input() activeTab: NavTab = 'crew';

  unreadCount = 0;
  private notificationService = inject(NotificationService);
  private notificationHub = inject(NotificationHubService);
  private subscriptions = new Subscription();

  ngOnInit() {
    this.notificationService.refreshBadges();
    this.subscriptions.add(
      this.notificationService.unreadCount$.subscribe(count => {
        this.unreadCount = count;
      })
    );
    this.subscriptions.add(
      this.notificationHub.notificationReceived$.subscribe(() => {
        this.notificationService.refreshBadges();
      })
    );
    this.subscriptions.add(
      this.notificationHub.unreadCountUpdated$.subscribe(count => {
        this.notificationService.setUnreadCount(count);
        this.notificationService.refreshBadges();
      })
    );
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
  }
}
