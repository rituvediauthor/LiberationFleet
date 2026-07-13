import {
  AfterViewInit,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  QueryList,
  ViewChild,
  ViewChildren
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { FallibleFooterComponent } from '../../components/fallible-footer/fallible-footer.component';
import { GiftService } from '../../services/gift.service';
import { CrewService } from '../../services/crew.service';
import { CrewmateService } from '../../services/crewmate.service';
import { GiftLogCryptoService } from '../../services/crypto/gift-log-crypto.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import { GiftLogEntry, GiftVerificationAction } from '../../models/gift.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../services/encryption-content.service';
import { NavigationService } from '../../services/navigation.service';
import { NotificationContentService } from '../../services/notification-content.service';

@Component({
  selector: 'app-gift-log',
  standalone: true,
  imports: [CommonModule, FormsModule, FallibleFooterComponent],
  templateUrl: './gift-log.component.html',
  styleUrl: './gift-log.component.css'
})
export class GiftLogComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('logContainer') logContainer?: ElementRef<HTMLDivElement>;
  @ViewChildren('loadMoreSentinel') loadMoreSentinels?: QueryList<ElementRef<HTMLElement>>;

  entries: GiftLogEntry[] = [];
  activeUserId = 0;
  loading = true;
  loadingMore = false;
  hasMore = false;
  errorMessage = '';
  verifyingGiftId: number | null = null;
  crewId = 0;
  canExportCrewData = false;
  userInSeason = false;
  seasonStarted = false;
  completionPlatformSelections: Record<number, number | ''> = {};

  private readonly pageSize = 50;
  private intersectionObserver?: IntersectionObserver;
  private sentinelChangesSubscription?: { unsubscribe(): void };
  private scrollToBottomOnNextRender = false;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private notificationContent = inject(NotificationContentService);
  private giftService = inject(GiftService);
  private crewService = inject(CrewService);
  private crewmateService = inject(CrewmateService);
  private giftLogCrypto = inject(GiftLogCryptoService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    this.notificationContent.markVisited('/app/crew/gift-log');
    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadGiftLog());

    this.authService.currentUser$.subscribe(user => {
      this.activeUserId = user?.id ?? 0;
    });

    this.giftService.getSeasonStatus().subscribe({
      next: status => {
        if (!status.seasonStarted) {
          if (this.router.url.split('?')[0] === '/app/crew/gift-log') {
            void this.router.navigate(['/app/crew/season-setup'], { replaceUrl: true });
          }
          return;
        }

        this.userInSeason = !!status.userInSeason;
        this.seasonStarted = true;
        this.crewService.getMembership().subscribe({
          next: async membership => {
            this.crewId = membership.crewId ?? 0;
            this.canExportCrewData = !!membership.canExportCrewData;
            await this.encryptionContent.whenReady();
            this.loadGiftLog();
            this.encryptionReload?.markInitialLoadDone();
          },
          error: () => {
            this.errorMessage = 'Failed to load crew membership';
            this.loading = false;
          }
        });
      },
      error: () => {
        this.errorMessage = 'Failed to load season status';
        this.loading = false;
      }
    });
  }

  ngAfterViewInit() {
    this.intersectionObserver = new IntersectionObserver(
      entries => {
        if (entries.some(entry => entry.isIntersecting)) {
          this.loadOlderEntries();
        }
      },
      { root: this.logContainer?.nativeElement, threshold: 0.01 }
    );

    this.sentinelChangesSubscription = this.loadMoreSentinels?.changes.subscribe(() => {
      this.observeLoadMoreSentinel();
      if (this.scrollToBottomOnNextRender) {
        this.scrollToBottomOnNextRender = false;
        this.scrollToBottom();
      }
    });
  }

  ngOnDestroy() {
    this.intersectionObserver?.disconnect();
    this.sentinelChangesSubscription?.unsubscribe();
    this.encryptionReload?.subscription.unsubscribe();
  }

  get loadMoreTriggerIndex(): number {
    return this.entries.length > 9 ? 9 : 0;
  }

  isHighlighted(entry: GiftLogEntry): boolean {
    return this.giftService.isUserRelated(entry, this.activeUserId);
  }

  formatTimestamp(date: Date): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  goBack() {
    this.navigation.back(['/app/crew']);
  }

  goToRecordGift() {
    void this.router.navigate(['/app/crew/gift-log/record']);
  }

  goToJoinSeason() {
    void this.router.navigate(['/app/crew/join-season']);
  }

  exportGiftLog() {
    this.crewmateService.exportGiftLog().subscribe({
      next: blob => this.downloadBlob(blob, 'gift-log.json'),
      error: () => this.toastService.error('Failed to export gift log')
    });
  }

  canCompleteTransfer(entry: GiftLogEntry): boolean {
    const platformId = this.completionPlatformSelections[entry.id];
    return !!platformId;
  }

  hasAction(entry: GiftLogEntry, action: GiftVerificationAction): boolean {
    return entry.availableActions?.includes(action) ?? false;
  }

  verifyGift(entry: GiftLogEntry, action: GiftVerificationAction) {
    if (this.verifyingGiftId) return;

    let paymentPlatformId: number | undefined;
    if (action === 'completeTransfer') {
      paymentPlatformId = Number(this.completionPlatformSelections[entry.id]);
      if (!paymentPlatformId) {
        this.toastService.error('Select a payment platform before completing this gift.');
        return;
      }
    }

    this.verifyingGiftId = entry.id;
    this.giftService.verifyGift(entry.id, action, paymentPlatformId).subscribe({
      next: async result => {
        this.verifyingGiftId = null;
        if (result.success) {
          this.toastService.success(result.message || 'Gift updated');
          delete this.completionPlatformSelections[entry.id];
          if (result.entry && this.crewId > 0) {
            await this.giftLogCrypto.encryptAndStoreEntry(result.entry, this.crewId);
          }
          this.loadGiftLog();
          return;
        }
        this.toastService.error(result.message || 'Failed to update gift');
      },
      error: () => {
        this.verifyingGiftId = null;
        this.toastService.error('Failed to update gift');
      }
    });
  }

  private loadGiftLog() {
    this.loading = true;
    this.loadingMore = false;
    this.errorMessage = '';

    this.giftService.getLogs({ limit: this.pageSize }).subscribe({
      next: async page => {
        try {
          let items = page.items;
          if (this.crewId > 0) {
            items = await this.giftLogCrypto.decryptEntries(items, this.crewId);
            void this.giftLogCrypto.backfillUnencryptedEntries(items, this.crewId, this.activeUserId);
          }
          this.entries = items;
          this.hasMore = page.hasMore;
          this.applyCompletionDefaults(page.items);
          this.scrollToBottomOnNextRender = true;
          setTimeout(() => {
            this.observeLoadMoreSentinel();
            this.scrollToBottom();
          }, 0);
        } catch {
          this.errorMessage = 'Failed to decrypt gift log';
        } finally {
          this.loading = false;
        }
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load gift log';
      }
    });
  }

  private loadOlderEntries() {
    if (this.loading || this.loadingMore || !this.hasMore || this.entries.length === 0) {
      return;
    }

    const oldest = this.entries[0];
    const container = this.logContainer?.nativeElement;
    const previousScrollHeight = container?.scrollHeight ?? 0;
    const previousScrollTop = container?.scrollTop ?? 0;

    this.loadingMore = true;
    this.giftService.getLogs({
      limit: this.pageSize,
      beforeCreatedAt: oldest.timestamp.toISOString(),
      beforeId: oldest.id
    }).subscribe({
      next: async page => {
        let items = page.items;
        if (this.crewId > 0) {
          items = await this.giftLogCrypto.decryptEntries(items, this.crewId);
        }
        this.applyCompletionDefaults(items);
        this.entries = [...items, ...this.entries];
        this.hasMore = page.hasMore;
        this.loadingMore = false;

        if (container) {
          requestAnimationFrame(() => {
            container.scrollTop = previousScrollTop + (container.scrollHeight - previousScrollHeight);
            this.observeLoadMoreSentinel();
          });
        }
      },
      error: () => {
        this.loadingMore = false;
        this.toastService.error('Failed to load older gifts');
      }
    });
  }

  private applyCompletionDefaults(entries: GiftLogEntry[]) {
    entries.forEach(entry => {
      if (entry.availableActions?.includes('completeTransfer') && entry.completionPlatformOptions?.length === 1) {
        this.completionPlatformSelections[entry.id] = entry.completionPlatformOptions[0].id;
      }
    });
  }

  private observeLoadMoreSentinel() {
    if (!this.intersectionObserver || !this.hasMore) {
      return;
    }

    this.intersectionObserver.disconnect();
    const sentinel = this.loadMoreSentinels?.first?.nativeElement;
    if (sentinel) {
      this.intersectionObserver.observe(sentinel);
    }
  }

  private scrollToBottom() {
    const el = this.logContainer?.nativeElement;
    if (!el) {
      return;
    }

    requestAnimationFrame(() => {
      el.scrollTop = el.scrollHeight;
    });
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
}
