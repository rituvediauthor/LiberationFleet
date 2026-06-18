import { Component, ElementRef, inject, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { GiftService } from '../../services/gift.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import { GiftLogEntry } from '../../models/gift.model';

@Component({
  selector: 'app-gift-log',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './gift-log.component.html',
  styleUrl: './gift-log.component.css'
})
export class GiftLogComponent implements OnInit {
  @ViewChild('logContainer') logContainer?: ElementRef<HTMLDivElement>;

  entries: GiftLogEntry[] = [];
  activeUserId = 0;
  loading = true;
  errorMessage = '';
  completingGiftId: number | null = null;
  backButton!: ActionBarButton;
  recordButton!: ActionBarButton;

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

  completeGift(entry: GiftLogEntry) {
    if (this.completingGiftId) return;

    this.completingGiftId = entry.id;
    this.giftService.completeMiddlemanGift(entry.id).subscribe({
      next: result => {
        this.completingGiftId = null;
        if (result.success) {
          this.toastService.success(result.message || 'Gift completed');
          this.loadGiftLog();
          return;
        }
        this.toastService.error(result.message || 'Failed to complete gift');
      },
      error: () => {
        this.completingGiftId = null;
        this.toastService.error('Failed to complete gift');
      }
    });
  }

  private loadGiftLog() {
    this.loading = true;
    this.errorMessage = '';

    this.giftService.getLogs().subscribe({
      next: entries => {
        this.entries = entries;
        this.loading = false;
        setTimeout(() => this.scrollToBottom(), 0);
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Failed to load gift log';
      }
    });
  }

  private scrollToBottom() {
    const el = this.logContainer?.nativeElement;
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  }
}
