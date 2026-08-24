import { Component, inject, OnDestroy, OnInit } from '@angular/core';
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

@Component({
  selector: 'app-fleet-home',
  standalone: true,
  imports: [
    CommonModule,
    NavLayoutComponent,
    ContentBadgeComponent,
    DonationCampaignWidgetComponent,
    BrandLogoComponent,
    HubLoadingComponent
  ],
  templateUrl: './fleet-home.component.html',
  styleUrl: './fleet-home.component.css'
})
export class FleetHomeComponent implements OnInit, OnDestroy {
  status: FleetStatus | null = null;
  nextAid: NextAidInfo | null = null;
  showNextAidWidget = false;
  nextAidLoaded = false;
  libraryOfThingsEnabled = true;
  loading = true;
  loadError = false;
  areaCounts: CrewNotificationAreaCounts = emptyAreaCounts();
  fleetImageSrc: string | null = null;

  private router = inject(Router);
  private fleetService = inject(FleetService);
  private giftService = inject(GiftService);
  private notificationService = inject(NotificationService);
  private cryptoSession = inject(CryptoSessionService);
  private images = inject(EncryptedImageCacheService);
  private forumPrefetch = inject(ForumListPrefetchService);
  private subscriptions = new Subscription();

  ngOnInit() {
    this.subscriptions.add(
      this.cryptoSession.unlocked$.subscribe(unlocked => {
        if (unlocked && this.status?.fleetId) {
          this.forumPrefetch.prefetchFleetSpace(this.status.fleetId);
        }
        void this.refreshFleetImage();
      })
    );

    this.subscriptions.add(
      this.fleetService.statusChanged$.pipe(
        startWith(undefined),
        switchMap(() =>
          this.fleetService.getStatus().pipe(
            catchError(() => {
              // Keep the outer subscription alive so retry via statusChanged$ works.
              this.status = null;
              this.loadError = true;
              this.showNextAidWidget = false;
              this.nextAidLoaded = true;
              this.fleetImageSrc = null;
              this.loading = false;
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

        this.status = status;
        this.loadError = false;
        this.libraryOfThingsEnabled = status.libraryOfThingsEnabled !== false;
        this.showNextAidWidget = !!status.hasFleet;
        this.nextAid = null;
        this.nextAidLoaded = !this.showNextAidWidget;
        this.loading = false;
        void this.refreshFleetImage();

        if (status.hasFleet && status.fleetId) {
          this.forumPrefetch.prefetchFleetSpace(status.fleetId);
        }

        if (this.showNextAidWidget) {
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

        if (status.hasFleet) {
          this.fleetService.getCurrent().subscribe({
            next: result => {
              if (result.success && result.fleet) {
                this.libraryOfThingsEnabled = result.fleet.libraryOfThingsEnabled !== false;
              }
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

  retryStatus() {
    this.loading = true;
    this.loadError = false;
    this.fleetService.clearStatusCache();
  }

  private async refreshFleetImage() {
    const fleetId = this.status?.fleetId;
    const resourceId = this.status?.imageResourceId;
    if (!fleetId || !resourceId || !this.cryptoSession.isUnlocked()) {
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

  goToEmergencyRequests() {
    this.router.navigate(['/app/fleet/emergency-requests']);
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
}
