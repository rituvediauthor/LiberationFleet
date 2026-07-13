import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { AdultContentGateComponent } from '../../../../components/adult-content-gate/adult-content-gate.component';
import { FleetService } from '../../../../services/fleet.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { FleetForumListItem } from '../../../../models/fleet-forum.model';
import { AdultContentService } from '../../../../services/adult-content.service';
import { ContentPreferenceService } from '../../../../services/content-preference.service';
import { NavigationService } from '../../../../services/navigation.service';

@Component({
  selector: 'app-fleet-forum-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, AdultContentGateComponent],
  templateUrl: './fleet-forum-list.component.html',
  styleUrl: './fleet-forum-list.component.css'
})
export class FleetForumListComponent implements OnInit {
  items: FleetForumListItem[] = [];
  loading = true;
  errorMessage = '';
  showAdultGate = false;
  pendingItem: FleetForumListItem | null = null;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
    this.createButton = {
      label: 'Create forum post',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/fleet/forums/create'])
    };

    this.contentPreferenceService.ensureLoaded().subscribe({
      next: () => this.loadPosts(),
      error: () => this.loadPosts()
    });
  }

  get visibleItems(): FleetForumListItem[] {
    return this.items.filter(item => this.adultContentService.shouldShowEntry(item.isAdultContent));
  }

  formatActivity(date: string): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  previewBody(body: string | null | undefined): string {
    const text = (body ?? '').trim();
    if (!text) {
      return '';
    }
    return text.length > 140 ? `${text.slice(0, 140)}…` : text;
  }

  openPost(item: FleetForumListItem) {
    const resourceKey = this.adultContentService.resourceKey('forum', item.id);
    if (this.adultContentService.needsAgeGate(item.isAdultContent, resourceKey)) {
      this.pendingItem = item;
      this.showAdultGate = true;
      return;
    }

    this.navigateToPost(item);
  }

  onAdultGateConfirmed() {
    if (!this.pendingItem) {
      this.showAdultGate = false;
      return;
    }

    const resourceKey = this.adultContentService.resourceKey('forum', this.pendingItem.id);
    this.adultContentService.grantConsent(resourceKey);
    const item = this.pendingItem;
    this.pendingItem = null;
    this.showAdultGate = false;
    this.navigateToPost(item);
  }

  onAdultGateDeclined() {
    this.pendingItem = null;
    this.showAdultGate = false;
  }

  private navigateToPost(item: FleetForumListItem) {
    this.router.navigate(['/app/fleet/forums', item.id]);
  }

  private loadPosts() {
    this.loading = true;
    this.errorMessage = '';

    this.fleetService.getForums().subscribe({
      next: response => {
        this.loading = false;
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load forum posts';
          this.toastService.error(this.errorMessage);
          return;
        }
        this.items = response.items ?? [];
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.error?.message ?? err?.message ?? 'Failed to load forum posts';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
