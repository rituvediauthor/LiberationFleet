import {
  MAX_CACHEABLE_MEDIA_ENTRY_BYTES,
  shouldCacheMediaBlob
} from './media-blob-cache.service';

describe('media-blob-cache size gate', () => {
  it('allows small decrypted blobs into IndexedDB', () => {
    expect(shouldCacheMediaBlob(1024)).toBeTrue();
    expect(shouldCacheMediaBlob(MAX_CACHEABLE_MEDIA_ENTRY_BYTES)).toBeTrue();
  });

  it('rejects empty and oversized blobs (large unencrypted videos)', () => {
    expect(shouldCacheMediaBlob(0)).toBeFalse();
    expect(shouldCacheMediaBlob(MAX_CACHEABLE_MEDIA_ENTRY_BYTES + 1)).toBeFalse();
    expect(shouldCacheMediaBlob(200 * 1024 * 1024)).toBeFalse();
  });
});
