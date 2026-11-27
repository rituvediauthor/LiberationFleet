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
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription, of, firstValueFrom } from 'rxjs';
import { catchError, startWith, switchMap } from 'rxjs/operators';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { ContentBadgeComponent } from '../../components/content-badge/content-badge.component';
import { DonationCampaignWidgetComponent } from '../../components/donation-campaign-widget/donation-campaign-widget.component';
import { BrandLogoComponent } from '../../components/brand-logo/brand-logo.component';
import { HubLoadingComponent } from '../../components/hub-loading/hub-loading.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { CharCounterComponent } from '../../components/char-counter/char-counter.component';
import { CrewService } from '../../services/crew.service';
import { GiftService } from '../../services/gift.service';
import { CrewCryptoSyncService } from '../../services/crew-crypto-sync.service';
import { CryptoSessionService } from '../../services/crypto/crypto-session.service';
import { ProposalCryptoService } from '../../services/crypto/proposal-crypto.service';
import { EncryptedImageCacheService } from '../../services/encrypted-image-cache.service';
import { LibraryAccessService } from '../../services/library-access.service';
import { NotificationService } from '../../services/notification.service';
import { ProfileService } from '../../services/profile.service';
import { ToastService } from '../../components/toast/toast.component';
import { CrewMembershipStatus } from '../../models/crew.model';
import { NextAidInfo } from '../../models/gift.model';
import {
  CrewNotificationAreaCounts,
  emptyAreaCounts
} from '../../utils/notification-area.util';
import { ForumListPrefetchService } from '../../services/forum-list-prefetch.service';
import { CrewDashboardPreviewService } from '../../services/crew-dashboard-preview.service';
import { CrewFindPanelComponent } from '../crew-find/crew-find-panel.component';
import { CollapsibleSectionComponent } from '../../components/collapsible-section/collapsible-section.component';
import { CrewDashboardPreviewComponent } from '../../components/crew-dashboard-preview/crew-dashboard-preview.component';
import {
  CrewDashboardPreviewKind,
  CrewDashboardPreviewOpenTarget,
  CrewDashboardPreviews
} from '../../models/crew-dashboard-preview.model';
import { MuteDuration, MutedContentItem, MutedContentType, withMute } from '../../models/notification.model';
import { MuteDurationDialogComponent } from '../../components/mute-duration-dialog/mute-duration-dialog.component';
import { CopyTextButtonComponent } from '../../components/copy-text-button/copy-text-button.component';
import { truncateNotificationPreview } from '../../utils/notification-preview.util';
import { TextFieldLimits } from '../../utils/text-field-limits';
import { PullRefreshController } from '../../utils/pull-refresh.controller';

const CYCLE_THANKYOU_DISMISS_PREFIX = 'lf-cycle-thankyou-dismissed:';
type CrewPreviewKey = keyof CrewDashboardPreviews;

@Component({
  selector: 'app-crew-home',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NavLayoutComponent,
    ContentBadgeComponent,
    DonationCampaignWidgetComponent,
    BrandLogoComponent,
    HubLoadingComponent,
    ConfirmDialogComponent,
    CharCounterComponent,
    CrewFindPanelComponent,
    CollapsibleSectionComponent,
    CrewDashboardPreviewComponent,
    MuteDurationDialogComponent,
    CopyTextButtonComponent
  ],
  templateUrl: './crew-home.component.html',
  styleUrl: './crew-home.component.css'
})
export class CrewHomeComponent implements OnInit, AfterViewInit, OnDestroy {
  membership: CrewMembershipStatus | null = null;
  loading = true;
  loadError = false;
  nextAid: NextAidInfo | null = null;
  /** True only when season has started — known from membership before the menu paints. */
  showNextAidWidget = false;
  nextAidLoaded = false;
  /** Membership/status may load while locked; dashboard chrome waits for decrypt unlock. */
  encryptionReady = false;
  mutedItems: MutedContentItem[] = [];
  muteTarget: { contentType: MutedContentType; resourceId: number; noun: string } | null = null;
  libraryOfThingsEnabled = true;
  areaCounts: CrewNotificationAreaCounts = emptyAreaCounts();
  /** Opens Administrative once when any nested item has a badge; user can still collapse. */
  adminSectionExpanded = false;
  private adminSectionAutoOpened = false;
  crewImageSrc: string | null = null;
  previews: CrewDashboardPreviews = {
    forum: null,
    chat: null,
    gift: null,
    library: null
  };
  previewLoading: Record<CrewPreviewKey, boolean> = {
    forum: false,
    chat: false,
    gift: false,
    library: false
  };
  pullDistance = 0;
  refreshing = false;

