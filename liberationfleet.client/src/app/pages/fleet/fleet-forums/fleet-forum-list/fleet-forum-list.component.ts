import { Component, OnInit, OnDestroy, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../../components/content-badge/content-badge.component';
import { AdultContentGateComponent } from '../../../../components/adult-content-gate/adult-content-gate.component';
import { LibraryImageCarouselComponent } from '../../../../components/library-image-carousel/library-image-carousel.component';
import { UserAvatarComponent } from '../../../../components/user-avatar/user-avatar.component';
import { FleetService } from '../../../../services/fleet.service';
import { ProposalCryptoService } from '../../../../services/crypto/proposal-crypto.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { FleetForumListItem } from '../../../../models/fleet-forum.model';
import { ProposalListItem } from '../../../../models/proposal.model';
import { AdultContentService } from '../../../../services/adult-content.service';
import { ContentPreferenceService } from '../../../../services/content-preference.service';
import { NavigationService } from '../../../../services/navigation.service';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../../services/encryption-content.service';
import { HiddenContentItem, MutedContentItem, MutedContentType } from '../../../../models/notification.model';
import { NotificationService } from '../../../../services/notification.service';

@Component({
  selector: 'app-fleet-forum-list',
  standalone: true,
  imports: [
    CommonModule,
    PageLayoutComponent,
    AdultContentGateComponent,
    ContentBadgeComponent,
    LibraryImageCarouselComponent,
    UserAvatarComponent
  ],
  templateUrl: './fleet-forum-list.component.html',
  styleUrl: './fleet-forum-list.component.css'
})
export class FleetForumListComponent implements OnInit, OnDestroy {
  items: FleetForumListItem[] = [];
  loading = true;
  errorMessage = '';
  fleetId = 0;
  openMenuItemId: number | null = null;
  mutedItems: MutedContentItem[] = [];
  hiddenItems: HiddenContentItem[] = [];
  showHiddenExpanded = false;
  showAdultGate = false;
  pendingItem: FleetForumListItem | null = null;
  resourceCounts: Record<string, number> = {};
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private forumCrypto = inject(ProposalCryptoService);
  private toastService = inject(ToastService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private encryptionContent = inject(EncryptionContentService);
  private notificationService = inject(NotificationService);
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadPosts());

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
            this.loadPosts();
          },
          error: () => {
            this.loadMutes();
            this.loadHidden();
            this.loadPosts();
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

  ngOnDestroy() {
    this.encryptionReload?.subscription.unsubscribe();
  }

  @HostListener('document:click')
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
    const contentType = this.muteContentType();
    const muted = !this.isItemMuted(item.id);
    this.notificationService.setMute(contentType, item.id, muted).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update mute setting');
          return;
        }
        if (muted) {
          this.mutedItems = [...this.mutedItems, { contentType, resourceId: item.id }];
          this.toastService.success('Post muted');
        } else {
          this.mutedItems = this.mutedItems.filter(
            entry => !(entry.contentType === contentType && entry.resourceId === item.id)
          );
          this.toastService.success('Post unmuted');
        }
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
    return text.length > 200 ? `${text.slice(0, 200)}…` : text;
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

  private loadPosts() {
    this.loading = true;
    this.errorMessage = '';

    this.fleetService.getForums().subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.errorMessage = response.message || 'Failed to load posts';
            this.toastService.error(this.errorMessage);
            return;
          }

          const items = response.items ?? [];
          if (this.fleetId > 0) {
            this.items = await this.forumCrypto.decryptListItems(
              items as unknown as ProposalListItem[],
              { fleetId: this.fleetId }
            ) as unknown as FleetForumListItem[];
          } else {
            this.items = items;
          }
        } catch (error: unknown) {
          this.items = [];
          this.errorMessage = error instanceof Error
            ? error.message
            : 'Failed to decrypt posts';
          this.toastService.error(this.errorMessage);
        } finally {
          this.loading = false;
        }
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.error?.message ?? err?.message ?? 'Failed to load posts';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
