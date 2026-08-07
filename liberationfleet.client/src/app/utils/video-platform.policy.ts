import { Capacitor } from '@capacitor/core';
import {
  getAppRuntimePlatform,
  isAppleMobileBrowser,
  isConstrainedMobileRuntime,
  isNativeAndroid,
  isNativeApp,
  isNativeIos
} from './app-platform.util';
import { canCompressVideoWithAudio } from './webcodecs-capability.util';

/** Post-prep encrypt/upload ceiling (desktop / when compress succeeded). */
export const MAX_VIDEO_UPLOAD_BYTES = 300 * 1024 * 1024;

/**
 * Passthrough ceiling when WebCodecs (or native plugin) cannot compress
 * with audio — avoids tab/process kills on phones.
 */
export const MAX_VIDEO_PASSTHROUGH_BYTES = 64 * 1024 * 1024;

/**
 * Max file-picker size when client can compress (Mediabunny / native).
 * Signal-style: accept large camera dumps, shrink before encrypt.
 */
export const MAX_VIDEO_PICK_WITH_COMPRESS_BYTES = 600 * 1024 * 1024;

/**
 * Capacitor AVFoundation / MediaCodec plugin (@honem/native-video-compressor).
 * When true, large picks are allowed even without WebCodecs AudioEncoder.
 */
export function hasNativeVideoCompressPlugin(): boolean {
  return isNativeApp() && Capacitor.isPluginAvailable('VideoCompressor');
}

/** Sync: WebCodecs A/V encode or a native compress plugin. */
export function canCompressVideoForUpload(): boolean {
  return hasNativeVideoCompressPlugin() || canCompressVideoWithAudio();
}

/** Picker / allowlist max for the current runtime. */
export function maxVideoPickerBytes(): number {
  return canCompressVideoForUpload()
    ? MAX_VIDEO_PICK_WITH_COMPRESS_BYTES
    : maxVideoPassthroughBytes();
}

/**
 * Max size of the file we encrypt/upload after prep.
 * With compress: full upload budget. Without: phone-safe passthrough only.
 */
export function maxVideoUploadBytes(): number {
  if (canCompressVideoForUpload()) {
    return MAX_VIDEO_UPLOAD_BYTES;
  }
  return maxVideoPassthroughBytes();
}

export function maxVideoPassthroughBytes(): number {
  if (isConstrainedMobileRuntime()) {
    return MAX_VIDEO_PASSTHROUGH_BYTES;
  }
  return MAX_VIDEO_UPLOAD_BYTES;
}

/** User-facing label for toasts / errors. */
export function videoRuntimeLabel(): string {
  if (isNativeIos()) {
    return 'this iOS app';
  }
  if (isNativeAndroid()) {
    return 'this Android app';
  }
  if (isAppleMobileBrowser()) {
    return 'this iPhone';
  }
  if (getAppRuntimePlatform() === 'android' || /Android/i.test(navigator?.userAgent || '')) {
    return 'this Android device';
  }
  return 'this device';
}

export function videoOverPickerLimitMessage(
  fileName?: string,
  options?: { canCompress?: boolean }
): string {
  const canCompress = options?.canCompress ?? canCompressVideoForUpload();
  const maxBytes = canCompress
    ? MAX_VIDEO_PICK_WITH_COMPRESS_BYTES
    : maxVideoPassthroughBytes();
  const maxMb = Math.floor(maxBytes / (1024 * 1024));
  const subject = fileName?.trim() ? fileName.trim() : 'This video';
  if (canCompress) {
    return `${subject} is over ${maxMb} MB — too large to process.`;
  }
  const tip = isNativeApp()
    ? 'Try a shorter clip, or update the app so on-device compression can shrink larger videos.'
    : 'Try a shorter clip, or attach from a computer — we’ll compress it automatically there.';
  return `${subject} is too large for ${videoRuntimeLabel()} (max ${maxMb} MB without auto-compress). ${tip}`;
}

export function videoCompressFailedMessage(options?: { uploadMaxBytes?: number }): string {
  const maxMb = Math.floor(
    (options?.uploadMaxBytes ?? maxVideoUploadBytes()) / (1024 * 1024)
  );
  return `We couldn’t compress this video enough to upload (need under ${maxMb} MB). Try a shorter clip.`;
}

export function videoStillTooLargeAfterPrepMessage(sizeBytes: number): string {
  return `Video is still ${Math.ceil(sizeBytes / (1024 * 1024))} MB after compressing. Try a shorter clip.`;
}
