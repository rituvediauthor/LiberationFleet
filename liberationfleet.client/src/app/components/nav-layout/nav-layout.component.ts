import { Component, Input, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Subscription } from 'rxjs';
import { FallibleFooterComponent } from '../fallible-footer/fallible-footer.component';
import { BrandLogoComponent } from '../brand-logo/brand-logo.component';
import { ContentBadgeComponent } from '../content-badge/content-badge.component';
import { UserAvatarComponent } from '../user-avatar/user-avatar.component';
import { NotificationService } from '../../services/notification.service';
import { NotificationHubService } from '../../services/notification-hub.service';
import { CrewService } from '../../services/crew.service';
import { FleetService } from '../../services/fleet.service';
import { ProfileService } from '../../services/profile.service';
import { EncryptedImageCacheService } from '../../services/encrypted-image-cache.service';
import { CryptoSessionService } from '../../services/crypto/crypto-session.service';

export type NavTab = 'crew' | 'fleet' | 'friends' | 'notifications' | 'profile';

@Component({
  selector: 'app-nav-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    RouterLinkActive,
    FallibleFooterComponent,
    BrandLogoComponent,
    ContentBadgeComponent,
    UserAvatarComponent
  ],
  templateUrl: './nav-layout.component.html',
  styleUrl: './nav-layout.component.css'
})
export class NavLayoutComponent implements OnInit, OnDestroy {
  @Input() activeTab: NavTab = 'crew';

  unreadCount = 0;
  crewId = 0;
  fleetId = 0;
  crewImageSrc: string | null = null;
  fleetImageSrc: string | null = null;
  profileAvatarResourceId: string | null = null;
  private crewImageResourceId: string | null = null;
  private fleetImageResourceId: string | null = null;

  private notificationService = inject(NotificationService);
  private notificationHub = inject(NotificationHubService);
  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
  private profileService = inject(ProfileService);
  private images = inject(EncryptedImageCacheService);
  private cryptoSession = inject(CryptoSessionService);
  private subscriptions = new Subscription();

  ngOnInit() {
    this.notificationService.refreshBadges();
    this.subscriptions.add(
      this.notificationService.unreadCount$.subscribe(count => {
        this.unreadCount = count;
      })
    );
    this.subscriptions.add(
      this.notificationHub.notificationReceived$.subscribe(() => {
        this.notificationService.refreshBadges();
      })
    );
    this.subscriptions.add(
      this.notificationHub.unreadCountUpdated$.subscribe(count => {
        this.notificationService.setUnreadCount(count);
        this.notificationService.refreshBadges();
      })
    );
    this.subscriptions.add(
      this.crewService.getMembership().subscribe({
        next: membership => {
          this.crewId = membership.crewId ?? 0;
          this.crewImageResourceId = membership.imageResourceId ?? null;
          void this.refreshCrewImage();
        }
      })
    );
    this.subscriptions.add(
      this.fleetService.getStatus().subscribe({
        next: status => {
          this.fleetId = status.fleetId ?? 0;
          this.fleetImageResourceId = status.imageResourceId ?? null;
          void this.refreshFleetImage();
        }
      })
    );
    this.subscriptions.add(
      this.profileService.getProfile().subscribe({
        next: profile => {
          this.profileAvatarResourceId = profile.avatarResourceId ?? null;
        }
      })
    );
    this.subscriptions.add(
      this.cryptoSession.unlocked$.subscribe(unlocked => {
        if (unlocked) {
          void this.refreshCrewImage();
          void this.refreshFleetImage();
        }
      })
    );
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
  }

  private async refreshCrewImage(): Promise<void> {
    if (!this.crewId || !this.crewImageResourceId) {
      this.crewImageSrc = null;
      return;
    }
    this.crewImageSrc = await this.images.getDataUrl(
      { crewId: this.crewId },
      this.crewImageResourceId,
      'ImageAsset'
    );
  }

  private async refreshFleetImage(): Promise<void> {
    if (!this.fleetId || !this.fleetImageResourceId) {
      this.fleetImageSrc = null;
      return;
    }
    this.fleetImageSrc = await this.images.getDataUrl(
      { fleetId: this.fleetId },
      this.fleetImageResourceId,
      'ImageAsset'
    );
  }
}
