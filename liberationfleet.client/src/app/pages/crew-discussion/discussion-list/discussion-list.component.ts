import { Component, OnInit, OnDestroy, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { AdultContentGateComponent } from '../../../components/adult-content-gate/adult-content-gate.component';
import { CrewDiscussionService } from '../../../services/crew-discussion.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { DiscussionConfig, DiscussionKind, getDiscussionConfig } from '../../../config/discussion.config';
import { DiscussionListItem } from '../../../models/crew-discussion.model';
import { ProposalListItem } from '../../../models/proposal.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../services/encryption-content.service';
import { HiddenContentItem, MutedContentItem, MutedContentType } from '../../../models/notification.model';
import { NotificationService } from '../../../services/notification.service';
import { AdultContentService } from '../../../services/adult-content.service';
import { ContentPreferenceService } from '../../../services/content-preference.service';
import { NavigationService } from '../../../services/navigation.service';

@Component({
  selector: 'app-discussion-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, AdultContentGateComponent, ContentBadgeComponent],
  templateUrl: './discussion-list.component.html',
  styleUrl: './discussion-list.component.css'
})
export class DiscussionListComponent implements OnInit, OnDestroy {
  config!: DiscussionConfig;
  items: DiscussionListItem[] = [];
  loading = true;
  errorMessage = '';
  crewId = 0;
  openMenuItemId: number | null = null;
  mutedItems: MutedContentItem[] = [];
  hiddenItems: HiddenContentItem[] = [];
  showHiddenExpanded = false;
  showAdultGate = false;
  pendingItem: DiscussionListItem | null = null;
  resourceCounts: Record<string, number> = {};
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private discussionService = inject(CrewDiscussionService);
  private discussionCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private notificationService = inject(NotificationService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private encryptionContent = inject(EncryptionContentService);
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    const kind = this.route.snapshot.data['discussionKind'] as DiscussionKind;
    this.config = getDiscussionConfig(kind);

    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadPosts());

    this.backButton = this.navigation.createBackButton([this.config.backRoute]);
    this.notificationService.refreshBadges();
    this.notificationService.resourceCounts$.subscribe(counts => {
      this.resourceCounts = counts;
    });

    this.createButton = {
      label: `Create ${this.config.label}`,
      type: 'primary',
      onClick: () => this.router.navigate([this.config.createRoute])
    };

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        this.contentPreferenceService.ensureLoaded().subscribe();
        this.loadMutes();
        this.loadHidden();
        this.loadPosts();
        this.encryptionReload?.markInitialLoadDone();
      },
      error: () => {
        this.errorMessage = 'Failed to load crew membership';
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

  get visibleItems(): DiscussionListItem[] {
    return this.items.filter(item =>
      !this.isItemHidden(item.id) && this.adultContentService.shouldShowEntry(item.isAdultContent)
    );
  }

  get hiddenItemsList(): DiscussionListItem[] {
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

  toggleMute(item: DiscussionListItem, event: Event) {
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
          this.toastService.success(`${this.config.label} muted`);
        } else {
          this.mutedItems = this.mutedItems.filter(
            entry => !(entry.contentType === contentType && entry.resourceId === item.id)
          );
          this.toastService.success(`${this.config.label} unmuted`);
        }
      },
      error: () => this.toastService.error('Failed to update mute setting')
    });
  }

  hideItem(item: DiscussionListItem, event: Event) {
    event.stopPropagation();
    this.openMenuItemId = null;
    const contentType = this.muteContentType();
    this.notificationService.setHidden(contentType, item.id, true).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || `Failed to hide ${this.config.label.toLowerCase()}`);
          return;
        }
        this.hiddenItems = [...this.hiddenItems, { contentType, resourceId: item.id }];
        if (!this.isItemMuted(item.id)) {
          this.mutedItems = [...this.mutedItems, { contentType, resourceId: item.id }];
        }
        this.toastService.success(`${this.config.label} hidden`);
      },
      error: () => this.toastService.error(`Failed to hide ${this.config.label.toLowerCase()}`)
    });
  }

  unhideItem(item: DiscussionListItem, event: Event) {
    event.stopPropagation();
    this.openMenuItemId = null;
    const contentType = this.muteContentType();
    this.notificationService.setHidden(contentType, item.id, false).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || `Failed to unhide ${this.config.label.toLowerCase()}`);
          return;
        }
        this.hiddenItems = this.hiddenItems.filter(
          entry => !(entry.contentType === contentType && entry.resourceId === item.id)
        );
        this.toastService.success(`${this.config.label} unhidden`);
      },
      error: () => this.toastService.error(`Failed to unhide ${this.config.label.toLowerCase()}`)
    });
  }

  editItem(item: DiscussionListItem, event: Event) {
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

  formatActivity(date: Date): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  shouldBlurThumbnail(item: DiscussionListItem): boolean {
    return this.adultContentService.shouldBlurThumbnail(item.isAdultContent);
  }

  openPost(item: DiscussionListItem) {
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

  private navigateToPost(item: DiscussionListItem) {
    this.router.navigate([this.config.detailRoute(item.id)]);
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

    this.discussionService.getPosts(this.config).subscribe({
      next: async items => {
        try {
          if (this.crewId > 0) {
            this.items = await this.discussionCrypto.decryptListItems(
              items as ProposalListItem[],
              this.crewId
            ) as DiscussionListItem[];
          } else {
            this.items = items;
          }
        } catch (error: unknown) {
          this.items = [];
          this.errorMessage = error instanceof Error
            ? error.message
            : `Failed to decrypt ${this.config.labelPlural.toLowerCase()}`;
          this.toastService.error(this.errorMessage);
        } finally {
          this.loading = false;
        }
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? `Failed to load ${this.config.labelPlural.toLowerCase()}`;
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
