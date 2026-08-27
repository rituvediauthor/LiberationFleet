import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { UserAvatarComponent } from '../../../components/user-avatar/user-avatar.component';
import { FleetService } from '../../../services/fleet.service';
import { CrewmateService } from '../../../services/crewmate.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetCrewmateProfile } from '../../../models/fleet.model';
import { mapFriendshipState } from '../../../models/crewmate.model';

@Component({
  selector: 'app-fleet-crewmate-detail',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ConfirmDialogComponent, UserAvatarComponent],
  templateUrl: './fleet-crewmate-detail.component.html',
  styleUrl: './fleet-crewmate-detail.component.css'
})
export class FleetCrewmateDetailComponent implements OnInit {
  profile: FleetCrewmateProfile | null = null;
  loading = true;
  errorMessage = '';
  actionLoading = false;
  showBlockDialog = false;
  fleetId = 0;
  backButton!: ActionBarButton;
  primaryButton: ActionBarButton | null = null;
  secondaryButton: ActionBarButton | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private crewmateService = inject(CrewmateService);
  private toastService = inject(ToastService);
  private userId = 0;
  private backCrewId: number | null = null;

  ngOnInit() {
    const fromCrewId = Number(this.route.snapshot.queryParamMap.get('crewId'));
    this.backCrewId = Number.isFinite(fromCrewId) && fromCrewId >= 0 ? fromCrewId : null;
    this.backButton = this.navigation.createBackButton(
      this.backCrewId != null
        ? ['/app/fleet/crews', String(this.backCrewId)]
        : ['/app/fleet/crews']
    );

    this.userId = Number(this.route.snapshot.paramMap.get('userId'));
    if (!this.userId) {
      this.loading = false;
      this.errorMessage = 'Invalid crewmate.';
      return;
    }

    this.fleetService.getStatus().subscribe({
      next: status => {
        this.fleetId = status.fleetId ?? 0;
      }
    });

    this.loadProfile();
  }

  get isBlocked(): boolean {
    return this.profile?.friendshipState === 'blocked';
  }

  onBlockCrewmate() {
    if (this.isBlocked || !this.profile?.canSocialInteract) {
      return;
    }
    this.showBlockDialog = true;
  }

  onConfirmBlock() {
    this.showBlockDialog = false;
    this.runAction(() => this.crewmateService.blockCrewmate(this.userId), { leaveOnSuccess: true });
  }

  onCancelBlock() {
    this.showBlockDialog = false;
  }

  private loadProfile() {
    this.loading = true;
    this.errorMessage = '';

    this.fleetService.getCrewmateProfile(this.userId).subscribe({
      next: response => {
        if (!response.success || !response.profile) {
          this.errorMessage = response.message || 'Failed to load profile';
          this.profile = null;
        } else {
          this.profile = response.profile;
          this.updateActionButtons();
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load profile';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private updateActionButtons() {
    if (!this.profile || this.profile.isSelf) {
      this.primaryButton = null;
      this.secondaryButton = null;
      return;
    }

    if (!this.profile.canSocialInteract) {
      this.primaryButton = null;
      this.secondaryButton = null;
      return;
    }

    const state = this.profile.friendshipState;
    const disabled = this.actionLoading;

    if (state === 'requestReceived') {
      this.primaryButton = {
        label: 'Accept',
        type: 'primary',
        disabled,
        onClick: () => this.runAction(() => this.crewmateService.acceptFriendship(this.userId))
      };
      this.secondaryButton = {
        label: 'Reject',
        type: 'secondary',
        disabled,
        onClick: () => this.runAction(() => this.crewmateService.rejectFriendship(this.userId))
      };
      return;
    }

    this.secondaryButton = null;

    switch (state) {
      case 'requestSent':
        this.primaryButton = {
          label: 'Cancel friend request',
          type: 'primary',
          disabled,
          onClick: () => this.runAction(() => this.crewmateService.cancelFriendshipRequest(this.userId))
        };
        break;
      case 'friends':
        this.primaryButton = {
          label: 'Unfriend',
          type: 'primary',
          disabled,
          onClick: () => this.runAction(() => this.crewmateService.unfriend(this.userId))
        };
        break;
      case 'blocked':
        this.primaryButton = null;
        break;
      default:
        this.primaryButton = {
          label: 'Request friendship',
          type: 'primary',
          disabled,
          onClick: () => this.runAction(() => this.crewmateService.requestFriendship(this.userId))
        };
        break;
    }
  }

  private runAction(
    action: () => ReturnType<CrewmateService['requestFriendship']>,
    options?: { leaveOnSuccess?: boolean }
  ) {
    if (!this.profile || this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.updateActionButtons();

    action().subscribe({
      next: response => {
        if (response.success) {
          this.toastService.success(response.message);
          if (options?.leaveOnSuccess) {
            this.actionLoading = false;
            if (this.backCrewId != null) {
              this.router.navigate(['/app/fleet/crews', this.backCrewId]);
            } else {
              this.router.navigate(['/app/fleet/crews']);
            }
            return;
          }
          this.profile = {
            ...this.profile!,
            friendshipState: mapFriendshipState(response.friendshipState as unknown as number | string)
          };
        } else {
          this.toastService.error(response.message || 'Action failed');
        }
        this.actionLoading = false;
        this.updateActionButtons();
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Action failed');
        this.updateActionButtons();
      }
    });
  }
}
