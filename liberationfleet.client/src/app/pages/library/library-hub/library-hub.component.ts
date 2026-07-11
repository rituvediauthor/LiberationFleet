import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { NotificationService } from '../../../services/notification.service';

@Component({
  selector: 'app-library-hub',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ContentBadgeComponent],
  templateUrl: './library-hub.component.html',
  styleUrl: './library-hub.component.css'
})
export class LibraryHubComponent implements OnInit, OnDestroy {
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  resourceCounts: Record<string, number> = {};

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private notificationService = inject(NotificationService);
  private subscription?: Subscription;

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/crew']);

    this.createButton = {
      label: 'Create Offering',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/library-of-things/offerings/create'])
    };
  }

  ngOnInit() {
    this.notificationService.refreshBadges();
    this.subscription = this.notificationService.resourceCounts$.subscribe(counts => {
      this.resourceCounts = counts;
    });
  }

  ngOnDestroy() {
    this.subscription?.unsubscribe();
  }

  sectionBadgeCount(key: string): number {
    return this.resourceCounts[key] ?? 0;
  }

  openSection(section: 'requests' | 'durable' | 'consumable' | 'services' | 'mine') {
    this.router.navigate(['/app/crew/library-of-things', section]);
  }

  openMyRequests() {
    this.router.navigate(['/app/crew/library-of-things/requests/mine']);
  }
}
