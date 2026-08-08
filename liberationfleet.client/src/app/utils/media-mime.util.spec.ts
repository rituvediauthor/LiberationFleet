import { resolveBlobMime, sniffMediaMime } from './media-mime.util';

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
});
