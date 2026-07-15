import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { NotificationService } from '../../../services/notification.service';
import { ProposalService } from '../../../services/proposal.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ProposalListItem, ProposalStatus } from '../../../models/proposal.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../services/encryption-content.service';

@Component({
  selector: 'app-proposals-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, ContentBadgeComponent],
  templateUrl: './proposals-list.component.html',
  styleUrl: './proposals-list.component.css'
})
export class ProposalsListComponent implements OnInit, OnDestroy {
  status: ProposalStatus = 'Pending';
  items: ProposalListItem[] = [];
  loading = true;
  errorMessage = '';
  crewId = 0;
  fleetId = 0;
  isFleetScope = false;
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
  private encryptionContent = inject(EncryptionContentService);
  private countdownIntervalId?: ReturnType<typeof setInterval>;
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    this.isFleetScope = this.route.snapshot.data['scope'] === 'fleet';
    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadProposals());

    this.countdownIntervalId = setInterval(() => {
      this.countdownTick++;
    }, 1000);

    const statusParam = (this.route.snapshot.paramMap.get('status') ?? 'pending').toLowerCase();
    this.status = this.parseStatus(statusParam);

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
        if (this.isFleetScope && this.fleetId > 0) {
          this.items = await this.proposalCrypto.decryptListItems(items, { fleetId: this.fleetId });
        } else if (!this.isFleetScope && this.crewId > 0) {
          this.items = await this.proposalCrypto.decryptListItems(items, this.crewId);
        } else {
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

