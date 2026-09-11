import { Router } from '@angular/router';

/** Temporary staging destination while Stripe checkout stays production-only. */
export const STAGING_GOFUNDME_DONATION_URL = 'https://gofund.me/b9940213d';

export function isStagingDonationHost(
  hostname: string = typeof location !== 'undefined' ? location.hostname : ''
): boolean {
  return /staging/i.test(hostname);
}

/**
 * Staging: open the GoFundMe campaign.
 * Production (and local): in-app amount picker → Stripe checkout.
 */
export function navigateToDonate(
  router: Router,
  hostname: string = typeof location !== 'undefined' ? location.hostname : ''
): void {
  if (isStagingDonationHost(hostname)) {
    window.location.assign(STAGING_GOFUNDME_DONATION_URL);
    return;
  }

  void router.navigate(['/app/donate']);
}
