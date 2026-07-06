import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { GiftService } from '../../../services/gift.service';
import { ToastService } from '../../../components/toast/toast.component';
import { GiftHistoryEntry } from '../../../models/gift.model';

@Component({
  selector: 'app-gift-history-detail',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './gift-history-detail.component.html',
  styleUrl: './gift-history-detail.component.css'
})
export class GiftHistoryDetailComponent implements OnInit {
  recipientUsername = '';
  totalAmount = 0;
  gifts: GiftHistoryEntry[] = [];
  loading = true;
  errorMessage = '';
  backButton!: ActionBarButton;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private giftService = inject(GiftService);
  private toastService = inject(ToastService);
  private recipientUserId = 0;

  ngOnInit() {
    this.recipientUserId = Number(this.route.snapshot.paramMap.get('userId'));
    if (!this.recipientUserId) {
      this.loading = false;
      this.errorMessage = 'Invalid recipient.';
      return;
    }

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/profile/gift-history'])
    };

    this.loadHistory();
  }

  private loadHistory() {
    this.loading = true;
    this.errorMessage = '';

    this.giftService.getMyGiftHistoryForRecipient(this.recipientUserId).subscribe({
      next: response => {
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load gift history';
          this.gifts = [];
        } else {
          this.recipientUsername = response.recipientUsername;
          this.totalAmount = response.totalAmount;
          this.gifts = response.items ?? [];
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
