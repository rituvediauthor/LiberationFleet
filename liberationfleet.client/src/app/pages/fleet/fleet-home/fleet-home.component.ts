import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavLayoutComponent } from '../../../components/nav-layout/nav-layout.component';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetStatus } from '../../../models/fleet.model';
import { NextAidInfo } from '../../../models/gift.model';

@Component({
  selector: 'app-fleet-home',
  standalone: true,
  imports: [CommonModule, NavLayoutComponent],
  templateUrl: './fleet-home.component.html',
  styleUrl: './fleet-home.component.css'
})
export class FleetHomeComponent implements OnInit {
  status: FleetStatus | null = null;
  nextAid: NextAidInfo | null = null;
  libraryOfThingsEnabled = true;
  loading = true;

  private router = inject(Router);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.fleetService.getStatus().subscribe({
      next: status => {
        if (status.hasFleet && status.needsRuleAcceptance) {
          this.router.navigate(['/app/fleet/accept-rules']);
          return;
        }

        this.status = status;
        this.libraryOfThingsEnabled = status.libraryOfThingsEnabled !== false;
        this.loading = false;

        if (status.hasFleet && status.allowCrossCrewGiving) {
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
      }
    });
  }

  get allowCrossCrewGiving(): boolean {
    return !!this.status?.allowCrossCrewGiving;
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
    if (!this.allowCrossCrewGiving) {
      this.toastService.error('Your crew has disabled cross-crew giving');
      return;
    }
    this.router.navigate(['/app/fleet/gift-log']);
  }

  goToEmergencyRequests() {
    if (!this.allowCrossCrewGiving) {
      this.toastService.error('Your crew has disabled cross-crew giving');
      return;
    }
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
