import { MAX_VIDEO_DURATION_SEC } from './media-attachment-allowlist.util';
import {
  canCompressVideoForUploadAsync,
  compressVideoForUpload,
  isAlreadyChatSizedVideo,
  readVideoMaxEdgeSafe,
  VIDEO_MAX_EDGE
} from './video-compress.util';
import {
  maxVideoPickerBytes,
  maxVideoUploadBytes,
  videoCompressFailedMessage,
  videoOverPickerLimitMessage,
  videoResolutionTooHighMessage,
  videoStillTooLargeAfterPrepMessage
} from './video-platform.policy';

export type VideoPrepProgress = (percent: number, label: string) => void;

export interface PreparedVideoAttachment {
  /** File ready for encrypt/upload (compressed when the browser / native plugin supports it). */
  file: File;
  durationSec: number;
}

/**
 * E2EE-friendly video attachment preparation (Signal-like when compress is available).
 *
 * Limits adapt by runtime (Capacitor iOS / Android / web) via video-platform.policy:
 *  - With native AVFoundation/MediaCodec or WebCodecs A/V: pick up to 600 MB, compress to ~720p
 *  - Without (encrypted): phone-safe passthrough (~64 MB); desktop can still upload larger originals
 *  - Unencrypted: same 600 MB ceiling as plain file uploads; no phone 64 MB passthrough and
 *    no 720p downscale/refuse — originals upload as-is (duration + byte limits still apply)
 *
 * Never uses canvas/MediaRecorder re-encode (that path dropped audio on iPhone).
 */
export async function prepareVideoAttachment(
  file: File,
  options?: { onProgress?: VideoPrepProgress; signal?: AbortSignal; encrypt?: boolean }
): Promise<PreparedVideoAttachment> {
  throwIfAborted(options?.signal);
  options?.onProgress?.(5, 'Checking video…');

  if (!isAllowedVideoFile(file)) {
    throw new Error('Unsupported video type. Use MP4, MOV, or WebM.');
  }

  const encrypt = options?.encrypt !== false;
  const canCompress = await canCompressVideoForUploadAsync();
  const pickMaxBytes = maxVideoPickerBytes({ encrypt });
  const uploadMaxBytes = maxVideoUploadBytes({ encrypt });

  if (file.size > pickMaxBytes) {
    throw new Error(videoOverPickerLimitMessage(undefined, { canCompress, encrypt }));
  }

  options?.onProgress?.(15, 'Reading video…');
  const durationSec = await readVideoDurationSec(file, options?.signal);
  throwIfAborted(options?.signal);

  if (!Number.isFinite(durationSec) || durationSec <= 0) {
    throw new Error('Unable to read this video. Try exporting it again from Photos.');
  }

  const maxMinutes = Math.floor(MAX_VIDEO_DURATION_SEC / 60);
  if (durationSec > MAX_VIDEO_DURATION_SEC) {
    throw new Error(`Videos must be ${maxMinutes} minutes or shorter.`);
  }

  let out = normalizeVideoFile(file);

  // Encrypted path: hard 720p ceiling (downscale when possible, else refuse).
  // Unencrypted: keep the original resolution — only duration + byte limits apply.
  if (encrypt) {
    const sourceMaxEdge = await readVideoMaxEdgeSafe(out);
    const exceeds720p = sourceMaxEdge > VIDEO_MAX_EDGE;

    if (canCompress) {
      const mustCompress = exceeds720p || !(await isAlreadyChatSizedVideo(out));
      if (mustCompress) {
        throwIfAborted(options?.signal);
        try {
          out = await compressVideoForUpload(out, {
            signal: options?.signal,
            onProgress: (percent, label) => {
              const mapped = 20 + Math.round(percent * 0.7);
              options?.onProgress?.(mapped, label);
            }
          });
        } catch (error) {
          if (isAbortError(error)) {
            throw error;
          }
          // Never fall back to the un-downscaled original when the source is >720p.
          if (exceeds720p || file.size > uploadMaxBytes) {
            throw new Error(videoCompressFailedMessage({ uploadMaxBytes }));
          }
          out = normalizeVideoFile(file);
        }
      }
    } else if (exceeds720p) {
      // No downscaler available (no WebCodecs A/V, no native plugin) — refuse rather
      // than crash the upload with a full-resolution HD file.
      throw new Error(videoResolutionTooHighMessage());
    }
  }

  if (out.size > uploadMaxBytes) {
    throw new Error(videoStillTooLargeAfterPrepMessage(out.size));
  }

  options?.onProgress?.(100, 'Ready');
  return {
    file: out,
    durationSec
  };
}

function isAllowedVideoFile(file: File): boolean {
  const mime = (file.type || '').toLowerCase();
  const name = (file.name || '').toLowerCase();
  if (mime.includes('mp4') || mime.includes('webm') || mime.includes('quicktime')) {
    return true;
  }
  return name.endsWith('.mp4')
    || name.endsWith('.m4v')
    || name.endsWith('.webm')
    || name.endsWith('.mov');
}

/** Ensure the File has a usable MIME for playback after decrypt. */
function normalizeVideoFile(file: File): File {
  const mime = (file.type || '').toLowerCase();
  if (mime.startsWith('video/')) {
    return file;
  }

  const name = (file.name || '').toLowerCase();
  let type = 'video/mp4';
  if (name.endsWith('.webm')) {
    type = 'video/webm';
  } else if (name.endsWith('.mov')) {
    type = 'video/quicktime';
  }

  return new File([file], file.name || `video-${Date.now()}.mp4`, {
    type,
    lastModified: file.lastModified || Date.now()
  });
}

function readVideoDurationSec(file: File, signal?: AbortSignal): Promise<number> {
  return new Promise((resolve, reject) => {
    const objectUrl = URL.createObjectURL(file);
    const video = document.createElement('video');
    video.preload = 'metadata';
    video.muted = true;
    video.playsInline = true;
    video.src = objectUrl;

    const timeout = window.setTimeout(() => {
      cleanup();
      reject(new Error('Timed out reading video metadata.'));
    }, 20_000);

    const onAbort = () => {
      cleanup();
      reject(abortError());
    };

    const cleanup = () => {
      window.clearTimeout(timeout);
      signal?.removeEventListener('abort', onAbort);
      video.onloadedmetadata = null;
      video.onerror = null;
      URL.revokeObjectURL(objectUrl);
      video.removeAttribute('src');
      video.load();
    };

    if (signal?.aborted) {
      cleanup();
      reject(abortError());
      return;
    }
    signal?.addEventListener('abort', onAbort, { once: true });

    video.onloadedmetadata = () => {
      const duration = video.duration;
      cleanup();
      resolve(duration);
    };
    video.onerror = () => {
      cleanup();
      reject(new Error('Unable to read this video. Try another export or format.'));
    };
  });
}

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) {
    throw abortError();
  }
}

function abortError(): DOMException {
  return new DOMException('Attachment processing cancelled.', 'AbortError');
}

function isAbortError(error: unknown): boolean {
  return (error instanceof DOMException && error.name === 'AbortError')
    || (error instanceof Error && (error.name === 'AbortError' || error.name === 'ConversionCanceledError'));
}
