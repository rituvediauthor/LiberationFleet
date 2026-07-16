import { mergePaymentPlatformOptions } from './payment-platform-options.util';

describe('payment-platform-options.util', () => {
  it('merges known accounts into options and sorts by name', () => {
    const merged = mergePaymentPlatformOptions(
      [{ id: 1, name: 'PayPal' }],
      [
        { id: -1, platformId: 2, platform: 'Venmo', handle: 'me' },
        { id: -2, platformId: 0, platform: 'Ignored', handle: 'x' }
      ]
    );

    expect(merged.map(o => o.name)).toEqual(['PayPal', 'Venmo']);
  });

  it('prefers customPlatformName when platform string is empty', () => {
    const merged = mergePaymentPlatformOptions(
      [],
      [{ id: -1, platformId: 9, platform: '', handle: 'h', customPlatformName: 'Custom Cash' }]
    );

    expect(merged).toEqual([{ id: 9, name: 'Custom Cash' }]);
  });
});
