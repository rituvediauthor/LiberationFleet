import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  OnInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription, of } from 'rxjs';
import { catchError, startWith, switchMap } from 'rxjs/operators';
import { NavLayoutComponent } from '../../../components/nav-layout/nav-layout.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { DonationCampaignWidgetComponent } from '../../../components/donation-campaign-widget/donation-campaign-widget.component';
import { BrandLogoComponent } from '../../../components/brand-logo/brand-logo.component';
import { HubLoadingComponent } from '../../../components/hub-loading/hub-loading.component';
import { FleetService } from '../../../services/fleet.service';
import { GiftService } from '../../../services/gift.service';
import { NotificationService } from '../../../services/notification.service';
import { CryptoSessionService } from '../../../services/crypto/crypto-session.service';
import { EncryptedImageCacheService } from '../../../services/encrypted-image-cache.service';
import { FleetStatus } from '../../../models/fleet.model';
import { NextAidInfo } from '../../../models/gift.model';
import {
  CrewNotificationAreaCounts,
  emptyAreaCounts
} from '../../../utils/notification-area.util';
import { ForumListPrefetchService } from '../../../services/forum-list-prefetch.service';
import { FleetDashboardPreviewService } from '../../../services/fleet-dashboard-preview.service';
import { CollapsibleSectionComponent } from '../../../components/collapsible-section/collapsible-section.component';
import { CrewDashboardPreviewComponent } from '../../../components/crew-dashboard-preview/crew-dashboard-preview.component';
import {
  CrewDashboardPreviewKind,
  CrewDashboardPreviewOpenTarget,
  CrewDashboardPreviews
} from '../../../models/crew-dashboard-preview.model';
import { ToastService } from '../../../components/toast/toast.component';
import { MuteDuration, MutedContentItem, MutedContentType, withMute } from '../../../models/notification.model';
import { MuteDurationDialogComponent } from '../../../components/mute-duration-dialog/mute-duration-dialog.component';
import { CopyTextButtonComponent } from '../../../components/copy-text-button/copy-text-button.component';
import { PullRefreshController } from '../../../utils/pull-refresh.controller';

type FleetPreviewKey = keyof CrewDashboardPreviews;

@Component({
  selector: 'app-fleet-home',
  standalone: true,
  imports: [
    CommonModule,
    NavLayoutComponent,
    ContentBadgeComponent,
    DonationCampaignWidgetComponent,
    BrandLogoComponent,
    HubLoadingComponent,
    CollapsibleSectionComponent,
    CrewDashboardPreviewComponent,
    MuteDurationDialogComponent,
    CopyTextButtonComponent
  ],
  templateUrl: './fleet-home.component.html',
  styleUrl: './fleet-home.component.css'
})
export class FleetHomeComponent implements OnInit, AfterViewInit, OnDestroy {
  status: FleetStatus | null = null;
  nextAid: NextAidInfo | null = null;
  showNextAidWidget = false;
  nextAidLoaded = false;
  mutedItems: MutedContentItem[] = [];
  muteTarget: { contentType: MutedContentType; resourceId: number; noun: string } | null = null;
  libraryOfThingsEnabled = true;
  loading = true;
  loadError = false;
  /** Status may load while locked; dashboard chrome waits for decrypt unlock. */
  encryptionReady = false;
  areaCounts: CrewNotificationAreaCounts = emptyAreaCounts();
  /** Opens Administrative once when any nested item has a badge; user can still collapse. */
  adminSectionExpanded = false;
  private adminSectionAutoOpened = false;
  fleetImageSrc: string | null = null;
  previews: CrewDashboardPreviews = {
    forum: null,
    chat: null,
    gift: null,
    library: null
  };
  previewLoading: Record<FleetPreviewKey, boolean> = {
    forum: false,
    chat: false,
    gift: false,
    library: false
  };
  pullDistance = 0;
  refreshing = false;

