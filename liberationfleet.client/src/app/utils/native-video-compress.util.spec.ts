import { hasNativeVideoCompressPlugin } from './video-platform.policy';
import { isNativeApp } from './app-platform.util';

describe('hasNativeVideoCompressPlugin', () => {
  it('is false outside a Capacitor native shell (web / PWA / Karma)', () => {
    // VideoCompressor may still be registered as a web stub; native gate must stay off.
    expect(isNativeApp()).toBeFalse();
    expect(hasNativeVideoCompressPlugin()).toBeFalse();
  });
});
