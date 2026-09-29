import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../../components/confirm-dialog/confirm-dialog.component';
import { KickReasonDialogComponent } from '../../../components/kick-reason-dialog/kick-reason-dialog.component';
import { ReportContentDialogComponent } from '../../../components/report-content-dialog/report-content-dialog.component';
import { CrewmateService } from '../../../services/crewmate.service';
import { ToastService } from '../../../components/toast/toast.component';
import { CrewmateIdCardComponent } from '../../../components/crewmate-id-card/crewmate-id-card.component';
import { CollapsibleSectionComponent } from '../../../components/collapsible-section/collapsible-section.component';
import { PriorityScoreAlgorithmsComponent } from '../../../components/priority-score-algorithms/priority-score-algorithms.component';
import { FleetService } from '../../../services/fleet.service';
import { formatIdentityGroupLabels } from '../../../utils/identity-groups.util';
import { CrewmateAidStatField, AidSeasonAccounting, AidSurvivalThresholdDraft, CrewmateProfile, ProposeCrewmateAidStatChangeItem } from '../../../models/crewmate.model';
import { CrewService } from '../../../services/crew.service';

@Component({
  selector: 'app-crewmate-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PageLayoutComponent,
    ConfirmDialogComponent,
    KickReasonDialogComponent,
    ReportContentDialogComponent,
    CrewmateIdCardComponent,
    CollapsibleSectionComponent,
    PriorityScoreAlgorithmsComponent
  ],
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
  showKickFromSeasonDialog = false;
  showReportDialog = false;
  selectedRoles = new Set<string>();
  crewId = 0;
  crewName: string | null = null;
  fleetName: string | null = null;
  identityGroupLabels: string[] = [];
  backButton!: ActionBarButton;
  primaryButton: ActionBarButton | null = null;
  secondaryButton: ActionBarButton | null = null;

  aidDraft = {
    estimatedMonthlyContribution: '',
    lifetimeContributions: '',
    receptionThisYear: '',
    percentBoost: '',
    cycleReceived: '',
    hasActiveCycle: false,
    receptionOrder: '',
    autoJoinSeasonOnStart: false,
    survivalThresholds: [] as AidSurvivalThresholdDraft[],
    removedThresholdIds: [] as number[]
  };

  private route = inject(ActivatedRoute);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private crewmateService = inject(CrewmateService);
  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);
  userId = 0;

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew/crewmates']);

    this.userId = Number(this.route.snapshot.paramMap.get('id'));
    if (!this.userId) {
      this.loading = false;
      this.errorMessage = 'Invalid crewmate.';
      return;
    }

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.crewName = membership.crewName ?? null;
      }
    });

    this.fleetService.getStatus().subscribe({
      next: status => {
        this.fleetName = status.fleetName ?? null;
      }
    });

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
      this.runAction(() => this.crewmateService.unblockCrewmate(this.userId));
      return;
    }
    this.showBlockDialog = true;
  }

  onReportCrewmate() {
    this.showReportDialog = true;
  }

  onReportDismissed() {
    this.showReportDialog = false;
  }

  onReportSubmitted() {
    this.showReportDialog = false;
  }

  onKickCrewmate() {
    this.showKickDialog = true;
  }

  onKickCrewmateFromSeason() {
    this.showKickFromSeasonDialog = true;
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

  onConfirmKickFromSeason(reason: string) {
    this.showKickFromSeasonDialog = false;
    if (!this.profile || this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.crewmateService.kickCrewmateFromSeason(this.userId, reason).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit season kick proposal');
          if (response.proposalId) {
            this.router.navigate(['/app/crew/proposals', response.proposalId]);
          }
          return;
        }

        this.toastService.success(response.message || 'Season kick proposal submitted');
        if (response.proposalId) {
          this.router.navigate(['/app/crew/proposals', response.proposalId]);
        } else {
          this.loadProfile();
        }
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to submit season kick proposal');
      }
    });
  }

  onCancelKick() {
    this.showKickDialog = false;
  }

  onCancelKickFromSeason() {
    this.showKickFromSeasonDialog = false;
  }

  onNominate() {
    this.router.navigate(['/app/crew/crewmates', this.userId, 'nominate-roles']);
  }

  onClaimIdentity() {
    if (!this.profile?.canClaimIdentity || this.actionLoading) {
      return;
    }

    this.actionLoading = true;
    this.crewmateService.claimPlaceholderIdentity(this.userId).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit identity claim');
          if (response.proposalId) {
            this.router.navigate(['/app/crew/proposals', response.proposalId]);
          }
          return;
        }

        this.toastService.success(response.message || 'Identity claim submitted');
        if (response.proposalId) {
          this.router.navigate(['/app/crew/proposals', response.proposalId]);
        }
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to submit identity claim');
      }
    });
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
          this.toastService.error(response.message || 'Failed to update roles');
          if (response.proposalId) {
            this.router.navigate(['/app/crew/proposals', response.proposalId]);
          }
          this.updateActionButtons();
          return;
        }

        this.toastService.success(
          response.message
            || (response.proposalId ? 'Demotion proposal submitted' : 'Roles updated')
        );
        this.selectedRoles.clear();
        if (response.proposalId) {
          this.router.navigate(['/app/crew/proposals', response.proposalId]);
        } else if (this.profile?.isSelf) {
          this.loadProfile();
        }
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to update roles');
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

        this.loadProfile();
        this.toastService.success(response.message);
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to update attachment permission');
      }
    });
  }

  onProposeAttachPermission() {
    if (!this.profile || this.actionLoading || !this.profile.canProposeAttachFilesGrant) {
      return;
    }

    this.actionLoading = true;
    this.crewmateService.proposeAttachPermission(this.userId).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit proposal');
          return;
        }

        this.toastService.success(response.message);
        this.router.navigate(['/app/crew/proposals', response.proposalId]);
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to submit proposal');
      }
    });
  }

  onProposeProposalPermission() {
    if (!this.profile || this.actionLoading || !this.profile.canProposeCreateProposalsGrant) {
      return;
    }

    this.actionLoading = true;
    this.crewmateService.proposeProposalPermission(this.userId).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit proposal');
          return;
        }

        this.toastService.success(response.message);
        this.router.navigate(['/app/crew/proposals', response.proposalId]);
      },
      error: () => {
        this.actionLoading = false;
        this.toastService.error('Failed to submit proposal');
      }
    });
  }

  onProposeAidStatChanges() {
    if (!this.profile || this.actionLoading || !this.profile.canProposeAidStatEdits) {
      return;
    }

    let changes: ProposeCrewmateAidStatChangeItem[];
    try {
      changes = this.buildAidStatChanges();
    } catch (error) {
      this.toastService.error(error instanceof Error ? error.message : 'Invalid aid statistic values.');
      return;
    }

    if (changes.length === 0) {
      this.toastService.error('Change at least one aid statistic before proposing.');
      return;
    }

    this.actionLoading = true;
    this.crewmateService.proposeAidStatChange(this.userId, changes).subscribe({
      next: response => {
        this.actionLoading = false;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit aid statistic proposal');
          if (response.proposalId) {
            this.router.navigate(['/app/crew/proposals', response.proposalId]);
          }
          return;
        }

        this.toastService.success(response.message || 'Aid statistic change proposal submitted');
        if (response.proposalId) {
          this.router.navigate(['/app/crew/proposals', response.proposalId]);
        }
      },
      error: err => {
        this.actionLoading = false;
        // API returns BadRequest({ success, message }) on business failures — surface that message.
        const apiMessage =
          err?.error?.message
          || err?.error?.Message
          || (typeof err?.error === 'string' ? err.error : null);
        this.toastService.error(apiMessage || 'Failed to submit aid statistic proposal');
        if (err?.error?.proposalId) {
          this.router.navigate(['/app/crew/proposals', err.error.proposalId]);
        }
      }
    });
  }

    private buildAidStatChanges(): ProposeCrewmateAidStatChangeItem[] {
    if (!this.profile) {
      return [];
    }

    const changes: ProposeCrewmateAidStatChangeItem[] = [];
    const pushIfChanged = (
      field: CrewmateAidStatField,
      draft: string | number | null | undefined,
      current: number | null | undefined,
      options?: { integer?: boolean }
    ) => {
      // number inputs can bind as numbers via ngModel — coerce before trim.
      const trimmed = String(draft ?? '').trim();
      if (!trimmed) {
        return;
      }

      const next = Number(trimmed);
      if (!Number.isFinite(next) || next < 0) {
        throw new Error(`Enter a valid non-negative value for ${field}.`);
      }

      if (options?.integer && !Number.isInteger(next)) {
        throw new Error('Percent boost must be a whole number.');
      }

      const currentValue = current ?? null;
      if (currentValue !== null && Math.abs(currentValue - next) < 0.0001) {
        return;
      }

      changes.push({
        field,
        newValue: options?.integer ? String(Math.round(next)) : next.toFixed(2)
      });
    };

    pushIfChanged(
      'EstimatedMonthlyContribution',
      this.aidDraft.estimatedMonthlyContribution,
      this.profile.estimatedMonthlyContribution
    );
    pushIfChanged(
      'LifetimeContributions',
      this.aidDraft.lifetimeContributions,
      this.profile.lifetimeContributions
    );
    pushIfChanged(
      'ReceptionThisYear',
      this.aidDraft.receptionThisYear,
      this.profile.receptionThisYear
    );
    pushIfChanged(
      'PercentBoost',
      this.aidDraft.percentBoost,
      this.profile.percentBoost,
      { integer: true }
    );

    const seasonAccounting = this.buildSeasonAccountingPayload();
    if (this.seasonAccountingChanged(seasonAccounting)) {
      changes.push({
        field: 'SeasonAccounting',
        newValue: JSON.stringify(seasonAccounting)
      });
    }

    return changes;
  }

  private buildSeasonAccountingPayload(): AidSeasonAccounting {
    const cycleReceived = Number(String(this.aidDraft.cycleReceived ?? '').trim() || '0');
    if (!Number.isFinite(cycleReceived) || cycleReceived < 0) {
      throw new Error('Enter a valid non-negative cycle reception amount.');
    }

    let receptionOrder: number | null = null;
    const orderRaw = String(this.aidDraft.receptionOrder ?? '').trim();
    if (orderRaw) {
      receptionOrder = Number(orderRaw);
      if (!Number.isInteger(receptionOrder) || receptionOrder < 1) {
        throw new Error('Reception order must be a whole number of 1 or greater.');
      }
    }

    const survivalThresholds = this.aidDraft.survivalThresholds.map((row, index) => {
      const amountRemaining = Number(row.amountRemaining);
      const thresholdAmount = Number(row.thresholdAmount);
      const order = Number(row.order);
      if (!Number.isFinite(amountRemaining) || amountRemaining < 0) {
        throw new Error('Survival threshold remaining amounts must be non-negative.');
      }
      if (!Number.isFinite(thresholdAmount) || thresholdAmount < 0) {
        throw new Error('Survival threshold amounts must be non-negative.');
      }
      if (!Number.isInteger(order) || order < 1) {
        throw new Error(`Survival threshold #${index + 1}: order must be a whole number of 1 or greater.`);
      }
      return {
        id: row.id ?? null,
        thresholdAmount,
        amountRemaining,
        order
      };
    });

    return {
      cycleReceived,
      hasActiveCycle: !!this.aidDraft.hasActiveCycle,
      receptionOrder,
      // Only meaningful before a season starts; keep existing value so mid-season edits don't churn it.
      autoJoinSeasonOnStart: this.profile.seasonStarted
        ? !!this.profile.autoJoinSeasonOnStart
        : !!this.aidDraft.autoJoinSeasonOnStart,
      survivalThresholds,
      removedThresholdIds: [...this.aidDraft.removedThresholdIds]
    };
  }

  private seasonAccountingChanged(next: AidSeasonAccounting): boolean {
    const current = this.profile?.seasonAccounting;
    if (!current) {
      return true;
    }

    if (Math.abs((current.cycleReceived ?? 0) - next.cycleReceived) >= 0.0001) {
      return true;
    }
    if (!!current.hasActiveCycle !== next.hasActiveCycle) {
      return true;
    }
    if ((current.receptionOrder ?? null) !== (next.receptionOrder ?? null)) {
      return true;
    }
    if (!!current.autoJoinSeasonOnStart !== next.autoJoinSeasonOnStart) {
      return true;
    }
    if ((next.removedThresholdIds?.length ?? 0) > 0) {
      return true;
    }

    const currentRows = current.survivalThresholds ?? [];
    if (currentRows.length !== next.survivalThresholds.length) {
      return true;
    }

    for (let i = 0; i < next.survivalThresholds.length; i++) {
      const a = currentRows[i];
      const b = next.survivalThresholds[i];
      if ((a?.id ?? null) !== (b.id ?? null)) {
        return true;
      }
      if (Math.abs((a?.amountRemaining ?? 0) - b.amountRemaining) >= 0.0001) {
        return true;
      }
      if (Math.abs((a?.thresholdAmount ?? 0) - b.thresholdAmount) >= 0.0001) {
        return true;
      }
      if ((a?.order ?? i + 1) !== b.order) {
        return true;
      }
    }

    return false;
  }

  get derivedSurvivalReceived(): number {
    return this.aidDraft.survivalThresholds.reduce((sum, row) => {
      const remaining = Number(row.amountRemaining) || 0;
      const threshold = Number(row.thresholdAmount) || 0;
      return sum + Math.max(0, threshold - remaining);
    }, 0);
  }

  get derivedTotalReception(): number {
    const cycle = Number(String(this.aidDraft.cycleReceived ?? '').trim() || '0') || 0;
    return cycle + this.derivedSurvivalReceived;
  }

  addSurvivalThresholdRow() {
    const maxOrder = this.aidDraft.survivalThresholds.reduce(
      (max, row) => Math.max(max, Number(row.order) || 0),
      0
    );
    this.aidDraft.survivalThresholds = [
      ...this.aidDraft.survivalThresholds,
      {
        id: null,
        thresholdAmount: 0,
        amountRemaining: 0,
        order: maxOrder + 1
      }
    ];
  }

  removeSurvivalThresholdRow(index: number) {
    const row = this.aidDraft.survivalThresholds[index];
    if (!row) {
      return;
    }
    if (row.id != null) {
      this.aidDraft.removedThresholdIds = [...this.aidDraft.removedThresholdIds, row.id];
    }
    this.aidDraft.survivalThresholds = this.aidDraft.survivalThresholds.filter((_, i) => i !== index);
  }

  private syncAidDraftFromProfile() {
    if (!this.profile) {
      return;
    }

    const money = (value: number | null | undefined) =>
      value == null ? '' : String(value);

    const accounting = this.profile.seasonAccounting;
    this.aidDraft = {
      estimatedMonthlyContribution: money(this.profile.estimatedMonthlyContribution),
      lifetimeContributions: money(this.profile.lifetimeContributions),
      receptionThisYear: money(this.profile.receptionThisYear),
      percentBoost: this.profile.percentBoost == null ? '' : String(this.profile.percentBoost),
      cycleReceived: money(accounting?.cycleReceived ?? this.profile.cycleReceived),
      hasActiveCycle: !!(accounting?.hasActiveCycle ?? this.profile.hasActiveCycle),
      receptionOrder: accounting?.receptionOrder != null
        ? String(accounting.receptionOrder)
        : (this.profile.receptionOrder != null ? String(this.profile.receptionOrder) : ''),
      autoJoinSeasonOnStart: !!(accounting?.autoJoinSeasonOnStart ?? this.profile.autoJoinSeasonOnStart),
      survivalThresholds: (accounting?.survivalThresholds ?? []).map((row, index) => ({
        id: row.id ?? null,
        thresholdAmount: row.thresholdAmount ?? 0,
        amountRemaining: row.amountRemaining ?? 0,
        order: row.order ?? index + 1
      })),
      removedThresholdIds: []
    };
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
          this.identityGroupLabels = formatIdentityGroupLabels(response.profile.identityGroups);
          this.selectedRoles.clear();
          this.syncAidDraftFromProfile();
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

    if (this.profile.isSelf || this.profile.isPlaceholderMember) {
      this.primaryButton = null;
      this.secondaryButton = null;
      return;
    }

    // Reverse-blocked: hide friend actions. Viewer-initiated block still uses the Block control.
    if (this.profile.canSocialInteract === false && this.profile.friendshipState !== 'blocked') {
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
        this.primaryButton = {
          label: 'Unblock',
          type: 'primary',
          disabled,
          onClick: () => this.runAction(() => this.crewmateService.unblockCrewmate(this.userId))
        };
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
