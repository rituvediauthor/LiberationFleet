import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { KickReasonDialogComponent } from '../../../components/kick-reason-dialog/kick-reason-dialog.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { UserAvatarComponent } from '../../../components/user-avatar/user-avatar.component';
import { FleetService } from '../../../services/fleet.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetCrewDetail, FleetCrewmateSummary } from '../../../models/fleet.model';
import { PublicCrewRule } from '../../../models/crew.model';

type JoinStep = 'detail' | 'rules';

@Component({
  selector: 'app-fleet-crew-detail',
  standalone: true,
  imports: [
    CommonModule,
    PageLayoutComponent,
    KickReasonDialogComponent,
    ConfirmDialogComponent,
    UserAvatarComponent
  ],
  templateUrl: './fleet-crew-detail.component.html',
  styleUrl: './fleet-crew-detail.component.css'
})
export class FleetCrewDetailComponent implements OnInit {
  crew: FleetCrewDetail | null = null;
  loading = true;
  errorMessage = '';
  actionLoading = false;
  showKickDialog = false;
  showJoinConfirm = false;
  viewerHasCrew = false;
  fleetId = 0;
  backButton!: ActionBarButton;
  primaryButton: ActionBarButton | null = null;
  secondaryButton: ActionBarButton | null = null;

  joinStep: JoinStep = 'detail';
  publicRules: PublicCrewRule[] = [];
  acceptedRuleIds = new Set<number>();
  isLoadingRules = false;
  rulesError = '';

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private crewId = 0;

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.onBack()
    };
    const rawId = this.route.snapshot.paramMap.get('id');
    this.crewId = Number(rawId);
    if (rawId === null || Number.isNaN(this.crewId) || this.crewId < 0) {
      this.loading = false;
      this.errorMessage = 'Invalid crew.';
      return;
    }
    this.fleetService.getStatus().subscribe({
      next: status => {
        this.fleetId = status.fleetId ?? 0;
      }
    });
    this.crewService.getMembership().subscribe({
      next: membership => {
        this.viewerHasCrew = !!membership?.hasCrew;
        this.updateActionButtons();
      }
    });
    this.loadCrew();
  }

  get allRulesAccepted(): boolean {
    return this.publicRules.every(rule => this.acceptedRuleIds.has(rule.id));
  }

  get joinConfirmMessage(): string {
    const name = this.crew?.crewName || 'this crew';
    if (this.viewerHasCrew) {
      return `Request to join ${name}? If they approve, you will leave your current crew and join theirs.`;
    }
    return `Request to join ${name}?`;
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
    this.updateActionButtons();
  }

  openCrewmate(crewmate: FleetCrewmateSummary) {
    this.router.navigate(['/app/fleet/crewmates', crewmate.userId], {
      queryParams: { crewId: this.crewId }
    });
  }

  onConfirmKick(reason: string) {
    this.showKickDialog = false;
    if (this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.updateActionButtons();
    this.fleetService.kickCrew(this.crewId, reason).subscribe({
      next: response => {
        this.actionLoading = false;
        this.updateActionButtons();
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to kick crew');
          return;
        }
        this.toastService.success(response.message || 'Kick proposal submitted');
        this.router.navigate(['/app/fleet/crews']);
      },
      error: () => {
        this.actionLoading = false;
        this.updateActionButtons();
        this.toastService.error('Failed to kick crew');
      }
    });
  }

  onCancelKick() {
    this.showKickDialog = false;
  }

  onConfirmJoin() {
    this.showJoinConfirm = false;
    this.beginJoin();
  }

  onCancelJoinConfirm() {
    this.showJoinConfirm = false;
  }

  private onBack() {
    if (this.joinStep === 'rules') {
      this.joinStep = 'detail';
      this.publicRules = [];
      this.acceptedRuleIds.clear();
      this.rulesError = '';
      this.updateActionButtons();
      return;
    }
    this.navigation.back(['/app/fleet/crews']);
  }

  private loadCrew() {
    this.loading = true;
    this.errorMessage = '';
    this.fleetService.getCrewDetail(this.crewId).subscribe({
      next: response => {
        if (!response.success || !response.crew) {
          this.errorMessage = response.message || 'Failed to load crew';
          this.crew = null;
        } else {
          this.crew = response.crew;
          this.updateActionButtons();
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private updateActionButtons() {
    this.primaryButton = null;
    this.secondaryButton = null;

    if (!this.crew) {
      return;
    }

    if (this.crew.canKick && this.joinStep === 'detail') {
      this.secondaryButton = {
        label: 'Kick crew',
        type: 'secondary',
        disabled: this.actionLoading,
        onClick: () => {
          this.showKickDialog = true;
        }
      };
    }

    if (!this.crew.canJoin) {
      return;
    }

    if (this.joinStep === 'rules') {
      this.primaryButton = {
        label: 'Request to join',
        type: 'primary',
        disabled: this.actionLoading || this.isLoadingRules || !this.allRulesAccepted,
        onClick: () => this.submitJoinRequest()
      };
      return;
    }

    this.primaryButton = {
      label: 'Join crew',
      type: 'primary',
      disabled: this.actionLoading || this.isLoadingRules,
      onClick: () => {
        this.showJoinConfirm = true;
      }
    };
  }

  private beginJoin() {
    if (this.actionLoading || this.isLoadingRules) {
      return;
    }

    this.isLoadingRules = true;
    this.rulesError = '';
    this.updateActionButtons();

    this.crewService.getPublicRules(this.crewId).subscribe({
      next: result => {
        this.isLoadingRules = false;
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to load crew rules');
          this.updateActionButtons();
          return;
        }

        this.publicRules = result.items ?? [];
        this.acceptedRuleIds.clear();
        this.joinStep = 'rules';
        this.updateActionButtons();
      },
      error: () => {
        this.isLoadingRules = false;
        this.toastService.error('Failed to load crew rules');
        this.updateActionButtons();
      }
    });
  }

  private submitJoinRequest() {
    if (this.actionLoading || !this.allRulesAccepted) {
      return;
    }

    this.actionLoading = true;
    this.updateActionButtons();
    const acceptedRuleIds = this.publicRules.map(rule => rule.id);
    this.fleetService.joinCrew(this.crewId, acceptedRuleIds).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to join crew');
          this.updateActionButtons();
          return;
        }
        this.toastService.success(response.message || 'Join request submitted');
        this.joinStep = 'detail';
        this.publicRules = [];
        this.acceptedRuleIds.clear();
        this.updateActionButtons();
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to join crew');
        this.updateActionButtons();
      }
    });
  }
}
