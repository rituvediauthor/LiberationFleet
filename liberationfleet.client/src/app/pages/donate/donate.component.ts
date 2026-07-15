import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { NavigationService } from '../../services/navigation.service';
import { DonationService } from '../../services/donation.service';
import { ToastService } from '../../components/toast/toast.component';
import { DONATION_PRESET_AMOUNTS_USD } from '../../models/donation.model';

@Component({
  selector: 'app-donate',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent],
  templateUrl: './donate.component.html',
  styleUrl: './donate.component.css'
})
export class DonateComponent implements OnInit {
  backButton!: ActionBarButton;
  presets = DONATION_PRESET_AMOUNTS_USD;
  selectedUsd: number | 'custom' = 25;
  customUsd: number | null = null;
  submitting = false;
  donationsEnabled = true;
  statusNote = '';

  private navigation = inject(NavigationService);
  private donationService = inject(DonationService);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/profile']);
    this.donationService.getSummary().subscribe({
      next: summary => {
        this.donationsEnabled = summary.donationsEnabled;
        if (!summary.donationsEnabled) {
          this.statusNote = 'Donations are being set up. Please check back soon.';
        }
      }
    });

    const success = this.route.snapshot.queryParamMap.get('success');
    const canceled = this.route.snapshot.queryParamMap.get('canceled');
    if (success === '1') {
      this.toast.success('Thank you — your donation was received.');
      this.statusNote = 'Thank you for supporting Liberation Fleet. A receipt will come from Stripe/your card issuer.';
    } else if (canceled === '1') {
      this.statusNote = 'Checkout canceled. You can still donate below whenever you are ready.';
    }
  }

  get amountCents(): number | null {
    if (this.selectedUsd === 'custom') {
      if (this.customUsd == null || this.customUsd < 1 || this.customUsd > 5000) {
        return null;
      }
      return Math.round(this.customUsd) * 100;
    }
    return this.selectedUsd * 100;
  }

  selectPreset(amount: number) {
    this.selectedUsd = amount;
  }

  selectCustom() {
    this.selectedUsd = 'custom';
  }

  startCheckout() {
    const cents = this.amountCents;
    if (cents == null || this.submitting) {
      this.toast.error('Enter a whole-dollar amount between $1 and $5,000.');
      return;
    }
    if (!this.donationsEnabled) {
      this.toast.error('Donations are not available yet.');
      return;
    }

    this.submitting = true;
    this.donationService.createCheckout(cents).subscribe({
      next: result => {
        this.submitting = false;
        if (!result.success || !result.checkoutUrl) {
          this.toast.error(result.message || 'Unable to start checkout');
          return;
        }
        window.location.href = result.checkoutUrl;
      },
      error: err => {
        this.submitting = false;
        this.toast.error(err?.error?.message || 'Unable to start checkout');
      }
    });
  }
}
