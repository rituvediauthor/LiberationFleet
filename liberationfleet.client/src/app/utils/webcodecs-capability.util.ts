/**
 * Sync probe: browser can encode video *and* audio via WebCodecs
 * (required for Signal-like compress that keeps sound).
 */
export function canCompressVideoWithAudio(): boolean {
  return typeof VideoEncoder !== 'undefined' && typeof AudioEncoder !== 'undefined';
}
