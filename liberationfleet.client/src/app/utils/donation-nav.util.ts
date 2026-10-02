import { Router } from '@angular/router';

/**
 * Temporary external destination while Stripe nonprofit/fundraising review is unresolved.
 * Flip {@link USE_GOFUNDME_FOR_LIVE_DONATIONS} to false once Stripe payouts are confirmed safe.
 */
export const GOFUNDME_DONATION_URL = 'https://gofund.me/b9940213d';

/** @deprecated Use {@link GOFUNDME_DONATION_URL}. */
export const STAGING_GOFUNDME_DONATION_URL = GOFUNDME_DONATION_URL;

/**
 * When true, production (and staging) donate CTAs open GoFundMe instead of Stripe Checkout.
 * Localhost stays on the in-app Stripe flow for development.
 */
export const USE_GOFUNDME_FOR_LIVE_DONATIONS = true;

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

/** Live hosts send donors to GoFundMe until Stripe is explicitly re-enabled. */
export function shouldUseExternalDonationCheckout(
  hostname: string = typeof location !== 'undefined' ? location.hostname : ''
): boolean {
  if (!USE_GOFUNDME_FOR_LIVE_DONATIONS) {
    return isStagingDonationHost(hostname);
  }

  return isStagingDonationHost(hostname) || isProductionDonationHost(hostname);
}

/**
 * Live (prod/staging): open the GoFundMe campaign.
 * Local / other hosts: in-app amount picker → Stripe checkout.
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
