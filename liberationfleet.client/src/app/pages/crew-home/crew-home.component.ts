import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { ContentBadgeComponent } from '../../components/content-badge/content-badge.component';
import { DonationCampaignWidgetComponent } from '../../components/donation-campaign-widget/donation-campaign-widget.component';
import { BrandLogoComponent } from '../../components/brand-logo/brand-logo.component';
import { HubLoadingComponent } from '../../components/hub-loading/hub-loading.component';
import { CrewService } from '../../services/crew.service';
import { GiftService } from '../../services/gift.service';
import { CrewCryptoSyncService } from '../../services/crew-crypto-sync.service';
import { CryptoSessionService } from '../../services/crypto/crypto-session.service';
import { LibraryAccessService } from '../../services/library-access.service';
import { NotificationService } from '../../services/notification.service';
import { NotificationHubService } from '../../services/notification-hub.service';
import { CrewMembershipStatus } from '../../models/crew.model';
import { NextAidInfo } from '../../models/gift.model';
import {
  CrewNotificationAreaCounts,
  emptyAreaCounts
} from '../../utils/notification-area.util';

@Component({
  selector: 'app-crew-home',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NavLayoutComponent,
    ContentBadgeComponent,
    DonationCampaignWidgetComponent,
    BrandLogoComponent,
    HubLoadingComponent
  ],
  templateUrl: './crew-home.component.html',
  styleUrl: './crew-home.component.css'
})
export class CrewHomeComponent implements OnInit, OnDestroy {
  membership: CrewMembershipStatus | null = null;
  loading = true;
  nextAid: NextAidInfo | null = null;
  seasonStarted = false;
  libraryOfThingsEnabled = true;
  areaCounts: CrewNotificationAreaCounts = emptyAreaCounts();

  private router = inject(Router);
  private crewService = inject(CrewService);
  private giftService = inject(GiftService);
  private libraryAccess = inject(LibraryAccessService);
  private crewCryptoSync = inject(CrewCryptoSyncService);
  private cryptoSession = inject(CryptoSessionService);
  private notificationService = inject(NotificationService);
  private notificationHub = inject(NotificationHubService);
  private subscriptions = new Subscription();

  ngOnInit() {
    void this.crewCryptoSync.syncActiveCrewKeyDistributions();
    this.cryptoSession.unlocked$.subscribe(unlocked => {
      if (unlocked) {
        void this.crewCryptoSync.syncActiveCrewKeyDistributions();
      }
    });

    this.crewService.getMembership().subscribe({
      next: status => {
        this.membership = status;
        this.libraryOfThingsEnabled = status.libraryOfThingsEnabled !== false;
        this.loading = false;
        if (status.hasCrew) {
          this.giftService.getSeasonStatus().subscribe({
            next: seasonStatus => {
              this.seasonStarted = seasonStatus.seasonStarted;
              if (seasonStatus.seasonStarted) {
                this.giftService.getNextAidInfo().subscribe({
                  next: info => this.nextAid = info
                });
              }
            }
          });
        }
      },
      error: () => {
        this.membership = { hasCrew: false };
        this.loading = false;
      }
    });

    this.notificationService.refreshBadges();
    this.subscriptions.add(
      this.notificationService.areaCounts$.subscribe(counts => {
        this.areaCounts = counts;
      })
    );
    this.subscriptions.add(
      this.notificationHub.notificationReceived$.subscribe(() => {
        this.notificationService.refreshBadges();
      })
    );
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
  }

  get nextAidHeadline(): string {
    if (!this.nextAid) {
      return 'No aid needed right now';
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

  goToCreateCrew() {
    this.router.navigate(['/app/crew/create']);
  }

  goToJoinCrew() {
    this.router.navigate(['/app/crew/join']);
  }

  goToJoinRequests() {
    this.router.navigate(['/app/crew/join-requests']);
  }

  goToEditCrew() {
    this.router.navigate(['/app/crew/edit']);
  }

  goToGiftLog() {
    this.giftService.navigateToGiftLogEntry(this.router);
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
}
