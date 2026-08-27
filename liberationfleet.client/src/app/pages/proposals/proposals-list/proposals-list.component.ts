import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { NotificationService } from '../../../services/notification.service';
import { NotificationContentService } from '../../../services/notification-content.service';
import { NotificationTargetDirective } from '../../../directives/notification-target.directive';
import { ProposalService } from '../../../services/proposal.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ProposalListItem, ProposalStatus } from '../../../models/proposal.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../services/encryption-content.service';
import {
  clearNotificationHighlightParams,
  readNotificationHighlightId
} from '../../../utils/notification-deep-link.util';

const CREW_KIND_OPTIONS: { value: string; label: string }[] = [
  { value: 'General', label: 'General' },
  { value: 'CrewSettingChange', label: 'Crew setting' },
  { value: 'CrewRuleChange', label: 'Crew rule' },
  { value: 'CrewChatChange', label: 'Chat change' },
  { value: 'CrewmateKick', label: 'Kick crewmate' },
  { value: 'CrewmateSeasonKick', label: 'Season kick' },
  { value: 'CrewmateRejoin', label: 'Rejoin' },
  { value: 'CrewJoinRequest', label: 'Join request' },
  { value: 'CrewRoleChange', label: 'Role change' },
  { value: 'ClaimPlaceholderIdentity', label: 'Claim identity' },
  { value: 'CrewmatePermissionGrant', label: 'Permission grant' },
  { value: 'CrewmateAidStatChange', label: 'Aid stat' },
  { value: 'CrewApplyToFleet', label: 'Apply to fleet' }
];

const FLEET_KIND_OPTIONS: { value: string; label: string }[] = [
  { value: 'General', label: 'General' },
  { value: 'FleetSettingChange', label: 'Fleet setting' },
  { value: 'FleetRuleChange', label: 'Fleet rule' },
  { value: 'FleetChatChange', label: 'Chat change' },
  { value: 'FleetJoinRequest', label: 'Join request' },
  { value: 'FleetKickCrew', label: 'Kick crew' }
];

@Component({
  selector: 'app-proposals-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ContentBadgeComponent, NotificationTargetDirective],
  templateUrl: './proposals-list.component.html',
  styleUrl: './proposals-list.component.css'
})
export class ProposalsListComponent implements OnInit, OnDestroy {
  status: ProposalStatus = 'Pending';
  items: ProposalListItem[] = [];
  selectedKind = '';
  loading = true;
  errorMessage = '';
  crewId = 0;
  fleetId = 0;
  isFleetScope = false;
  highlightId: number | null = null;
  notifyPrefix = '';
  backButton!: ActionBarButton;
  resourceCounts: Record<string, number> = {};
  countdownTick = 0;

  private route = inject(ActivatedRoute);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private proposalService = inject(ProposalService);
  private proposalCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);
  private notificationService = inject(NotificationService);
  private notificationContent = inject(NotificationContentService);
  private encryptionContent = inject(EncryptionContentService);
  private countdownIntervalId?: ReturnType<typeof setInterval>;
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    this.isFleetScope = this.route.snapshot.data['scope'] === 'fleet';
    this.highlightId = readNotificationHighlightId(this.route);
    clearNotificationHighlightParams(this.router, this.route);
    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadProposals());

    this.countdownIntervalId = setInterval(() => {
      this.countdownTick++;
    }, 1000);

    const statusParam = (this.route.snapshot.paramMap.get('status') ?? 'pending').toLowerCase();
    this.status = this.parseStatus(statusParam);
    const statusSegment = statusParam === 'approved' || statusParam === 'rejected' ? statusParam : 'pending';
    this.notifyPrefix = this.isFleetScope
      ? `/app/fleet/proposals/list/${statusSegment}`
      : `/app/crew/proposals/list/${statusSegment}`;
    this.notificationContent.markVisited(this.notifyPrefix);

    this.backButton = this.navigation.createBackButton(
      this.isFleetScope ? ['/app/fleet/proposals'] : ['/app/crew/proposals']
    );
    this.notificationService.refreshBadges();
    this.notificationService.resourceCounts$.subscribe(counts => {
      this.resourceCounts = counts;
    });

    if (this.isFleetScope) {
      this.fleetService.getStatus().subscribe({
        next: async status => {
          this.fleetId = status.fleetId ?? 0;
          await this.encryptionContent.whenReady();
          this.loadProposals();
          this.encryptionReload?.markInitialLoadDone();
        },
        error: () => {
          this.errorMessage = 'Failed to load fleet status';
          this.loading = false;
        }
      });
      return;
    }

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        this.loadProposals();
        this.encryptionReload?.markInitialLoadDone();
      },
      error: () => {
        this.errorMessage = 'Failed to load crew membership';
        this.loading = false;
      }
    });
  }

  ngOnDestroy() {
    if (this.countdownIntervalId) {
      clearInterval(this.countdownIntervalId);
    }
    this.encryptionReload?.subscription.unsubscribe();
  }

  get statusLabel(): string {
    return this.status;
  }

  get statusPageLabel(): string {
    if (this.status === 'Approved') {
      return 'Proposals Approved';
    }
    if (this.status === 'Rejected') {
      return 'Proposals Rejected';
    }
    return 'Proposals Pending';
  }

  get kindOptions(): { value: string; label: string }[] {
    return this.isFleetScope ? FLEET_KIND_OPTIONS : CREW_KIND_OPTIONS;
  }

  get filteredItems(): ProposalListItem[] {
    if (!this.selectedKind) {
      return this.items;
    }
    return this.items.filter(item => item.kind === this.selectedKind);
  }

  onKindFilterChange(value: string) {
    this.selectedKind = value;
  }

  kindLabel(kind?: string): string | null {
    if (!kind) {
      return null;
    }
    const match = [...CREW_KIND_OPTIONS, ...FLEET_KIND_OPTIONS].find(option => option.value === kind);
    return match?.label ?? kind;
  }

  proposalBadgeCount(proposalId: number): number {
    return this.resourceCounts[`proposal:${proposalId}`] ?? 0;
  }

  formatActivity(date: Date): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  countdownText(item: ProposalListItem): string | null {
    void this.countdownTick;
    if (item.status !== 'Pending') {
      return null;
    }
    return this.proposalService.formatCountdown(item.approvalTimerEndsAt ?? null);
  }

  openProposal(item: ProposalListItem) {
    const base = this.isFleetScope ? '/app/fleet/proposals' : '/app/crew/proposals';
    this.router.navigate([base, item.id]);
  }

  private parseStatus(value: string): ProposalStatus {
    if (value === 'approved') return 'Approved';
    if (value === 'rejected') return 'Rejected';
    return 'Pending';
  }

  private loadProposals() {
    this.loading = true;
    this.errorMessage = '';

    this.proposalService.getProposals(this.status, this.isFleetScope ? 'fleet' : 'crew').subscribe({
      next: async items => {
        try {
          if (this.isFleetScope && this.fleetId > 0) {
            this.items = await this.proposalCrypto.decryptListItems(items, { fleetId: this.fleetId });
          } else if (!this.isFleetScope && this.crewId > 0) {
            this.items = await this.proposalCrypto.decryptListItems(items, this.crewId);
          } else {
            this.items = items;
          }
        } catch {
          this.items = items;
        }
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load proposals';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
