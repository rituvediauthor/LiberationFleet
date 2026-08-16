import { normalizeMediaMime, resolveBlobMime, sniffMediaMime } from './media-mime.util';

describe('media-mime.util', () => {
  it('sniffs JPEG magic bytes', () => {
    const bytes = new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0, 0, 0, 0, 0]);
    expect(sniffMediaMime(bytes)).toBe('image/jpeg');
  });

  it('replaces octet-stream with sniffed JPEG for Safari img blobs', () => {
    const bytes = new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0, 0, 0, 0, 0]);
    expect(resolveBlobMime('application/octet-stream', bytes)).toBe('image/jpeg');
  });

  it('keeps an explicit image mime', () => {
    const bytes = new Uint8Array([0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0, 0, 0, 0, 0]);
    expect(resolveBlobMime('image/png', bytes)).toBe('image/png');
  });

  it('normalizes audio aliases and strips codecs', () => {
    expect(normalizeMediaMime('audio/webm;codecs=opus')).toBe('audio/webm');
    expect(normalizeMediaMime('audio/m4a')).toBe('audio/mp4');
    expect(normalizeMediaMime('audio/mp3')).toBe('audio/mpeg');
  });

  it('sniffs WebM as audio when preferAudio is set', () => {
    const bytes = new Uint8Array([0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0]);
    expect(sniffMediaMime(bytes)).toBe('video/webm');
    expect(sniffMediaMime(bytes, { preferAudio: true })).toBe('audio/webm');
    expect(resolveBlobMime('application/octet-stream', bytes, { preferAudio: true })).toBe('audio/webm');
    expect(resolveBlobMime('audio/webm;codecs=opus', bytes)).toBe('audio/webm');
  });
});
