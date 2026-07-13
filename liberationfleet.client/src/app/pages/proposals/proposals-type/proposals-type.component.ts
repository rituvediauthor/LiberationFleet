import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { NotificationService } from '../../../services/notification.service';

@Component({
  selector: 'app-proposals-type',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ContentBadgeComponent],
  templateUrl: './proposals-type.component.html',
  styleUrl: './proposals-type.component.css'
})
export class ProposalsTypeComponent implements OnInit, OnDestroy {
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  resourceCounts: Record<string, number> = {};
  isFleetScope = false;

  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private navigation = inject(NavigationService);
  private notificationService = inject(NotificationService);
  private subscription?: Subscription;

  constructor() {
    this.isFleetScope = this.route.snapshot.data['scope'] === 'fleet';
    const home = this.isFleetScope ? ['/app/fleet'] : ['/app/crew'];
    const create = this.isFleetScope ? ['/app/fleet/proposals/create'] : ['/app/crew/proposals/create'];
    this.backButton = this.navigation.createBackButton(home);

    this.createButton = {
      label: 'Create Proposal',
      type: 'primary',
      onClick: () => this.router.navigate(create)
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

  statusBadgeCount(status: 'approved' | 'pending' | 'rejected'): number {
    return this.resourceCounts[`proposal-status:${status}`] ?? 0;
  }

  openStatus(status: 'Approved' | 'Pending' | 'Rejected') {
    const base = this.isFleetScope ? '/app/fleet/proposals/list' : '/app/crew/proposals/list';
    this.router.navigate([base, status.toLowerCase()]);
  }
}
