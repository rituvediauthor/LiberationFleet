import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs/operators';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { NavigationService } from '../../services/navigation.service';
import { DonationService } from '../../services/donation.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import { DONATION_PRESET_AMOUNTS_USD } from '../../models/donation.model';
import {
  GOFUNDME_DONATION_URL,
  shouldUseExternalDonationCheckout
} from '../../utils/donation-nav.util';

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
  receiptEmail = '';
  submitting = false;
  donationsEnabled = true;
  statusNote = '';
  isLoggedIn = false;
  /** Staging (or live GoFundMe bypass) redirects away from this page. */
  usesExternalDonationCheckout = shouldUseExternalDonationCheckout();

  private navigation = inject(NavigationService);
  private donationService = inject(DonationService);
  private authService = inject(AuthService);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);

  ngOnInit() {
    if (this.usesExternalDonationCheckout) {
      window.location.assign(GOFUNDME_DONATION_URL);
      return;
    }

    this.isLoggedIn = this.authService.isAuthenticated();
    this.authService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user?.email?.trim()) {
        this.receiptEmail = user.email.trim();
      }
    });

    this.backButton = this.isLoggedIn
      ? this.navigation.createBackButton(['/app/profile'])
      : this.navigation.createBackButton(['/']);

    this.donationService.getStatus().subscribe({
      next: status => {
        this.donationsEnabled = status.donationsEnabled;
        if (!status.donationsEnabled) {
          this.statusNote = 'Donations are being set up. Please check back soon.';
        }
      },
      error: () => {
        this.donationsEnabled = false;
        this.statusNote = 'Donations are being set up. Please check back soon.';
      }
    });

    const success = this.route.snapshot.queryParamMap.get('success');
    const canceled = this.route.snapshot.queryParamMap.get('canceled');
    if (success === '1') {
      this.toast.success('Thank you — your donation was received.');
      this.statusNote = this.isLoggedIn
        ? 'Thank you for supporting Liberation Fleet Co. A donation acknowledgment email is on its way to your receipt address. Signed-in donations also appear in your profile tax-year summary.'
        : 'Thank you for supporting Liberation Fleet Co. A donation acknowledgment email is on its way to the address you provided.';
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

  get customAmountOutOfRange(): boolean {
    return this.customUsd != null && (this.customUsd < 1 || this.customUsd > 5000);
  }

  get receiptEmailMissing(): boolean {
    return !this.isLoggedIn && !this.receiptEmail.trim();
  }

  get receiptEmailInvalid(): boolean {
    const email = this.receiptEmail.trim();
    if (!email) {
      return !this.isLoggedIn;
    }
    return !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
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
    if (this.receiptEmailInvalid) {
      this.toast.error(
        this.isLoggedIn
          ? 'Enter a valid email for your donation receipt, or update the email on your account.'
          : 'Enter a valid email address for your donation receipt.'
      );
      return;
    }

    this.submitting = true;
    const email = this.receiptEmail.trim() || undefined;
    this.donationService.createCheckout(cents, email).subscribe({
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
