import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { KickReasonDialogComponent } from '../../../components/kick-reason-dialog/kick-reason-dialog.component';
import { CrewmateService } from '../../../services/crewmate.service';
import { ToastService } from '../../../components/toast/toast.component';
import { CrewmateProfile } from '../../../models/crewmate.model';

@Component({
  selector: 'app-crewmate-detail',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ConfirmDialogComponent, KickReasonDialogComponent],
  templateUrl: './crewmate-detail.component.html',
  styleUrl: './crewmate-detail.component.css'
})
export class CrewmateDetailComponent implements OnInit {
  profile: CrewmateProfile | null = null;
  loading = true;
  errorMessage = '';
  actionLoading = false;
  showBlockDialog = false;
  showKickDialog = false;
  backButton!: ActionBarButton;
  primaryButton: ActionBarButton | null = null;
  secondaryButton: ActionBarButton | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private crewmateService = inject(CrewmateService);
  private toastService = inject(ToastService);
  private userId = 0;

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/crewmates'])
    };

    this.userId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.userId) {
      this.loading = false;
      this.errorMessage = 'Invalid crewmate.';
      return;
    }

    this.loadProfile();
  }

  get isBlocked(): boolean {
    return this.profile?.friendshipState === 'blocked';
  }

  onBlockCrewmate() {
    if (this.isBlocked) {
      return;
    }
    this.showBlockDialog = true;
  }

  onKickCrewmate() {
    this.showKickDialog = true;
  }

  onConfirmKick(reason: string) {
    this.showKickDialog = false;
    if (!this.profile || this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.crewmateService.kickCrewmate(this.userId, reason).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit kick proposal');
          if (response.proposalId) {
            this.router.navigate(['/app/crew/proposals', response.proposalId]);
          }
          return;
        }

        this.toastService.success(response.message || 'Kick proposal submitted');
        if (response.proposalId) {
          this.router.navigate(['/app/crew/proposals', response.proposalId]);
        }
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to submit kick proposal');
      }
    });
  }

  onCancelKick() {
    this.showKickDialog = false;
  }

  onNominate() {
    this.toastService.error('Nominate is not available yet.');
  }

  onDemote() {
    this.toastService.error('Demote is not available yet.');
  }

  onConfirmBlock() {
    this.showBlockDialog = false;
    this.runAction(() => this.crewmateService.blockCrewmate(this.userId));
  }

  onCancelBlock() {
    this.showBlockDialog = false;
  }

  private loadProfile() {
    this.loading = true;
    this.errorMessage = '';

    this.crewmateService.getCrewmateProfile(this.userId).subscribe({
      next: response => {
        if (!response.success || !response.profile) {
          this.errorMessage = response.message || 'Failed to load crewmate profile';
          this.profile = null;
        } else {
          this.profile = response.profile;
          this.updateActionButtons();
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crewmate profile';
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
          label: 'Cancel request',
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

  private runAction(action: () => ReturnType<CrewmateService['requestFriendship']>) {
    if (!this.profile || this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.updateActionButtons();

    action().subscribe({
      next: response => {
        if (response.success) {
          this.profile = {
            ...this.profile!,
            friendshipState: response.friendshipState
          };
          this.toastService.success(response.message);
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
