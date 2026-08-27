import { Component, inject, OnDestroy, OnInit } from '@angular/core';
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
import { truncateNotificationPreview } from '../../utils/notification-preview.util';
import { TextFieldLimits } from '../../utils/text-field-limits';

const CYCLE_THANKYOU_DISMISS_PREFIX = 'lf-cycle-thankyou-dismissed:';

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
    CharCounterComponent
  ],
  templateUrl: './crew-home.component.html',
  styleUrl: './crew-home.component.css'
})
export class CrewHomeComponent implements OnInit, OnDestroy {
  membership: CrewMembershipStatus | null = null;
  loading = true;
  loadError = false;
  nextAid: NextAidInfo | null = null;
  /** True only when season has started — known from membership before the menu paints. */
  showNextAidWidget = false;
  nextAidLoaded = false;
  libraryOfThingsEnabled = true;
  areaCounts: CrewNotificationAreaCounts = emptyAreaCounts();
  crewImageSrc: string | null = null;

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
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private subscriptions = new Subscription();

  ngOnInit() {
    void this.crewCryptoSync.syncActiveCrewKeyDistributions();
    this.subscriptions.add(
      this.cryptoSession.unlocked$.subscribe(unlocked => {
        if (unlocked) {
          void this.crewCryptoSync.syncActiveCrewKeyDistributions();
          const crewId = this.membership?.crewId;
          if (crewId) {
            this.forumPrefetch.prefetchCrewSpace(crewId);
          }
        }
        void this.refreshCrewImage();
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
              this.membership = null;
              this.loadError = true;
              this.showNextAidWidget = false;
              this.nextAidLoaded = true;
              this.crewImageSrc = null;
              this.loading = false;
              return of(null);
            })
          )
        )
      ).subscribe(status => {
        if (!status) {
          return;
        }
        this.membership = status;
        this.loadError = false;
        this.libraryOfThingsEnabled = status.libraryOfThingsEnabled !== false;
        this.showNextAidWidget = !!status.hasCrew && !!status.seasonStarted;
        this.nextAid = null;
        this.nextAidLoaded = !this.showNextAidWidget;
        this.loading = false;
        void this.refreshCrewImage();
        this.maybeShowCycleThankYou(status);
        if (status.hasCrew && status.crewId) {
          this.forumPrefetch.prefetchCrewSpace(status.crewId);
        }
        if (this.showNextAidWidget) {
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
      })
    );

    this.notificationService.refreshBadges();
    this.subscriptions.add(
      this.notificationService.areaCounts$.subscribe(counts => {
        this.areaCounts = counts;
      })
    );
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
  }

  retryMembership() {
    this.loading = true;
    this.loadError = false;
    this.crewService.clearMembershipCache();
  }

  private async refreshCrewImage() {
    const crewId = this.membership?.crewId;
    const resourceId = this.membership?.imageResourceId;
    if (!crewId || !resourceId || !this.cryptoSession.isUnlocked()) {
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

  goToEmergencyRequests() {
    this.router.navigate(['/app/crew/emergency-requests']);
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

  private maybeShowCycleThankYou(status: CrewMembershipStatus) {
    const giftId = status.pendingCycleThankYouGiftId ?? null;
    if (!giftId || this.isCycleThankYouDismissed(giftId)) {
      this.showCycleThankYouPrompt = false;
      this.showCycleThankYouCompose = false;
      this.cycleThankYouGiftId = null;
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
