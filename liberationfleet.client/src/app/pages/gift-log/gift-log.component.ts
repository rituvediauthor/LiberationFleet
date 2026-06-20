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
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { GiftService } from '../../services/gift.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import { GiftLogEntry, GiftVerificationAction } from '../../models/gift.model';

@Component({
  selector: 'app-gift-log',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent],
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
  completionPlatformSelections: Record<number, number | ''> = {};
  backButton!: ActionBarButton;
  recordButton!: ActionBarButton;

  private readonly pageSize = 50;
  private intersectionObserver?: IntersectionObserver;
  private sentinelChangesSubscription?: { unsubscribe(): void };
  private scrollToBottomOnNextRender = false;

  private router = inject(Router);
  private giftService = inject(GiftService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.recordButton = {
      label: 'Record Gift',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/gift-log/record'])
    };

    this.authService.currentUser$.subscribe(user => {
      this.activeUserId = user?.id ?? 0;
    });

    this.giftService.getSeasonStatus().subscribe({
      next: status => {
        if (!status.seasonStarted) {
          this.router.navigate(['/app/crew/season-setup']);
          return;
        }
        if (!status.userInSeason) {
          this.router.navigate(['/app/crew/join-season']);
          return;
        }
        this.loadGiftLog();
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
      next: result => {
        this.verifyingGiftId = null;
        if (result.success) {
          this.toastService.success(result.message || 'Gift updated');
          delete this.completionPlatformSelections[entry.id];
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
      next: page => {
        this.entries = page.items;
        this.hasMore = page.hasMore;
        this.applyCompletionDefaults(page.items);
        this.loading = false;
        this.scrollToBottomOnNextRender = true;
        setTimeout(() => {
          this.observeLoadMoreSentinel();
          this.scrollToBottom();
        }, 0);
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
      next: page => {
        this.applyCompletionDefaults(page.items);
        this.entries = [...page.items, ...this.entries];
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
}
