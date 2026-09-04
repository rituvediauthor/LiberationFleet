import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { NavigationService } from '../../../services/navigation.service';
import { FleetService } from '../../../services/fleet.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { PublicFleetRule } from '../../../models/fleet.model';

@Component({
  selector: 'app-accept-fleet-rules',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ConfirmDialogComponent],
  templateUrl: './accept-fleet-rules.component.html',
  styleUrl: './accept-fleet-rules.component.css'
})
export class AcceptFleetRulesComponent implements OnInit {
  fleetId = 0;
  fleetName = '';
  publicRules: PublicFleetRule[] = [];
  acceptedRuleIds = new Set<number>();
  loading = true;
  submitting = false;
  leaving = false;
  showLeaveDialog = false;
  errorMessage = '';
  isNoCrewMember = false;

  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;
  secondaryButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew']);
    this.updateButtons();

    this.fleetService.getStatus().subscribe({
      next: status => {
        if (!status.hasFleet || !status.fleetId) {
          this.loading = false;
          this.router.navigate(['/app/fleet']);
          return;
        }

        if (!status.needsRuleAcceptance) {
          this.router.navigate(['/app/fleet']);
          return;
        }

        this.fleetId = status.fleetId;
        this.fleetName = status.fleetName || 'your fleet';
        this.isNoCrewMember = !!status.isNoCrewMember;
        this.loadRules();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load fleet status.';
        this.updateButtons();
      }
    });
  }

  get allRulesAccepted(): boolean {
    return this.publicRules.length > 0
      && this.publicRules.every(rule => this.acceptedRuleIds.has(rule.id));
  }

  get leaveLabel(): string {
    return 'Leave fleet';
  }

  get leaveDialogTitle(): string {
    return this.createsLeaveProposal ? 'Propose leaving the fleet?' : 'Leave fleet?';
  }

  get leaveDialogMessage(): string {
    if (this.isNoCrewMember) {
      return 'You will leave this fleet and lose access to fleet content.';
    }
    return 'Confirming will create a crew proposal to leave this fleet. If it passes, your crew will leave and lose access to other crews\' library offerings and fleet content.';
  }

  private get createsLeaveProposal(): boolean {
    return !this.isNoCrewMember;
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

  onConfirmLeave() {
    this.showLeaveDialog = false;
    this.performLeave();
  }

  onCancelLeave() {
    this.showLeaveDialog = false;
  }

  private loadRules() {
    this.fleetService.getPublicRules(this.fleetId).subscribe({
      next: result => {
        this.loading = false;
        if (!result.success) {
          this.errorMessage = result.message;
          this.updateButtons();
          return;
        }
        this.fleetName = result.fleetName || this.fleetName;
        this.publicRules = result.items ?? [];
        this.acceptedRuleIds.clear();
        this.updateButtons();
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error.error?.message || 'Failed to load fleet rules.';
        this.updateButtons();
      }
    });
  }

  private updateButtons() {
    this.secondaryButton = {
      label: this.leaveLabel,
      type: 'secondary',
      disabled: this.loading || this.submitting || this.leaving,
      onClick: () => {
        this.showLeaveDialog = true;
      }
    };
    this.primaryButton = {
      label: 'Continue to fleet',
      type: 'primary',
      disabled: this.loading || this.submitting || this.leaving || !this.allRulesAccepted,
      onClick: () => this.submit()
    };
  }

  private submit() {
    if (this.submitting || !this.allRulesAccepted) {
      return;
    }

    this.submitting = true;
    this.updateButtons();

    this.fleetService.acceptRules(this.publicRules.map(rule => rule.id)).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Rules accepted');
          this.router.navigate(['/app/fleet']);
          return;
        }
        this.toastService.error(result.message);
        this.submitting = false;
        this.updateButtons();
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to accept rules');
        this.submitting = false;
        this.updateButtons();
      }
    });
  }

  private performLeave() {
    if (this.leaving) {
      return;
    }

    this.leaving = true;
    this.updateButtons();

    this.fleetService.leaveFleet().subscribe({
      next: result => {
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to leave fleet');
          this.leaving = false;
          this.updateButtons();
          return;
        }

        this.fleetService.clearSessionCache();
        this.crewService.clearMembershipCache();

        if (result.proposalsSubmitted) {
          this.toastService.success(result.message || 'Leave-fleet proposal submitted');
          this.router.navigate(['/app/crew/proposals/list/pending']);
          return;
        }

        this.toastService.success(result.message || 'Left fleet');
        this.router.navigate(['/app/fleet']);
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to leave fleet');
        this.leaving = false;
        this.updateButtons();
      }
    });
  }
}
