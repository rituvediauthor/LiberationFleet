import {
  GOFUNDME_DONATION_URL,
  isProductionDonationHost,
  isStagingDonationHost,
  navigateToDonate,
  shouldUseExternalDonationCheckout
} from './donation-nav.util';

describe('donation-nav.util', () => {
  it('detects staging hostnames', () => {
    expect(isStagingDonationHost('staging.liberationfleet.com')).toBe(true);
    expect(isStagingDonationHost('app.staging.example.com')).toBe(true);
    expect(isStagingDonationHost('liberationfleet.com')).toBe(false);
    expect(isStagingDonationHost('localhost')).toBe(false);
  });

  it('detects production donation hosts', () => {
    expect(isProductionDonationHost('liberationfleet.org')).toBe(true);
    expect(isProductionDonationHost('www.liberationfleet.org')).toBe(true);
    expect(isProductionDonationHost('app.liberationfleet.org')).toBe(true);
    expect(isProductionDonationHost('app-lfleet-production.azurewebsites.net')).toBe(true);
    expect(isProductionDonationHost('localhost')).toBe(false);
    expect(isProductionDonationHost('staging.liberationfleet.org')).toBe(false);
  });

  it('uses Stripe on production and localhost; GoFundMe on staging', () => {
    expect(shouldUseExternalDonationCheckout('liberationfleet.org')).toBe(false);
    expect(shouldUseExternalDonationCheckout('localhost')).toBe(false);
    expect(shouldUseExternalDonationCheckout('staging.example.com')).toBe(true);
  });

  it('navigates to in-app donate on production', () => {
    const assignSpy = spyOn(window.location, 'assign');
    const router = jasmine.createSpyObj('Router', ['navigate']);

    navigateToDonate(router as never, 'liberationfleet.org');

    expect(assignSpy).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/app/donate']);
  });

  it('assigns GoFundMe on staging', () => {
    const assignSpy = spyOn(window.location, 'assign');
    const router = jasmine.createSpyObj('Router', ['navigate']);

    navigateToDonate(router as never, 'staging.example.com');

    expect(assignSpy).toHaveBeenCalledWith(GOFUNDME_DONATION_URL);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('navigates to in-app donate on localhost', () => {
    const assignSpy = spyOn(window.location, 'assign');
    const router = jasmine.createSpyObj('Router', ['navigate']);

    navigateToDonate(router as never, 'localhost');

    expect(assignSpy).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/app/donate']);
  });
});
