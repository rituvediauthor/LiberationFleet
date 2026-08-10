import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
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
  isOrganizer = false;

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

    forkJoin({
      status: this.fleetService.getStatus(),
      membership: this.crewService.getMembership().pipe(
        catchError(() => of({ hasCrew: false, isOrganizer: false }))
      )
    }).subscribe({
      next: ({ status, membership }) => {
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
        this.isOrganizer = !!membership.isOrganizer;
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
    return this.publicRules.every(rule => this.acceptedRuleIds.has(rule.id));
  }

  get leaveLabel(): string {
    return this.canLeaveFleetDirectly ? 'Leave fleet' : 'Leave crew';
  }

  get leaveDialogTitle(): string {
    return this.canLeaveFleetDirectly ? 'Leave fleet?' : 'Leave crew?';
  }

  get leaveDialogMessage(): string {
    if (this.canLeaveFleetDirectly) {
      return this.isNoCrewMember
        ? 'You will leave this fleet and lose access to fleet content.'
        : 'Your crew will leave this fleet. Access to other crews\' library offerings and fleet content will end.';
    }
    return 'Only an organizer can remove the whole crew from the fleet. Leaving the crew will also remove you from this fleet.';
  }

  private get canLeaveFleetDirectly(): boolean {
    return this.isNoCrewMember || this.isOrganizer;
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

    const onResult = (success: boolean, message: string | undefined) => {
      if (success) {
        this.fleetService.clearSessionCache();
        this.crewService.clearMembershipCache();
        this.toastService.success(message || (this.canLeaveFleetDirectly ? 'Left fleet' : 'Left crew'));
        this.router.navigate([this.canLeaveFleetDirectly ? '/app/fleet' : '/app/crew']);
        return;
      }
      this.toastService.error(message || 'Failed to leave');
      this.leaving = false;
      this.updateButtons();
    };

    const onError = (error: { error?: { message?: string } }) => {
      this.toastService.error(error.error?.message || 'Failed to leave');
      this.leaving = false;
      this.updateButtons();
    };

    if (this.canLeaveFleetDirectly) {
      this.fleetService.leaveFleet().subscribe({
        next: result => onResult(result.success, result.message),
        error: onError
      });
      return;
    }

    this.crewService.leaveCrew().subscribe({
      next: result => onResult(result.success, result.message),
      error: onError
    });
  }
}
