import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ActivityService } from '../../services/activity.service';
import { ActivityCryptoService } from '../../services/crypto/activity-crypto.service';
import { EncryptionContentService } from '../../services/encryption-content.service';
import { ToastService } from '../../components/toast/toast.component';
import {
  ACTIVITY_FILTER_OPTIONS,
  UserActivityFilterCategory,
  UserActivityItem
} from '../../models/activity.model';
import { getActivityRoute } from '../../utils/activity-navigation.util';
import { EncryptionReloadHandle } from '../../services/encryption-content.service';

@Component({
  selector: 'app-activity-center',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './activity-center.component.html',
  styleUrl: './activity-center.component.css'
})
export class ActivityCenterComponent implements OnInit, OnDestroy {
  readonly filterOptions = ACTIVITY_FILTER_OPTIONS;
  selectedCategory: UserActivityFilterCategory = 'All';
  items: UserActivityItem[] = [];
  loading = true;
  loadingMore = false;
  hasMore = false;
  errorMessage = '';
  backButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private activityService = inject(ActivityService);
  private activityCrypto = inject(ActivityCryptoService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/profile']);

    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => {
      void this.enrichVisibleItems();
    });

    this.loadActivity();
  }

  ngOnDestroy() {
    this.encryptionReload?.subscription.unsubscribe();
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
        void this.appendAndEnrichItems(nextItems);
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

  displayPreview(item: UserActivityItem): string | null {
    return item.previewText ?? item.plaintextPreview ?? null;
  }

  showDetail(item: UserActivityItem): boolean {
    if (!item.detail?.trim()) {
      return false;
    }

    const preview = this.displayPreview(item);
    return !preview || item.detail.trim() !== preview.trim();
  }

  private async appendAndEnrichItems(nextItems: UserActivityItem[]) {
    if (!nextItems.length) {
      return;
    }

    const enriched = await this.activityCrypto.enrichItems(nextItems);
    this.items = [...this.items, ...enriched];
  }

  private async enrichVisibleItems() {
    if (!this.items.length) {
      return;
    }

    this.items = await this.activityCrypto.enrichItems(this.items);
  }

  private loadActivity() {
    this.loading = true;
    this.errorMessage = '';
    this.items = [];
    this.hasMore = false;

    this.activityService.getActivity(this.selectedCategory).subscribe({
      next: response => {
        if (!response.success) {
          this.loading = false;
          this.errorMessage = response.message || 'Failed to load activity';
          return;
        }

        void this.activityCrypto.enrichItems(response.items ?? []).then(items => {
          this.loading = false;
          this.items = items;
          this.hasMore = response.hasMore;
          this.encryptionReload?.markInitialLoadDone();
        });
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load activity';
      }
    });
  }
}
