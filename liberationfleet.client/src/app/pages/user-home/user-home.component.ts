import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { ContentBadgeComponent } from '../../components/content-badge/content-badge.component';
import { NotificationService } from '../../services/notification.service';
import { CrewNotificationAreaCounts, emptyAreaCounts } from '../../utils/notification-area.util';

@Component({
  selector: 'app-user-home',
  standalone: true,
  imports: [CommonModule, NavLayoutComponent, ContentBadgeComponent],
  templateUrl: './user-home.component.html',
  styleUrl: './user-home.component.css'
})
export class UserHomeComponent implements OnInit, OnDestroy {
  areaCounts: CrewNotificationAreaCounts = emptyAreaCounts();

  private router = inject(Router);
  private notificationService = inject(NotificationService);
  private subscription?: Subscription;

  ngOnInit() {
    this.notificationService.refreshBadges();
    this.subscription = this.notificationService.areaCounts$.subscribe(counts => {
      this.areaCounts = counts;
    });
  }

  ngOnDestroy() {
    this.subscription?.unsubscribe();
  }

  goToInvitations() {
    this.router.navigate(['/app/crew/invitations']);
  }

  goToUserProfile() {
    this.router.navigate(['/app/profile/user']);
  }

  goToGiftHistory() {
    this.router.navigate(['/app/profile/gift-history']);
  }

  goToActivityCenter() {
    this.router.navigate(['/app/profile/activity']);
  }

  goToPreferences() {
    this.router.navigate(['/app/profile/preferences']);
  }

  goToDonate() {
    this.router.navigate(['/app/donate']);
  }

  goToAiDisclosure() {
    this.router.navigate(['/app/ai-disclosure']);
  }
}
