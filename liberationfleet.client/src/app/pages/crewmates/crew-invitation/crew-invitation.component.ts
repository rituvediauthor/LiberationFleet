import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { CrewInvitation, PublicCrewRule } from '../../../models/crew.model';

type InviteStep = 'invite' | 'rules';

@Component({
  selector: 'app-crew-invitation',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './crew-invitation.component.html',
  styleUrl: './crew-invitation.component.css'
})
export class CrewInvitationComponent implements OnInit {
  invitation: CrewInvitation | null = null;
  step: InviteStep = 'invite';
  loading = true;
  loadingRules = false;
  submitting = false;
  declining = false;
  errorMessage = '';
  publicRules: PublicCrewRule[] = [];
  acceptedRuleIds = new Set<number>();

  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;
  secondaryButton!: ActionBarButton;

  private invitationId = 0;
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.onBack()
    };
    this.updateButtons();

    this.invitationId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.invitationId) {
      this.loading = false;
      this.errorMessage = 'Invitation not found.';
      return;
    }

    this.crewService.getInvitation(this.invitationId).subscribe({
      next: result => {
        this.loading = false;
        if (!result.success || !result.invitation) {
          this.errorMessage = result.message || 'Invitation not found.';
          this.updateButtons();
          return;
        }
        this.invitation = result.invitation;
        this.updateButtons();
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error.error?.message || 'Failed to load invitation.';
        this.updateButtons();
      }
    });
  }

  get allRulesAccepted(): boolean {
    return this.publicRules.every(rule => this.acceptedRuleIds.has(rule.id));
  }

  isRuleAccepted(ruleId: number): boolean {
    return this.acceptedRuleIds.has(ruleId);
  }

  toggleRuleAcceptance(ruleId: number, accepted: boolean) {
    if (accepted) {
      this.acceptedRuleIds.add(ruleId);
    } else {
      this.acceptedRuleIds.delete(ruleId);
    }
    this.updateButtons();
  }

  private onBack() {
    if (this.step === 'rules') {
      this.step = 'invite';
      this.publicRules = [];
      this.acceptedRuleIds.clear();
      this.updateButtons();
      return;
    }
    this.navigation.back(['/app/user']);
  }

  private updateButtons() {
    if (this.step === 'rules') {
      this.secondaryButton = {
        label: 'Back',
        type: 'secondary',
        disabled: this.submitting,
        onClick: () => this.onBack()
      };
      this.primaryButton = {
        label: 'Request to join',
        type: 'primary',
        disabled: this.submitting || this.loadingRules || !this.allRulesAccepted,
        onClick: () => this.submitJoinRequest()
      };
      return;
    }

    const pending = this.invitation?.status === 'Pending';
    this.secondaryButton = {
      label: 'Decline',
      type: 'secondary',
      disabled: !pending || this.declining || this.loading,
      onClick: () => this.decline()
    };
    this.primaryButton = {
      label: 'Accept',
      type: 'primary',
      disabled: !pending || this.loading || this.loadingRules,
      onClick: () => this.continueToRules()
    };
  }

  private continueToRules() {
    if (!this.invitation || this.loadingRules) {
      return;
    }

    this.loadingRules = true;
    this.updateButtons();

    this.crewService.getPublicRules(this.invitation.crewId).subscribe({
      next: result => {
        this.loadingRules = false;
        if (!result.success) {
          this.toastService.error(result.message);
          this.updateButtons();
          return;
        }
        this.publicRules = result.items ?? [];
        this.acceptedRuleIds.clear();
        this.step = 'rules';
        this.updateButtons();
      },
      error: error => {
        this.loadingRules = false;
        this.toastService.error(error.error?.message || 'Failed to load rules');
        this.updateButtons();
      }
    });
  }

  private decline() {
    if (!this.invitation || this.declining) {
      return;
    }

    this.declining = true;
    this.updateButtons();

    this.crewService.declineInvitation(this.invitation.id).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Invitation declined');
          this.router.navigate(['/app/user']);
          return;
        }
        this.toastService.error(result.message);
        this.declining = false;
        this.updateButtons();
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to decline invitation');
        this.declining = false;
        this.updateButtons();
      }
    });
  }

  private submitJoinRequest() {
    if (!this.invitation || this.submitting || !this.allRulesAccepted) {
      return;
    }

    this.submitting = true;
    this.updateButtons();

    this.crewService.submitJoinRequest({
      invitationId: this.invitation.id,
      crewId: this.invitation.crewId,
      acceptedRuleIds: this.publicRules.map(rule => rule.id)
    }).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Join request submitted');
          this.router.navigate(['/app/crew/join-requests']);
          return;
        }
        this.toastService.error(result.message);
        this.submitting = false;
        this.updateButtons();
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to submit join request');
        this.submitting = false;
        this.updateButtons();
      }
    });
  }
}
