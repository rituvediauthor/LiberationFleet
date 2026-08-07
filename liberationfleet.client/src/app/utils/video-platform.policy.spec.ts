import {
  canCompressVideoForUpload,
  MAX_VIDEO_PASSTHROUGH_BYTES,
  MAX_VIDEO_PICK_WITH_COMPRESS_BYTES,
  MAX_VIDEO_UPLOAD_BYTES,
  maxVideoPickerBytes,
  maxVideoUploadBytes,
  videoOverPickerLimitMessage
} from './video-platform.policy';
import { isConstrainedMobileRuntime, isNativeIos, isNativeAndroid } from './app-platform.util';

describe('video-platform.policy', () => {
  it('exposes picker and upload ceilings', () => {
    expect(MAX_VIDEO_PICK_WITH_COMPRESS_BYTES).toBe(600 * 1024 * 1024);
    expect(MAX_VIDEO_UPLOAD_BYTES).toBe(300 * 1024 * 1024);
    expect(MAX_VIDEO_PASSTHROUGH_BYTES).toBe(64 * 1024 * 1024);
  });

  it('ties picker max to compress capability', () => {
    const picker = maxVideoPickerBytes();
    if (canCompressVideoForUpload()) {
      expect(picker).toBe(MAX_VIDEO_PICK_WITH_COMPRESS_BYTES);
      expect(maxVideoUploadBytes()).toBe(MAX_VIDEO_UPLOAD_BYTES);
    } else {
      expect(picker).toBe(
        isConstrainedMobileRuntime() ? MAX_VIDEO_PASSTHROUGH_BYTES : MAX_VIDEO_UPLOAD_BYTES
      );
    }
  });

  it('builds a non-empty over-limit message', () => {
    const message = videoOverPickerLimitMessage('clip.mp4');
    expect(message).toContain('clip.mp4');
    expect(message.length).toBeGreaterThan(20);
  });

  it('reports native vs web runtime helpers without throwing', () => {
    expect(typeof isNativeIos()).toBe('boolean');
    expect(typeof isNativeAndroid()).toBe('boolean');
    expect(typeof isConstrainedMobileRuntime()).toBe('boolean');
  });
});
