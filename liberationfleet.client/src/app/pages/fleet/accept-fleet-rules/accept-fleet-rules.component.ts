import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { PublicFleetRule } from '../../../models/fleet.model';

@Component({
  selector: 'app-accept-fleet-rules',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
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
  errorMessage = '';

  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew']);
    this.updatePrimaryButton();

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
        this.loadRules();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load fleet status.';
        this.updatePrimaryButton();
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
    this.updatePrimaryButton();
  }

  private loadRules() {
    this.fleetService.getPublicRules(this.fleetId).subscribe({
      next: result => {
        this.loading = false;
        if (!result.success) {
          this.errorMessage = result.message;
          this.updatePrimaryButton();
          return;
        }
        this.fleetName = result.fleetName || this.fleetName;
        this.publicRules = result.items ?? [];
        this.acceptedRuleIds.clear();
        this.updatePrimaryButton();
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error.error?.message || 'Failed to load fleet rules.';
        this.updatePrimaryButton();
      }
    });
  }

  private updatePrimaryButton() {
    this.primaryButton = {
      label: 'Continue to fleet',
      type: 'primary',
      disabled: this.loading || this.submitting || !this.allRulesAccepted,
      onClick: () => this.submit()
    };
  }

  private submit() {
    if (this.submitting || !this.allRulesAccepted) {
      return;
    }

    this.submitting = true;
    this.updatePrimaryButton();

    this.fleetService.acceptRules(this.publicRules.map(rule => rule.id)).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success(result.message || 'Rules accepted');
          this.router.navigate(['/app/fleet']);
          return;
        }
        this.toastService.error(result.message);
        this.submitting = false;
        this.updatePrimaryButton();
      },
      error: error => {
        this.toastService.error(error.error?.message || 'Failed to accept rules');
        this.submitting = false;
        this.updatePrimaryButton();
      }
    });
  }
}