  showCycleThankYouPrompt = false;
  showCycleThankYouCompose = false;
  cycleThankYouGiftId: number | null = null;
  cycleThankYouText = '';
  postingCycleThankYou = false;
  readonly cycleThankYouMaxLength = TextFieldLimits.message;

  private router = inject(Router);
  private crewService = inject(CrewService);
  private giftService = inject(GiftService);
  private libraryAccess = inject(LibraryAccessService);
  private crewCryptoSync = inject(CrewCryptoSyncService);
  private cryptoSession = inject(CryptoSessionService);
  private proposalCrypto = inject(ProposalCryptoService);
  private images = inject(EncryptedImageCacheService);
  private notificationService = inject(NotificationService);
  private forumPrefetch = inject(ForumListPrefetchService);
  private dashboardPreviews = inject(CrewDashboardPreviewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private host = inject(ElementRef<HTMLElement>);
  private cdr = inject(ChangeDetectorRef);
  private subscriptions = new Subscription();
  private previewLoadGeneration = 0;
  private readonly pullRefresh = new PullRefreshController({
    edges: ['top'],
    isBusy: () => this.loading || this.refreshing || !this.encryptionReady,
    onRefresh: () => void this.refreshFromPull(),
    onDistanceChange: (distance) => {
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
    this.paintPersistedMembership();
    void this.crewCryptoSync.syncActiveCrewKeyDistributions();
    this.encryptionReady = this.cryptoSession.isUnlocked();
    this.subscriptions.add(
      this.cryptoSession.unlocked$.subscribe(unlocked => {
        this.encryptionReady = unlocked;
        if (unlocked) {
          void this.crewCryptoSync.syncActiveCrewKeyDistributions();
          const crewId = this.membership?.crewId;
          if (crewId) {
            this.forumPrefetch.prefetchCrewSpace(crewId);
            this.applyCachedDashboardPreviews(crewId);
            void this.refreshDashboardPreviews(crewId);
          }
          if (this.membership) {
            this.maybeShowCycleThankYou(this.membership);
          }
          void this.refreshCrewImage();
        } else {
          // Never stack cycle thank-you over the recovery-phrase unlock dialog.
          this.showCycleThankYouPrompt = false;
          this.showCycleThankYouCompose = false;
          this.crewImageSrc = null;
          this.clearPreviews();
        }
      })
    );

    this.subscriptions.add(
      this.crewService.membershipChanged$.pipe(
        startWith(undefined),
        switchMap(() =>
          this.crewService.getMembership().pipe(
            catchError(() => {
              // Keep the outer subscription alive so retry via membershipChanged$ works.
              // Never map probe failures to hasCrew:false (that shows Create/Join incorrectly).
              if (!this.membership) {
                this.loadError = true;
                this.showNextAidWidget = false;
                this.nextAidLoaded = true;
                this.crewImageSrc = null;
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
        this.applyMembership(status, true);
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

  retryMembership() {
    this.loading = true;
    this.loadError = false;
    this.crewService.clearMembershipCache();
  }

  goToCrewDashboard() {
    if (!this.membership?.hasCrew) {
      return;
    }
    this.crewService.clearMembershipCache();
    void this.router.navigate(['/app/crew']);
  }

  private async refreshCrewImage() {
    const crewId = this.membership?.crewId;
    const resourceId = this.membership?.imageResourceId;
    if (!crewId || !resourceId) {
      this.crewImageSrc = null;
      return;
    }

    this.crewImageSrc = await this.images.getDataUrl(
      { crewId },
      resourceId,
      'ImageAsset'
    );
  }

  get nextAidHeadline(): string {
    if (!this.nextAid) {
      return 'No aid needed right now';
    }
    const unverifiedNote = this.nextAid.hasUnverifiedPending ? ' (unverified gifts pending)' : '';
    if (this.nextAid.isUnlimitedNeed) {
      if (this.nextAid.isCurrentUserRecipient) {
        return `You're next as Representative — open for aid (no maximum)${unverifiedNote}`;
      }
      return `${this.nextAid.recipientName} is Representative — open for aid (no maximum)${unverifiedNote}`;
    }
    if (this.nextAid.isCurrentUserRecipient) {
      return `You're next! $${this.nextAid.amount} still needed${unverifiedNote}`;
    }
    return `${this.nextAid.recipientName} needs $${this.nextAid.amount}${unverifiedNote}`;
  }

  get nextAidHandle(): string | null {
    if (!this.nextAid || this.nextAid.isCurrentUserRecipient) {
      return null;
    }
    return this.nextAid.platformHandle?.trim() || null;
  }

  get spaceMuted(): boolean {
    const crewId = this.membership?.crewId;
    return !!crewId && this.notificationService.isMuted(this.mutedItems, 'CrewSpace', crewId);
  }

  openSpaceMute(event: Event) {
    event.stopPropagation();
    const crewId = this.membership?.crewId;
    if (!crewId) {
      return;
    }
    this.muteTarget = { contentType: 'CrewSpace', resourceId: crewId, noun: 'Posts' };
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

  goToCreateCrew() {
    this.router.navigate(['/app/crew/create']);
  }

  goToJoinCrew() {
    this.router.navigate(['/app/crew/join']);
  }

  goToInvitations() {
    this.router.navigate(['/app/crew/invitations']);
  }

  goToJoinRequests() {
    this.router.navigate(['/app/crew/join-requests']);
  }

  goToEditCrew() {
    this.router.navigate(['/app/crew/edit']);
  }

  goToGiftLog() {
    this.giftService.navigateToGiftLogEntry(this.router, this.membership?.seasonStarted);
  }

  goToNextAidAction() {
    this.giftService.navigateToNextAidAction(this.router, 'crew', {
      seasonStarted: this.membership?.seasonStarted,
      userInSeason: this.membership?.isInSeason
    });
  }

  goToProposals() {
    this.router.navigate(['/app/crew/proposals']);
  }

  goToForums() {
    this.router.navigate(['/app/crew/forums']);
  }

  goToCrewmates() {
    this.router.navigate(['/app/crew/crewmates']);
  }

  goToRules() {
    this.router.navigate(['/app/crew/rules']);
  }

  goToLibraryOfThings() {
    this.libraryAccess.navigateToLibrary(this.router);
  }

  goToHowToUse() {
    this.router.navigate(['/app/how-to']);
  }

  openPreview(preview: CrewDashboardPreviewOpenTarget) {
    if ('libraryKind' in preview) {
      if (preview.libraryKind === 'unit') {
        void this.router.navigate(['/app/crew/library-of-things/units', preview.unit.unitId]);
      } else {
        void this.router.navigate(['/app/crew/library-of-things/tasks', preview.task.taskId]);
      }
      return;
    }
    if ('roomId' in preview && 'messageId' in preview) {
      void this.router.navigate(['/app/crew/chats', preview.roomId], {
        queryParams: { messageId: preview.messageId }
      });
      return;
    }
    if ('timestamp' in preview && 'message' in preview) {
      void this.router.navigate(['/app/crew/gift-log', preview.id]);
      return;
    }
    if ('lastActivityAt' in preview && 'id' in preview) {
      void this.router.navigate(['/app/crew/forums', preview.id]);
    }
  }

  onPreviewNeedMore(kind: CrewDashboardPreviewKind) {
    const crewId = this.membership?.crewId;
    if (!crewId || !this.cryptoSession.isUnlocked()) {
      return;
    }
    void this.appendDashboardPreview(crewId, kind);
  }

  private paintPersistedMembership() {
    const cached = this.crewService.peekPersistedMembership();
    if (!cached) {
      return;
    }
    this.applyMembership(cached, false);
  }

  private applyMembership(status: CrewMembershipStatus, live: boolean) {
    this.membership = status;
    this.loadError = false;
    this.libraryOfThingsEnabled = status.libraryOfThingsEnabled !== false;
    this.showNextAidWidget = !!status.hasCrew && !!status.seasonStarted;
    if (live) {
      this.nextAid = null;
      this.nextAidLoaded = !this.showNextAidWidget;
    } else if (this.showNextAidWidget) {
      this.nextAidLoaded = false;
    } else {
      this.nextAidLoaded = true;
    }
    this.loading = false;
    void this.refreshCrewImage();
    this.maybeShowCycleThankYou(status);
    if (status.hasCrew && status.crewId) {
      this.applyCachedDashboardPreviews(status.crewId);
      if (this.cryptoSession.isUnlocked()) {
        this.forumPrefetch.prefetchCrewSpace(status.crewId);
        if (live) {
          void this.refreshDashboardPreviews(status.crewId);
        }
      }
    } else {
      this.clearPreviews();
    }
    if (live && this.showNextAidWidget) {
      this.giftService.getNextAidInfo().subscribe({
        next: info => {
          this.nextAid = info;
          this.nextAidLoaded = true;
        },
        error: () => {
          this.nextAid = null;
          this.nextAidLoaded = true;
        }
      });
    }
  }

  private applyCachedDashboardPreviews(crewId: number): void {
    const forum = this.dashboardPreviews.peekCachedPreview(crewId, 'forum');
    const chat = this.dashboardPreviews.peekCachedPreview(crewId, 'chat');
    const gift = this.dashboardPreviews.peekCachedPreview(crewId, 'gift');
    const library = this.libraryOfThingsEnabled
      ? this.dashboardPreviews.peekCachedPreview(crewId, 'library')
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

  private async refreshDashboardPreviews(crewId: number) {
    const generation = ++this.previewLoadGeneration;
    const keys: CrewPreviewKey[] = ['forum', 'chat', 'gift'];
    if (this.libraryOfThingsEnabled) {
      keys.push('library');
    } else {
      this.previews = { ...this.previews, library: null };
      this.previewLoading = { ...this.previewLoading, library: false };
    }

    await Promise.all(keys.map(key => this.refreshDashboardPreview(crewId, key, true, generation)));
  }

  private async refreshDashboardPreview(
    crewId: number,
    previewKey: CrewPreviewKey,
    force = false,
    generation = this.previewLoadGeneration
  ) {
    if (!this.previews[previewKey]) {
      this.previewLoading = { ...this.previewLoading, [previewKey]: true };
    }
    try {
      const next = await this.dashboardPreviews.loadPreview(crewId, previewKey, { force });
      if (generation !== this.previewLoadGeneration || this.membership?.crewId !== crewId) {
        return;
      }
      this.previews = { ...this.previews, [previewKey]: next };
    } finally {
      if (generation === this.previewLoadGeneration && this.membership?.crewId === crewId) {
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

  private maybeExpandAdminForBadges(counts: CrewNotificationAreaCounts): void {
    if (this.adminSectionAutoOpened) {
      return;
    }
    if ((counts.crewProposals ?? 0) > 0
      || (counts.crewCrewmates ?? 0) > 0
      || (counts.crewSettings ?? 0) > 0) {
      this.adminSectionExpanded = true;
      this.adminSectionAutoOpened = true;
    }
  }

  private async refreshFromPull() {
    const crewId = this.membership?.crewId;
    if (!crewId || !this.cryptoSession.isUnlocked()) {
      this.refreshing = false;
      return;
    }
    this.refreshing = true;
    try {
      await this.refreshDashboardPreviews(crewId);
    } finally {
      this.refreshing = false;
      this.pullDistance = 0;
      this.cdr.markForCheck();
    }
  }

  private async appendDashboardPreview(crewId: number, kind: CrewDashboardPreviewKind) {
    const next = await this.dashboardPreviews.appendPreviewItem(crewId, kind);
    if (this.membership?.crewId !== crewId) {
      return;
    }
    this.previews = { ...this.previews, [kind]: next };
    this.cdr.markForCheck();
  }

  private maybeShowCycleThankYou(status: CrewMembershipStatus) {
    const giftId = status.pendingCycleThankYouGiftId ?? null;
    if (!giftId || this.isCycleThankYouDismissed(giftId)) {
      this.showCycleThankYouPrompt = false;
      this.showCycleThankYouCompose = false;
      this.cycleThankYouGiftId = null;
      return;
    }

    // Wait until encryption is unlocked so this never covers the 12-word key dialog.
    if (!this.cryptoSession.isUnlocked()) {
      this.cycleThankYouGiftId = giftId;
      this.showCycleThankYouPrompt = false;
      this.showCycleThankYouCompose = false;
      return;
    }

    this.cycleThankYouGiftId = giftId;
    this.showCycleThankYouPrompt = true;
    this.showCycleThankYouCompose = false;
  }

  onCycleThankYouSure() {
    this.showCycleThankYouPrompt = false;
    this.showCycleThankYouCompose = true;
    this.cycleThankYouText = '';
  }

  onCycleThankYouDismiss() {
    if (this.cycleThankYouGiftId) {
      this.dismissCycleThankYou(this.cycleThankYouGiftId);
    }
    this.showCycleThankYouPrompt = false;
    this.showCycleThankYouCompose = false;
    this.cycleThankYouGiftId = null;
    this.cycleThankYouText = '';
  }

  onCycleThankYouComposeCancel() {
    this.onCycleThankYouDismiss();
  }

  onCycleComposeBackdrop(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.onCycleThankYouDismiss();
    }
  }

  async postCycleThankYou() {
    const giftId = this.cycleThankYouGiftId;
    const crewId = this.membership?.crewId;
    const body = this.cycleThankYouText.trim();
    if (!giftId || !crewId || !body || this.postingCycleThankYou) {
      return;
    }
    if (!this.cryptoSession.isUnlocked()) {
      this.toastService.error('Unlock encryption to post your message.');
      return;
    }

    this.postingCycleThankYou = true;
    try {
      const profile = await firstValueFrom(this.profileService.getProfile());
      const encrypted = await this.proposalCrypto.encryptCommentPayload(
        crewId,
        {
          body,
          authorDisplayName: profile.username
        }
      );
      this.giftService.createGiftComment(giftId, {
        ...encrypted,
        notificationPreview: truncateNotificationPreview(body),
        mentionedUserIds: []
      }).subscribe({
        next: result => {
          this.postingCycleThankYou = false;
          if (result.success) {
            this.dismissCycleThankYou(giftId);
            this.showCycleThankYouCompose = false;
            this.cycleThankYouGiftId = null;
            this.cycleThankYouText = '';
            if (this.membership) {
              this.membership = { ...this.membership, pendingCycleThankYouGiftId: null };
            }
            this.toastService.success('Message posted to the Giving log');
            return;
          }
          this.toastService.error(result.message || 'Failed to post message');
        },
        error: () => {
          this.postingCycleThankYou = false;
          this.toastService.error('Failed to post message');
        }
      });
    } catch {
      this.postingCycleThankYou = false;
      this.toastService.error('Failed to encrypt message');
    }
  }

  private isCycleThankYouDismissed(giftId: number): boolean {
    try {
      return localStorage.getItem(`${CYCLE_THANKYOU_DISMISS_PREFIX}${giftId}`) === '1';
    } catch {
      return false;
    }
  }

  private dismissCycleThankYou(giftId: number) {
    try {
      localStorage.setItem(`${CYCLE_THANKYOU_DISMISS_PREFIX}${giftId}`, '1');
    } catch {
      // Ignore storage failures.
    }
  }
}
