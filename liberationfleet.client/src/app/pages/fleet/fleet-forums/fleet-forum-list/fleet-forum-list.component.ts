import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../../components/content-badge/content-badge.component';
import { AdultContentGateComponent } from '../../../../components/adult-content-gate/adult-content-gate.component';
import { LibraryImageCarouselComponent } from '../../../../components/library-image-carousel/library-image-carousel.component';
import { AuthorIdentityComponent } from '../../../../components/author-identity/author-identity.component';
import { ForumEngagementBarComponent } from '../../../../components/forum-engagement-bar/forum-engagement-bar.component';
import { ContentLikersDialogComponent } from '../../../../components/content-likers-dialog/content-likers-dialog.component';
import { FleetService } from '../../../../services/fleet.service';
import { ProposalCryptoService } from '../../../../services/crypto/proposal-crypto.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { FleetForumListItem } from '../../../../models/fleet-forum.model';
import { ProposalListItem } from '../../../../models/proposal.model';
import { AdultContentService } from '../../../../services/adult-content.service';
import { ContentPreferenceService } from '../../../../services/content-preference.service';
import { NavigationService } from '../../../../services/navigation.service';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../../services/encryption-content.service';
import { HiddenContentItem, MuteDuration, MutedContentItem, MutedContentType, withMute } from '../../../../models/notification.model';
import { MuteDurationDialogComponent } from '../../../../components/mute-duration-dialog/mute-duration-dialog.component';
import { NotificationService } from '../../../../services/notification.service';
import {
  clearForumListScrollState,
  readForumListScrollState,
  saveForumListScrollState
} from '../../../../utils/forum-list-scroll.util';
import { ForumListPrefetchService } from '../../../../services/forum-list-prefetch.service';
import { ContentLiker } from '../../../../models/gift.model';
import { truncateNotificationPreview } from '../../../../utils/notification-preview.util';
import { OutsideMenuCloseDirective } from '../../../../directives/outside-menu-close.directive';
import { PullRefreshController } from '../../../../utils/pull-refresh.controller';

