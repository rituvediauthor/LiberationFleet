import {
  defaultSystemPlatformId,
  defaultSystemPlatformSelection,
  mergePaymentPlatformOptions
} from './payment-platform-options.util';
import { CUSTOM_PLATFORM_OPTION_ID } from '../models/profile.model';

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

  it('defaults to the first system platform when options exist', () => {
    expect(defaultSystemPlatformId([{ id: 2, name: 'Venmo' }, { id: 1, name: 'PayPal' }])).toBe(2);
    expect(defaultSystemPlatformSelection([{ id: 2, name: 'Venmo' }])).toEqual({
      platformId: 2,
      platform: 'Venmo'
    });
  });

  it('falls back to custom when no system platforms exist', () => {
    expect(defaultSystemPlatformId([])).toBe(CUSTOM_PLATFORM_OPTION_ID);
    expect(defaultSystemPlatformSelection([])).toEqual({
      platformId: CUSTOM_PLATFORM_OPTION_ID,
      platform: ''
    });
  });

  it('honors a preferred system platform when present', () => {
    expect(
      defaultSystemPlatformId(
        [{ id: 1, name: 'PayPal' }, { id: 2, name: 'Venmo' }],
        2
      )
    ).toBe(2);
  });
});
