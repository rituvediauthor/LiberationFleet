import { Router } from '@angular/router';

/** Fallback campaign URL (staging / emergency bypass only). */
export const GOFUNDME_DONATION_URL = 'https://gofund.me/b9940213d';

/** @deprecated Use {@link GOFUNDME_DONATION_URL}. */
export const STAGING_GOFUNDME_DONATION_URL = GOFUNDME_DONATION_URL;

/**
 * When true, production donate CTAs open GoFundMe instead of Stripe Checkout.
 * Keep false while Stripe nonprofit donations are approved and live.
 * Staging still uses GoFundMe when this is false (no live Stripe on staging).
 */
export const USE_GOFUNDME_FOR_LIVE_DONATIONS = false;

export function isStagingDonationHost(
  hostname: string = typeof location !== 'undefined' ? location.hostname : ''
): boolean {
  return /staging/i.test(hostname);
}

export function isProductionDonationHost(
  hostname: string = typeof location !== 'undefined' ? location.hostname : ''
): boolean {
  if (isStagingDonationHost(hostname)) {
    return false;
  }

  const host = hostname.toLowerCase();
  return (
    host === 'liberationfleet.org'
    || host === 'www.liberationfleet.org'
    || host.endsWith('.liberationfleet.org')
    || host === 'app-lfleet-production.azurewebsites.net'
  );
}

/** External GoFundMe when staging, or when the live bypass flag is on. */
export function shouldUseExternalDonationCheckout(
  hostname: string = typeof location !== 'undefined' ? location.hostname : ''
): boolean {
  if (USE_GOFUNDME_FOR_LIVE_DONATIONS && isProductionDonationHost(hostname)) {
    return true;
  }

  return isStagingDonationHost(hostname);
}

/**
 * Staging (or live bypass): open GoFundMe.
 * Production / local: in-app amount picker → Stripe checkout.
 */
export function navigateToDonate(
  router: Router,
  hostname: string = typeof location !== 'undefined' ? location.hostname : ''
): void {
  if (shouldUseExternalDonationCheckout(hostname)) {
    window.location.assign(GOFUNDME_DONATION_URL);
    return;
  }

  void router.navigate(['/app/donate']);
}
