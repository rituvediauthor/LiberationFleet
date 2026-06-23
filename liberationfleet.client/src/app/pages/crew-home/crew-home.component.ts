import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { CrewService } from '../../services/crew.service';
import { GiftService } from '../../services/gift.service';
import { CrewCryptoSyncService } from '../../services/crew-crypto-sync.service';
import { CryptoSessionService } from '../../services/crypto/crypto-session.service';
import { CrewMembershipStatus } from '../../models/crew.model';
import { NextAidInfo } from '../../models/gift.model';

@Component({
  selector: 'app-crew-home',
  standalone: true,
  imports: [CommonModule, NavLayoutComponent],
  templateUrl: './crew-home.component.html',
  styleUrl: './crew-home.component.css'
})
export class CrewHomeComponent implements OnInit {
  membership: CrewMembershipStatus | null = null;
  nextAid: NextAidInfo | null = null;
  seasonStarted = false;

  private router = inject(Router);
  private crewService = inject(CrewService);
  private giftService = inject(GiftService);
  private crewCryptoSync = inject(CrewCryptoSyncService);
  private cryptoSession = inject(CryptoSessionService);

  ngOnInit() {
    void this.crewCryptoSync.syncActiveCrewKeyDistributions();
    this.cryptoSession.unlocked$.subscribe(unlocked => {
      if (unlocked) {
        void this.crewCryptoSync.syncActiveCrewKeyDistributions();
      }
    });

    this.crewService.getMembership().subscribe(status => {
      this.membership = status;
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
    });
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
        return 'Middle-man needed';
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

  goToEditCrew() {
    this.router.navigate(['/app/crew/edit']);
  }

  goToGiftLog() {
    this.giftService.navigateToGiftLogEntry(this.router);
  }

  goToChats() {
    this.router.navigate(['/app/crew/chats']);
  }

  goToProposals() {
    this.router.navigate(['/app/crew/proposals']);
  }

  goToProjects() {
    this.router.navigate(['/app/crew/projects']);
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
    this.router.navigate(['/app/crew/library-of-things']);
  }
}
