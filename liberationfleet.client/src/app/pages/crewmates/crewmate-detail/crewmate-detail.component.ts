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
  selectedRoles = new Set<string>();
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

  get hasSelectedRoles(): boolean {
    return this.selectedRoles.size > 0;
  }

  isRoleSelected(role: string): boolean {
    return this.selectedRoles.has(role);
  }

  toggleRoleSelection(role: string) {
    if (this.selectedRoles.has(role)) {
      this.selectedRoles.delete(role);
    } else {
      this.selectedRoles.add(role);
    }
    this.updateActionButtons();
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
    this.router.navigate(['/app/crew/crewmates', this.userId, 'nominate-roles']);
  }

  onDemote() {
    if (!this.profile || this.actionLoading || this.selectedRoles.size === 0) {
      return;
    }

    this.actionLoading = true;
    this.updateActionButtons();

    this.crewmateService.demoteRoles(this.userId, [...this.selectedRoles]).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit demotion proposal');
          if (response.proposalId) {
            this.router.navigate(['/app/crew/proposals', response.proposalId]);
          }
          this.updateActionButtons();
          return;
        }

        this.toastService.success(response.message || 'Demotion proposal submitted');
        this.selectedRoles.clear();
        if (response.proposalId) {
          this.router.navigate(['/app/crew/proposals', response.proposalId]);
        }
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to submit demotion proposal');
        this.updateActionButtons();
      }
    });
  }

  onToggleCanAttachFiles() {
    if (!this.profile || this.actionLoading || !this.profile.canToggleCanAttachFiles) {
      return;
    }

    const nextValue = !this.profile.canAttachFiles;
    this.actionLoading = true;
    this.crewmateService.toggleCanAttachFiles(this.userId, nextValue).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update attachment permission');
          return;
        }

        this.profile = { ...this.profile!, canAttachFiles: nextValue };
        this.toastService.success(response.message);
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to update attachment permission');
      }
    });
  }

  exportGiftLog() {
    this.crewmateService.exportGiftLog().subscribe({
      next: blob => this.downloadBlob(blob, 'gift-log.json'),
      error: () => this.toastService.error('Failed to export gift log')
    });
  }

  exportCrewmateStates() {
    this.crewmateService.exportCrewmateStates().subscribe({
      next: blob => this.downloadBlob(blob, 'crewmate-states.json'),
      error: () => this.toastService.error('Failed to export crewmate states')
    });
  }

  onConfirmBlock() {
    this.showBlockDialog = false;
    this.runAction(() => this.crewmateService.blockCrewmate(this.userId));
  }

  onCancelBlock() {
    this.showBlockDialog = false;
  }

  private downloadBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
    this.toastService.success('Export downloaded.');
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
          this.selectedRoles.clear();
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
    if (!this.profile) {
      this.primaryButton = null;
      this.secondaryButton = null;
      return;
    }

    if (this.hasSelectedRoles) {
      this.primaryButton = {
        label: 'Demote',
        type: 'primary',
        disabled: this.actionLoading,
        onClick: () => this.onDemote()
      };
      this.secondaryButton = null;
      return;
    }

    if (this.profile.isSelf) {
      this.primaryButton = {
        label: 'Nominate',
        type: 'primary',
        disabled: this.actionLoading,
        onClick: () => this.onNominate()
      };
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

    this.secondaryButton = {
      label: 'Nominate',
      type: 'secondary',
      disabled,
      onClick: () => this.onNominate()
    };

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
        this.secondaryButton = null;
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
