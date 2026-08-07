import { canCompressVideoWithAudio } from './webcodecs-capability.util';
import { SKIP_COMPRESS_BYTES } from './video-compress.util';

describe('video-compress capability', () => {
  it('exposes a sync WebCodecs probe', () => {
    const result = canCompressVideoWithAudio();
    expect(typeof result).toBe('boolean');
    if (typeof VideoEncoder !== 'undefined' && typeof AudioEncoder !== 'undefined') {
      expect(result).toBeTrue();
    }
  });

  it('keeps the chat-sized skip threshold at 8 MB', () => {
    expect(SKIP_COMPRESS_BYTES).toBe(8 * 1024 * 1024);
  });
});
