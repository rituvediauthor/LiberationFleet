import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { NavLayoutComponent } from '../../../components/nav-layout/nav-layout.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { DonationCampaignWidgetComponent } from '../../../components/donation-campaign-widget/donation-campaign-widget.component';
import { BrandLogoComponent } from '../../../components/brand-logo/brand-logo.component';
import { HubLoadingComponent } from '../../../components/hub-loading/hub-loading.component';
import { FleetService } from '../../../services/fleet.service';
import { NotificationService } from '../../../services/notification.service';
import { NotificationHubService } from '../../../services/notification-hub.service';
import { CryptoSessionService } from '../../../services/crypto/crypto-session.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { FleetStatus } from '../../../models/fleet.model';
import { NextAidInfo } from '../../../models/gift.model';
import {
  CrewNotificationAreaCounts,
  emptyAreaCounts
} from '../../../utils/notification-area.util';

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
  libraryOfThingsEnabled = true;
  loading = true;
  areaCounts: CrewNotificationAreaCounts = emptyAreaCounts();
  fleetImageSrc: string | null = null;

  private router = inject(Router);
  private fleetService = inject(FleetService);
  private notificationService = inject(NotificationService);
  private notificationHub = inject(NotificationHubService);
  private cryptoSession = inject(CryptoSessionService);
  private proposalCrypto = inject(ProposalCryptoService);
  private subscriptions = new Subscription();

  ngOnInit() {
    this.subscriptions.add(
      this.cryptoSession.unlocked$.subscribe(() => void this.refreshFleetImage())
    );

    this.fleetService.getStatus().subscribe({
      next: status => {
        if (status.hasFleet && status.needsRuleAcceptance) {
          this.router.navigate(['/app/fleet/accept-rules']);
          return;
        }

        this.status = status;
        this.libraryOfThingsEnabled = status.libraryOfThingsEnabled !== false;
        this.loading = false;
        void this.refreshFleetImage();

        if (status.hasFleet) {
          this.fleetService.getNextAid().subscribe({
            next: result => {
              if (result.success && result.nextAid) {
                this.nextAid = result.nextAid;
              }
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
      },
      error: () => {
        this.loading = false;
        this.status = { hasFleet: false };
        this.fleetImageSrc = null;
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

  private async refreshFleetImage() {
    const fleetId = this.status?.fleetId;
    const resourceId = this.status?.imageResourceId;
    if (!fleetId || !resourceId || !this.cryptoSession.isUnlocked()) {
      this.fleetImageSrc = null;
      return;
    }

    this.fleetImageSrc = await this.proposalCrypto.decryptImageDataUrl(
      { fleetId },
      resourceId,
      'ImageAsset'
    );
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

  goToCreateFleet() {
    this.router.navigate(['/app/fleet/create']);
  }

  goToJoinFleet() {
    this.router.navigate(['/app/fleet/join']);
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