  private router = inject(Router);
  private fleetService = inject(FleetService);
  private giftService = inject(GiftService);
  private notificationService = inject(NotificationService);
  private toastService = inject(ToastService);
  private cryptoSession = inject(CryptoSessionService);
  private images = inject(EncryptedImageCacheService);
  private forumPrefetch = inject(ForumListPrefetchService);
  private dashboardPreviews = inject(FleetDashboardPreviewService);
  private host = inject(ElementRef<HTMLElement>);
  private cdr = inject(ChangeDetectorRef);
  private subscriptions = new Subscription();
  private previewLoadGeneration = 0;
  private readonly pullRefresh = new PullRefreshController({
    edges: ['top'],
    isBusy: () => this.loading || this.refreshing || !this.encryptionReady,
    onRefresh: () => void this.refreshFromPull(),
    onDistanceChange: distance => {
      this.pullDistance = distance;
      this.cdr.markForCheck();
    }
  });

  ngOnInit() {
    this.notificationService.getMutes().subscribe({
      next: response => {
        this.mutedItems = response.items ?? [];
      }
    });
    this.paintPersistedStatus();
    this.encryptionReady = this.cryptoSession.isUnlocked();
    this.subscriptions.add(
      this.cryptoSession.unlocked$.subscribe(unlocked => {
        this.encryptionReady = unlocked;
        if (unlocked) {
          if (this.status?.fleetId) {
            this.forumPrefetch.prefetchFleetSpace(this.status.fleetId);
            void this.refreshDashboardPreviews(this.status.fleetId);
          }
          void this.refreshFleetImage();
        } else {
          this.fleetImageSrc = null;
          this.clearPreviews();
        }
      })
    );

    this.subscriptions.add(
      this.fleetService.statusChanged$.pipe(
        startWith(undefined),
        switchMap(() =>
          this.fleetService.getStatus().pipe(
            catchError(() => {
              if (!this.status) {
                this.loadError = true;
                this.showNextAidWidget = false;
                this.nextAidLoaded = true;
                this.fleetImageSrc = null;
                this.loading = false;
              }
              return of(null);
            })
          )
        )
      ).subscribe(status => {
        if (!status) {
          return;
        }

        if (status.hasFleet && status.needsRuleAcceptance) {
          this.router.navigate(['/app/fleet/accept-rules']);
          return;
        }

        this.applyStatus(status, true);
      })
    );

    this.notificationService.refreshBadges();
    this.subscriptions.add(
      this.notificationService.areaCounts$.subscribe(counts => {
        this.areaCounts = counts;
        this.maybeExpandAdminForBadges(counts);
      })
    );
  }

  ngAfterViewInit() {
    // .nav-content is a descendant (inside app-nav-layout), not an ancestor.
    const scrollEl = (this.host.nativeElement as HTMLElement).querySelector('.nav-content') as HTMLElement | null;
    this.pullRefresh.bind(scrollEl);
  }

  ngOnDestroy() {
    this.pullRefresh.unbind();
    this.subscriptions.unsubscribe();
  }

  retryStatus() {
    this.loading = true;
    this.loadError = false;
    this.fleetService.clearStatusCache();
  }

  private paintPersistedStatus() {
    const cached = this.fleetService.peekPersistedStatus();
    if (!cached || cached.needsRuleAcceptance) {
      return;
    }
    this.applyStatus(cached, false);
  }

  private applyStatus(status: FleetStatus, live: boolean) {
    this.status = status;
    this.loadError = false;
    this.libraryOfThingsEnabled = status.libraryOfThingsEnabled !== false;
    this.showNextAidWidget = !!status.hasFleet;
    if (live) {
      this.nextAid = null;
      this.nextAidLoaded = !this.showNextAidWidget;
    } else if (this.showNextAidWidget) {
      this.nextAidLoaded = false;
    } else {
      this.nextAidLoaded = true;
    }
    this.loading = false;
    void this.refreshFleetImage();

    if (status.hasFleet && status.fleetId) {
      this.applyCachedDashboardPreviews(status.fleetId);
      if (this.cryptoSession.isUnlocked()) {
        this.forumPrefetch.prefetchFleetSpace(status.fleetId);
        if (live) {
          void this.refreshDashboardPreviews(status.fleetId);
        }
      }
    } else {
      this.clearPreviews();
    }

    if (live && this.showNextAidWidget) {
      this.fleetService.getNextAid().subscribe({
        next: result => {
          this.nextAid = result.success ? (result.nextAid ?? null) : null;
          this.nextAidLoaded = true;
        },
        error: () => {
          this.nextAid = null;
          this.nextAidLoaded = true;
        }
      });
    }

    if (live && status.hasFleet) {
      this.fleetService.getCurrent().subscribe({
        next: result => {
          if (result.success && result.fleet) {
            this.libraryOfThingsEnabled = result.fleet.libraryOfThingsEnabled !== false;
          }
        }
      });
    }
  }