@Component({
  selector: 'app-fleet-forum-list',
  standalone: true,
  imports: [
    CommonModule,
    PageLayoutComponent,
    AdultContentGateComponent,
    ContentBadgeComponent,
    LibraryImageCarouselComponent,
    AuthorIdentityComponent,
    ForumEngagementBarComponent,
    ContentLikersDialogComponent,
    MuteDurationDialogComponent,
    OutsideMenuCloseDirective
  ],
  templateUrl: './fleet-forum-list.component.html',
  styleUrl: './fleet-forum-list.component.css'
})
export class FleetForumListComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild(PageLayoutComponent) pageLayout?: PageLayoutComponent;
  @ViewChild('loadMoreSentinel') loadMoreSentinel?: ElementRef<HTMLElement>;

  items: FleetForumListItem[] = [];
  loading = true;
  loadingMore = false;
  refreshing = false;
  hasMore = false;
  errorMessage = '';
  fleetId = 0;
  openMenuItemId: number | null = null;
  likingPostId: number | null = null;
  mutedItems: MutedContentItem[] = [];
  muteTarget: { contentType: MutedContentType; resourceId: number; noun: string } | null = null;
  hiddenItems: HiddenContentItem[] = [];
  showHiddenExpanded = false;
  showAdultGate = false;
  pendingItem: FleetForumListItem | null = null;
  resourceCounts: Record<string, number> = {};
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  pullDistance = 0;
  likersDialogOpen = false;
  likersDialogLoading = false;
  likersDialogItems: ContentLiker[] = [];
  likersDialogTitle = 'Liked by';

  private readonly pageSize = 20;
  private readonly scrollStateKey = 'fleet';
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private forumCrypto = inject(ProposalCryptoService);
  private toastService = inject(ToastService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private encryptionContent = inject(EncryptionContentService);
  private notificationService = inject(NotificationService);
  private forumPrefetch = inject(ForumListPrefetchService);
  private encryptionReload?: EncryptionReloadHandle;
  private listObserver?: IntersectionObserver;
  private scrollEl: HTMLElement | null = null;
  private pendingRestoreCount: number | null = null;
  private pendingRestoreScrollTop: number | null = null;
  private readonly pullRefresh = new PullRefreshController({
    edges: ['top'],
    isBusy: () => this.loading || this.loadingMore || this.refreshing,
    onRefresh: () => this.refreshFromTop(),
    onDistanceChange: distance => {
      this.pullDistance = distance;
    }
  });

  ngOnInit() {
    const saved = readForumListScrollState(this.scrollStateKey);
    if (saved && saved.loadedCount > 0) {
      this.pendingRestoreCount = saved.loadedCount;
      this.pendingRestoreScrollTop = saved.scrollTop;
    }

    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadPosts(true));

    this.backButton = this.navigation.createBackButton(['/app/fleet']);
    this.notificationService.refreshBadges();
    this.notificationService.resourceCounts$.subscribe(counts => {
      this.resourceCounts = counts;
    });

    this.createButton = {
      label: 'Create Post',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/fleet/forums/create'])
    };

    this.fleetService.getStatus().subscribe({
      next: async status => {
        this.fleetId = status.fleetId ?? 0;
        await this.encryptionContent.whenReady();
        this.contentPreferenceService.ensureLoaded().subscribe({
          next: () => {
            this.loadMutes();
            this.loadHidden();
            this.loadPosts(true);
          },
          error: () => {
            this.loadMutes();
            this.loadHidden();
            this.loadPosts(true);
          }
        });
        this.encryptionReload?.markInitialLoadDone();
      },
      error: () => {
        this.errorMessage = 'Failed to load fleet status';
        this.loading = false;
      }
    });
  }

  ngAfterViewInit() {
    setTimeout(() => this.bindScrollContainer(), 0);
  }

  ngOnDestroy() {
    this.encryptionReload?.subscription.unsubscribe();
    this.listObserver?.disconnect();
    this.unbindScrollContainer();
  }

  closeMenus() {
    this.openMenuItemId = null;
  }

  get visibleItems(): FleetForumListItem[] {
    return this.items.filter(item =>
      !this.isItemHidden(item.id) && this.adultContentService.shouldShowEntry(item.isAdultContent)
    );
  }

  get hiddenItemsList(): FleetForumListItem[] {
    return this.items.filter(item =>
      this.isItemHidden(item.id) && this.adultContentService.shouldShowEntry(item.isAdultContent)
    );
  }

  muteContentType(): MutedContentType {
    return 'Forum';
  }

  isItemMuted(itemId: number): boolean {
    return this.notificationService.isMuted(this.mutedItems, this.muteContentType(), itemId);
  }

  isItemHidden(itemId: number): boolean {
    return this.notificationService.isHidden(this.hiddenItems, this.muteContentType(), itemId);
  }

  toggleMenu(itemId: number, event: Event) {
    event.stopPropagation();
    this.openMenuItemId = this.openMenuItemId === itemId ? null : itemId;
  }

  toggleMute(item: FleetForumListItem, event: Event) {
    event.stopPropagation();
    this.openMenuItemId = null;
    this.muteTarget = { contentType: this.muteContentType(), resourceId: item.id, noun: 'Post' };
  }

  onMuteDuration(duration: MuteDuration) {
    this.commitMute(duration);
  }

  onCancelMute() {
    const target = this.muteTarget;
    if (!target || !this.notificationService.isMuted(this.mutedItems, target.contentType, target.resourceId)) {
      this.muteTarget = null;
      return;
    }
    this.commitMute(null);
  }

  dismissMuteDialog() {
    this.muteTarget = null;
  }

  private commitMute(duration: MuteDuration | null) {
    const target = this.muteTarget;
    this.muteTarget = null;
    if (!target) {
      return;
    }
    const muted = duration !== null;
    this.notificationService.setMute(target.contentType, target.resourceId, muted, duration).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update mute setting');
          return;
        }
        this.mutedItems = withMute(this.mutedItems, target.contentType, target.resourceId, duration);
        this.toastService.success(muted ? `${target.noun} muted` : `${target.noun} unmuted`);
      },
      error: () => this.toastService.error('Failed to update mute setting')
    });
  }

  hideItem(item: FleetForumListItem, event: Event) {
    event.stopPropagation();
    this.openMenuItemId = null;
    const contentType = this.muteContentType();
    this.notificationService.setHidden(contentType, item.id, true).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to hide post');
          return;
        }
        this.hiddenItems = [...this.hiddenItems, { contentType, resourceId: item.id }];
        if (!this.isItemMuted(item.id)) {
          this.mutedItems = [...this.mutedItems, { contentType, resourceId: item.id }];
        }
        this.toastService.success('Post hidden');
      },
      error: () => this.toastService.error('Failed to hide post')
    });
  }

  unhideItem(item: FleetForumListItem, event: Event) {
    event.stopPropagation();
    this.openMenuItemId = null;
    const contentType = this.muteContentType();
    this.notificationService.setHidden(contentType, item.id, false).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to unhide post');
          return;
        }
        this.hiddenItems = this.hiddenItems.filter(
          entry => !(entry.contentType === contentType && entry.resourceId === item.id)
        );
        this.toastService.success('Post unhidden');
      },
      error: () => this.toastService.error('Failed to unhide post')
    });
  }

  editItem(item: FleetForumListItem, event: Event) {
    event.stopPropagation();
    this.openMenuItemId = null;
    this.openPost(item);
  }

  toggleShowHidden() {
    this.showHiddenExpanded = !this.showHiddenExpanded;
  }

  forumBadgeCount(postId: number): number {
    return this.resourceCounts[`forum:${postId}`] ?? 0;
  }

  formatActivity(date: string): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  previewImages(item: FleetForumListItem): string[] {
    if (item.previewImageUrls && item.previewImageUrls.length > 0) {
      return item.previewImageUrls.slice(0, 20);
    }
    return item.thumbnailUrl ? [item.thumbnailUrl] : [];
  }

  previewBody(item: FleetForumListItem): string {
    const text = (item.descriptionPreview ?? item.body ?? '').trim();
    if (!text) {
      return '';
    }
    return truncateNotificationPreview(text);
  }

  shouldBlurThumbnail(item: FleetForumListItem): boolean {
    return this.adultContentService.shouldBlurThumbnail(item.isAdultContent);
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

  togglePostLike(item: FleetForumListItem) {
    if (this.likingPostId === item.id) {
      return;
    }
    this.likingPostId = item.id;
    const notificationPreview = truncateNotificationPreview(
      item.body || item.descriptionPreview || item.title
    );
    this.fleetService.toggleForumPostLike(item.id, notificationPreview).subscribe({
      next: response => {
        this.likingPostId = null;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update like');
          return;
        }
        item.likedByCurrentUser = response.liked;
        item.likeCount = response.likeCount;
      },
      error: () => {
        this.likingPostId = null;
        this.toastService.error('Failed to update like');
      }
    });
  }

  openPostLikers(item: FleetForumListItem) {
    this.likersDialogTitle = 'Liked by';
    this.likersDialogOpen = true;
    this.likersDialogLoading = true;
    this.likersDialogItems = [];
    this.fleetService.getForumPostLikers(item.id).subscribe({
      next: items => {
        this.likersDialogLoading = false;
        this.likersDialogItems = items;
      },
      error: err => {
        this.likersDialogLoading = false;
        this.likersDialogOpen = false;
        this.toastService.error(err?.message ?? 'Failed to load likers');
      }
    });
  }

  closeLikersDialog() {
    this.likersDialogOpen = false;
    this.likersDialogItems = [];
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
    this.persistScrollState();
    this.router.navigate(['/app/fleet/forums', item.id]);
  }

  private persistScrollState() {
    const scrollTop = this.pageLayout?.scrollElement?.scrollTop ?? 0;
    saveForumListScrollState(this.scrollStateKey, {
      scrollTop,
      loadedCount: this.items.length
    });
  }

  private loadMutes() {
    this.notificationService.getMutes().subscribe({
      next: response => {
        if (response.success) {
          this.mutedItems = response.items ?? [];
        }
      }
    });
  }

  private loadHidden() {
    this.notificationService.getHidden().subscribe({
      next: response => {
        if (response.success) {
          this.hiddenItems = response.items ?? [];
        }
      }
    });
  }

  private loadPosts(reset: boolean) {
    if (reset) {
      const prefetched = this.forumPrefetch.takeFleetSpacePage(this.fleetId);
      if (prefetched && !this.refreshing && !this.pendingRestoreCount) {
        this.items = prefetched.items;
        this.hasMore = prefetched.hasMore;
        this.loading = false;
        this.errorMessage = '';
        setTimeout(() => {
          this.bindScrollContainer();
          this.setupLoadMoreObserver();
        }, 0);
        return;
      }

      if (!this.refreshing) {
        this.loading = true;
      }
      this.errorMessage = '';
      this.items = [];
      this.hasMore = false;
    } else {
      this.loadingMore = true;
    }

    const offset = reset ? 0 : this.items.length;
    this.fleetService.getForums({ offset, limit: this.pageSize }).subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.errorMessage = response.message || 'Failed to load posts';
            this.toastService.error(this.errorMessage);
            return;
          }

          const pageItems = response.items ?? [];
          let mapped = pageItems;
          if (this.fleetId > 0) {
            mapped = await this.forumCrypto.decryptListItems(
              pageItems as unknown as ProposalListItem[],
              { fleetId: this.fleetId }
            ) as unknown as FleetForumListItem[];
          }
          this.items = reset ? mapped : [...this.items, ...mapped];
          this.hasMore = !!response.hasMore;
        } catch (error: unknown) {
          if (reset) {
            this.items = [];
          }
          this.errorMessage = error instanceof Error
            ? error.message
            : 'Failed to decrypt posts';
          this.toastService.error(this.errorMessage);
        } finally {
          this.loading = false;
          this.loadingMore = false;
          this.refreshing = false;
          this.pullDistance = 0;
          setTimeout(() => {
            this.bindScrollContainer();
            this.setupLoadMoreObserver();
            void this.finishRestoreIfNeeded();
          }, 0);
        }
      },
      error: err => {
        this.loading = false;
        this.loadingMore = false;
        this.refreshing = false;
        this.pullDistance = 0;
        this.errorMessage = err?.error?.message ?? err?.message ?? 'Failed to load posts';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private loadMore() {
    if (this.loading || this.loadingMore || this.refreshing || !this.hasMore) {
      return;
    }
    this.loadPosts(false);
  }

  private refreshFromTop() {
    if (this.loading || this.loadingMore || this.refreshing) {
      return;
    }
    this.refreshing = true;
    this.pendingRestoreCount = null;
    this.pendingRestoreScrollTop = null;
    clearForumListScrollState(this.scrollStateKey);
    this.loadPosts(true);
  }

  private async finishRestoreIfNeeded() {
    const targetCount = this.pendingRestoreCount;
    const targetScroll = this.pendingRestoreScrollTop;
    if (targetCount == null || targetScroll == null) {
      return;
    }

    if (this.items.length < targetCount && this.hasMore && !this.loadingMore) {
      this.loadMore();
      return;
    }

    this.pendingRestoreCount = null;
    this.pendingRestoreScrollTop = null;
    clearForumListScrollState(this.scrollStateKey);
    const el = this.pageLayout?.scrollElement;
    if (el) {
      requestAnimationFrame(() => {
        el.scrollTop = targetScroll;
      });
    }
  }

  private setupLoadMoreObserver() {
    this.listObserver?.disconnect();
    const sentinel = this.loadMoreSentinel?.nativeElement;
    if (!sentinel || !this.hasMore) {
      return;
    }

    this.listObserver = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) {
        this.loadMore();
      }
    }, { root: this.pageLayout?.scrollElement ?? null, threshold: 0.1 });

    this.listObserver.observe(sentinel);
  }

  private bindScrollContainer() {
    const el = this.pageLayout?.scrollElement ?? null;
    if (el === this.scrollEl) {
      return;
    }
    this.unbindScrollContainer();
    this.scrollEl = el;
    this.pullRefresh.bind(el);
  }

  private unbindScrollContainer() {
    this.pullRefresh.unbind();
    this.scrollEl = null;
  }
}
