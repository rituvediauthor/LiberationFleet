import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ActivityService } from '../../services/activity.service';
import { ToastService } from '../../components/toast/toast.component';
import {
  ACTIVITY_FILTER_OPTIONS,
  UserActivityFilterCategory,
  UserActivityItem
} from '../../models/activity.model';
import { getActivityRoute } from '../../utils/activity-navigation.util';

@Component({
  selector: 'app-activity-center',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './activity-center.component.html',
  styleUrl: './activity-center.component.css'
})
export class ActivityCenterComponent implements OnInit {
  readonly filterOptions = ACTIVITY_FILTER_OPTIONS;
  selectedCategory: UserActivityFilterCategory = 'All';
  items: UserActivityItem[] = [];
  loading = true;
  loadingMore = false;
  hasMore = false;
  errorMessage = '';
  backButton!: ActionBarButton;

  private router = inject(Router);
  private activityService = inject(ActivityService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/profile'])
    };

    this.loadActivity();
  }

  onCategoryChange(value: string) {
    this.selectedCategory = value as UserActivityFilterCategory;
    this.loadActivity();
  }

  openItem(item: UserActivityItem) {
    const route = getActivityRoute(item);
    if (!route) {
      this.toastService.error('This activity is no longer accessible.');
      return;
    }

    void this.router.navigate(route);
  }

  loadMore() {
    if (this.loadingMore || !this.hasMore || this.items.length === 0) {
      return;
    }

    const lastItem = this.items[this.items.length - 1];
    this.loadingMore = true;
    this.activityService.getActivity(this.selectedCategory, {
      beforeCreatedAt: lastItem.createdAt,
      beforeKey: lastItem.key,
      limit: 50
    }).subscribe({
      next: response => {
        this.loadingMore = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to load more activity');
          return;
        }

        const existingKeys = new Set(this.items.map(item => item.key));
        const nextItems = (response.items ?? []).filter(item => !existingKeys.has(item.key));
        this.items = [...this.items, ...nextItems];
        this.hasMore = response.hasMore;
      },
      error: () => {
        this.loadingMore = false;
        this.toastService.error('Failed to load more activity');
      }
    });
  }

  formatWhen(date: string): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  categoryLabel(category: UserActivityFilterCategory): string {
    return this.filterOptions.find(option => option.value === category)?.label ?? category;
  }

  private loadActivity() {
    this.loading = true;
    this.errorMessage = '';
    this.items = [];
    this.hasMore = false;

    this.activityService.getActivity(this.selectedCategory).subscribe({
      next: response => {
        this.loading = false;
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load activity';
          return;
        }

        this.items = response.items ?? [];
        this.hasMore = response.hasMore;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load activity';
      }
    });
  }
}
