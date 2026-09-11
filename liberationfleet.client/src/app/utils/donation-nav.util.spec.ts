import {
  isStagingDonationHost,
  navigateToDonate,
  STAGING_GOFUNDME_DONATION_URL
} from './donation-nav.util';

describe('donation-nav.util', () => {
  it('detects staging hostnames', () => {
    expect(isStagingDonationHost('staging.liberationfleet.com')).toBe(true);
    expect(isStagingDonationHost('app.staging.example.com')).toBe(true);
    expect(isStagingDonationHost('liberationfleet.com')).toBe(false);
    expect(isStagingDonationHost('localhost')).toBe(false);
  });

  it('assigns GoFundMe on staging', () => {
    const assignSpy = spyOn(window.location, 'assign');
    const router = jasmine.createSpyObj('Router', ['navigate']);

    navigateToDonate(router as never, 'staging.example.com');

    expect(assignSpy).toHaveBeenCalledWith(STAGING_GOFUNDME_DONATION_URL);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('navigates to in-app donate when not staging', () => {
    const assignSpy = spyOn(window.location, 'assign');
    const router = jasmine.createSpyObj('Router', ['navigate']);

    navigateToDonate(router as never, 'liberationfleet.com');

    expect(assignSpy).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/app/donate']);
  });
});
