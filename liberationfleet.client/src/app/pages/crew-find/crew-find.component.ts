import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription, of } from 'rxjs';
import { catchError, startWith, switchMap } from 'rxjs/operators';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { HubLoadingComponent } from '../../components/hub-loading/hub-loading.component';
import { CrewService } from '../../services/crew.service';
import { NavigationService } from '../../services/navigation.service';
import { CrewMembershipStatus } from '../../models/crew.model';
import { CrewFindPanelComponent } from './crew-find-panel.component';

/** While waiting for a join approval SignalR event, refresh membership on this cadence. */
const MEMBERSHIP_POLL_MS = 4000;

@Component({
  selector: 'app-crew-find',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, HubLoadingComponent, CrewFindPanelComponent],
  templateUrl: './crew-find.component.html',
  styleUrl: './crew-find.component.css'
})
export class CrewFindComponent implements OnInit, OnDestroy {
  membership: CrewMembershipStatus | null = null;
  loading = true;
  loadError = false;
  backButton: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private subscriptions = new Subscription();
  private membershipPollId: ReturnType<typeof setInterval> | null = null;
  private hadCrew: boolean | null = null;

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/profile']);
  }

  ngOnInit() {
    this.subscriptions.add(
      this.crewService.membershipChanged$.pipe(
        startWith(undefined),
        switchMap(() =>
          this.crewService.getMembership().pipe(
            catchError(() => {
              this.membership = null;
              this.loadError = true;
              this.loading = false;
              this.stopMembershipPoll();
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
        this.loading = false;

        const nowHasCrew = !!status.hasCrew;
        if (this.hadCrew === false && nowHasCrew) {
          this.stopMembershipPoll();
          void this.router.navigate(['/app/crew'], { replaceUrl: true });
        } else if (nowHasCrew) {
          this.stopMembershipPoll();
        } else {
          this.startMembershipPoll();
        }
        this.hadCrew = nowHasCrew;
      })
    );
  }

  ngOnDestroy() {
    this.stopMembershipPoll();
    this.subscriptions.unsubscribe();
  }

  get hasCrew(): boolean {
    return !!this.membership?.hasCrew;
  }

  retryMembership() {
    this.loading = true;
    this.loadError = false;
    this.crewService.clearMembershipCache();
  }

  goToCreateCrew() {
    void this.router.navigate(['/app/crew/create']);
  }

  goToJoinCrew() {
    void this.router.navigate(['/app/crew/join']);
  }

  goToInvitations() {
    void this.router.navigate(['/app/crew/invitations']);
  }

  goToJoinRequests() {
    void this.router.navigate(['/app/crew/join-requests']);
  }

  goToHowToUse() {
    void this.router.navigate(['/app/how-to']);
  }

  goToCrewDashboard() {
    if (!this.hasCrew) {
      return;
    }
    this.crewService.clearMembershipCache();
    void this.router.navigate(['/app/crew']);
  }

  private startMembershipPoll() {
    if (this.membershipPollId != null) {
      return;
    }
    this.membershipPollId = setInterval(() => {
      this.crewService.getMembership(true).subscribe({
        error: () => undefined
      });
    }, MEMBERSHIP_POLL_MS);
  }

  private stopMembershipPoll() {
    if (this.membershipPollId == null) {
      return;
    }
    clearInterval(this.membershipPollId);
    this.membershipPollId = null;
  }
}
