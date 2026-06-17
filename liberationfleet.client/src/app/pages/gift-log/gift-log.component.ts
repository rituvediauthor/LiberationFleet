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
  backButton!: ActionBarButton;
  recordButton!: ActionBarButton;
  completingGiftId: number | null = null;

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

    this.loadGiftLog();
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

  isInitiatedByUser(entry: GiftLogEntry): boolean {
    return entry.type === 'initiated' && entry.middlemanId === this.activeUserId;
  }

  completeGift(giftId: number) {
    this.completingGiftId = giftId;
    this.router.navigate(['/app/crew/gift-log/complete'], { 
      queryParams: { giftId }
    });
  }
}
