import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { GiftService } from '../../../services/gift.service';
import { ToastService } from '../../../components/toast/toast.component';
import { GiftHistoryRecipientSummary } from '../../../models/gift.model';

@Component({
  selector: 'app-gift-history-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './gift-history-list.component.html',
  styleUrl: './gift-history-list.component.css'
})
export class GiftHistoryListComponent implements OnInit {
  recipients: GiftHistoryRecipientSummary[] = [];
  loading = true;
  errorMessage = '';
  backButton!: ActionBarButton;

  private router = inject(Router);
  private giftService = inject(GiftService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/profile'])
    };

    this.loadHistory();
  }

  openRecipient(recipient: GiftHistoryRecipientSummary) {
    this.router.navigate(['/app/profile/gift-history', recipient.recipientUserId]);
  }

  private loadHistory() {
    this.loading = true;
    this.errorMessage = '';

    this.giftService.getMyGiftHistory().subscribe({
      next: response => {
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load gift history';
          this.recipients = [];
        } else {
          this.recipients = response.items ?? [];
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load gift history';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
