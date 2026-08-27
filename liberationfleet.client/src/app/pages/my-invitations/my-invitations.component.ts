import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { CrewService } from '../../services/crew.service';
import { FleetService } from '../../services/fleet.service';
import { ToastService } from '../../components/toast/toast.component';
import { NotificationContentService } from '../../services/notification-content.service';
import { CrewInvitation } from '../../models/crew.model';
import { FleetJoinRequestListItem } from '../../models/fleet.model';

export type InvitationListKind = 'crew' | 'fleet';

export interface InvitationListItem {
  kind: InvitationListKind;
  id: number;
  title: string;
  subtitle: string;
  createdAt: string;
  crewInvitationId?: number;
  proposalId?: number;
}

@Component({
  selector: 'app-my-invitations',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './my-invitations.component.html',
  styleUrl: './my-invitations.component.css'
})
export class MyInvitationsComponent implements OnInit {
  backButton: ActionBarButton;
  loading = true;
  errorMessage = '';
  items: InvitationListItem[] = [];

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);
  private notificationContent = inject(NotificationContentService);

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/crew']);
  }

  ngOnInit() {
    this.notificationContent.markVisited('/app/crew/invitations');
    // Fleet invitations on this page open crew proposals.
    this.notificationContent.markVisited('/app/crew/proposals');
    this.loadInvitations();
  }

  loadInvitations() {
    this.loading = true;
    this.errorMessage = '';

    forkJoin({
      crew: this.crewService.getMyInvitations().pipe(
        catchError(error => {
          this.toastService.error(error.error?.message || 'Failed to load crew invitations');
          return of({ success: false, message: 'Failed to load crew invitations', items: [] as CrewInvitation[] });
        })
      ),
      membership: this.crewService.getMembership().pipe(
        catchError(() => of({ hasCrew: false }))
      ),
      fleet: this.fleetService.getMyJoinRequests().pipe(
        catchError(() => of({ success: false, message: '', items: [] as FleetJoinRequestListItem[] }))
      )
    }).subscribe({
      next: ({ crew, membership, fleet }) => {
        this.loading = false;
        const next: InvitationListItem[] = [];

        if (crew.success) {
          for (const invitation of crew.items ?? []) {
            next.push({
              kind: 'crew',
              id: invitation.id,
              title: invitation.crewName,
              subtitle: `Invited by ${invitation.inviterUsername}`,
              createdAt: invitation.createdAt,
              crewInvitationId: invitation.id
            });
          }
        } else if (!membership.hasCrew) {
          this.errorMessage = crew.message || 'Failed to load invitations';
        }

        if (membership.hasCrew && fleet.success) {
          for (const request of fleet.items ?? []) {
            next.push({
              kind: 'fleet',
              id: request.proposalId,
              title: request.fleetName,
              subtitle: 'Fleet invitation for your crew',
              createdAt: request.createdAt,
              proposalId: request.proposalId
            });
          }
        }

        next.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
        this.items = next;

        if (!crew.success && !membership.hasCrew && this.items.length === 0 && !this.errorMessage) {
          this.errorMessage = crew.message || 'Failed to load invitations';
        }
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error.error?.message || 'Failed to load invitations';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  openInvitation(item: InvitationListItem) {
    if (item.kind === 'crew' && item.crewInvitationId) {
      this.router.navigate(['/app/crew/invitations', item.crewInvitationId]);
      return;
    }

    if (item.kind === 'fleet' && item.proposalId) {
      this.router.navigate(['/app/crew/proposals', item.proposalId]);
    }
  }

  formatCreatedAt(value: string): string {
    return new Date(value).toLocaleString();
  }

  kindLabel(kind: InvitationListKind): string {
    return kind === 'crew' ? 'Crew' : 'Fleet';
  }
}