  private async refreshFleetImage() {
    const fleetId = this.status?.fleetId;
    const resourceId = this.status?.imageResourceId;
    if (!fleetId || !resourceId) {
      this.fleetImageSrc = null;
      return;
    }

    this.fleetImageSrc = await this.images.getDataUrl(
      { fleetId },
      resourceId,
      'ImageAsset'
    );
  }

  get nextAidHeadline(): string {
    if (!this.nextAid) {
      return 'No aid needed right now';
    }
    if (this.nextAid.isUnlimitedNeed) {
      if (this.nextAid.isCurrentUserRecipient) {
        return `You're next as Representative — open for aid (no maximum)`;
      }
      return `${this.nextAid.recipientName} is Representative — open for aid (no maximum)`;
    }
    if (this.nextAid.isCurrentUserRecipient) {
      return `You're next! $${this.nextAid.amount} still needed`;
    }
    return `${this.nextAid.recipientName} needs $${this.nextAid.amount}`;
  }

  get nextAidHandle(): string | null {
    if (!this.nextAid || this.nextAid.isCurrentUserRecipient) {
      return null;
    }
    return this.nextAid.platformHandle?.trim() || null;
  }

  get spaceMuted(): boolean {
    const fleetId = this.status?.fleetId;
    return !!fleetId && this.notificationService.isMuted(this.mutedItems, 'FleetSpace', fleetId);
  }

  openSpaceMute(event: Event) {
    event.stopPropagation();
    const fleetId = this.status?.fleetId;
    if (!fleetId) {
      return;
    }
    this.muteTarget = { contentType: 'FleetSpace', resourceId: fleetId, noun: 'Posts' };
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
        this.toastService.success(muted ? 'Posts muted' : 'Posts unmuted');
      },
      error: () => this.toastService.error('Failed to update mute setting')
    });
  }

  get nextAidPlatformLine(): string | null {
    if (!this.nextAid || this.nextAid.isCurrentUserRecipient) {
      return null;
    }

    switch (this.nextAid.platformDisplayKind) {
      case 'preferred':
      case 'common':
        if (this.nextAid.platformName && this.nextAid.platformHandle) {
          return `${this.nextAid.platformName}: ${this.nextAid.platformHandle}`;
        }
        if (this.nextAid.platformName) {
          return this.nextAid.platformName;
        }
        return null;
      case 'middlemanNeeded':
      case 'intermediaryNeeded':
        return 'Intermediary needed';
      case 'unavailable':
        return 'No payment platform';
      default:
        return null;
    }
  }

  openPreview(preview: CrewDashboardPreviewOpenTarget) {
    if ('libraryKind' in preview) {
      if (preview.libraryKind === 'unit') {
        void this.router.navigate(['/app/fleet/library/units', preview.unit.unitId]);
      }
      return;
    }
    if ('roomId' in preview && 'messageId' in preview) {
      void this.router.navigate(['/app/fleet/chats', preview.roomId], {
        queryParams: { messageId: preview.messageId }
      });
      return;
    }
    if ('title' in preview && 'lastActivityAt' in preview) {
      void this.router.navigate(['/app/fleet/forums', preview.id]);
      return;
    }
    if ('message' in preview && 'timestamp' in preview) {
      void this.router.navigate(['/app/fleet/gift-log', preview.id]);
    }
  }

  onPreviewNeedMore(kind: CrewDashboardPreviewKind) {
    const fleetId = this.status?.fleetId;
    if (!fleetId || !this.cryptoSession.isUnlocked()) {
      return;
    }
    void this.appendDashboardPreview(fleetId, kind);
  }

  goToCreateFleet() {
    this.router.navigate(['/app/fleet/create']);
  }

  goToJoinFleet() {
    this.router.navigate(['/app/fleet/join']);
  }

  goToInvitations() {
    this.router.navigate(['/app/crew/invitations']);
  }

  goToJoinRequests() {
    this.router.navigate(['/app/fleet/join-requests']);
  }

  goToEditFleet() {
    this.router.navigate(['/app/fleet/edit']);
  }

  goToRules() {
    this.router.navigate(['/app/fleet/rules']);
  }

  goToGiftLog() {
    this.router.navigate(['/app/fleet/gift-log']);
  }

  goToNextAidAction() {
    this.giftService.navigateToNextAidAction(this.router, 'fleet');
  }

  goToLibrary() {
    this.router.navigate(['/app/fleet/library']);
  }

  goToChats() {
    this.router.navigate(['/app/fleet/chats']);
  }

  goToForums() {
    this.router.navigate(['/app/fleet/forums']);
  }

  goToProposals() {
    this.router.navigate(['/app/fleet/proposals']);
  }

  goToCrews() {
    this.router.navigate(['/app/fleet/crews']);
  }

  private applyCachedDashboardPreviews(fleetId: number): void {
    const forum = this.dashboardPreviews.peekCachedPreview(fleetId, 'forum');
    const chat = this.dashboardPreviews.peekCachedPreview(fleetId, 'chat');
    const gift = this.dashboardPreviews.peekCachedPreview(fleetId, 'gift');
    const library = this.libraryOfThingsEnabled
      ? this.dashboardPreviews.peekCachedPreview(fleetId, 'library')
      : null;

    this.previews = {
      forum: forum === undefined ? this.previews.forum : forum,
      chat: chat === undefined ? this.previews.chat : chat,
      gift: gift === undefined ? this.previews.gift : gift,
      library: library === undefined ? this.previews.library : library
    };
    this.previewLoading = {
      forum: forum === undefined,
      chat: chat === undefined,
      gift: gift === undefined,
      library: this.libraryOfThingsEnabled && library === undefined
    };
  }

  private async refreshDashboardPreviews(fleetId: number) {
    const generation = ++this.previewLoadGeneration;
    const keys: FleetPreviewKey[] = ['forum', 'chat', 'gift'];
    if (this.libraryOfThingsEnabled) {
      keys.push('library');
    } else {
      this.previews = { ...this.previews, library: null };
      this.previewLoading = { ...this.previewLoading, library: false };
    }

    await Promise.all(keys.map(key => this.refreshDashboardPreview(fleetId, key, true, generation)));
  }

  private async refreshDashboardPreview(
    fleetId: number,
    previewKey: FleetPreviewKey,
    force = false,
    generation = this.previewLoadGeneration
  ) {
    if (!this.previews[previewKey]) {
      this.previewLoading = { ...this.previewLoading, [previewKey]: true };
    }
    try {
      const next = await this.dashboardPreviews.loadPreview(fleetId, previewKey, { force });
      if (generation !== this.previewLoadGeneration || this.status?.fleetId !== fleetId) {
        return;
      }
      this.previews = { ...this.previews, [previewKey]: next };
    } finally {
      if (generation === this.previewLoadGeneration && this.status?.fleetId === fleetId) {
        this.previewLoading = { ...this.previewLoading, [previewKey]: false };
      }
    }
  }

  private clearPreviews() {
    this.previews = { forum: null, chat: null, gift: null, library: null };
    this.previewLoading = {
      forum: false,
      chat: false,
      gift: false,
      library: false
    };
  }

  private async refreshFromPull() {
    const fleetId = this.status?.fleetId;
    if (!fleetId || !this.cryptoSession.isUnlocked()) {
      this.refreshing = false;
      return;
    }
    this.refreshing = true;
    try {
      await this.refreshDashboardPreviews(fleetId);
    } finally {
      this.refreshing = false;
      this.pullDistance = 0;
      this.cdr.markForCheck();
    }
  }

  private async appendDashboardPreview(fleetId: number, kind: CrewDashboardPreviewKind) {
    const next = await this.dashboardPreviews.appendPreviewItem(fleetId, kind);
    if (this.status?.fleetId !== fleetId) {
      return;
    }
    this.previews = { ...this.previews, [kind]: next };
    this.cdr.markForCheck();
  }

  private maybeExpandAdminForBadges(counts: CrewNotificationAreaCounts): void {
    if (this.adminSectionAutoOpened) {
      return;
    }
    if ((counts.fleetProposals ?? 0) > 0
      || (counts.fleetCrewmates ?? 0) > 0
      || (counts.fleetSettings ?? 0) > 0) {
      this.adminSectionExpanded = true;
      this.adminSectionAutoOpened = true;
    }
  }
}
