import {
  MAX_VIDEO_BYTES,
  MAX_VIDEO_DURATION_SEC,
  MAX_VIDEO_INPUT_BYTES
} from './media-attachment-allowlist.util';

export type VideoPrepProgress = (percent: number, label: string) => void;

export interface PreparedVideoAttachment {
  /** Original file bytes — never browser-re-encoded (keeps audio intact). */
  file: File;
  durationSec: number;
}

/**
 * E2EE-friendly video attachment preparation.
 *
 * Browser canvas/MediaRecorder re-encode is intentionally not used: on iPhone it
 * routinely drops audio or hangs after the Photos picker. Instead we:
 *  1. Validate type / size / duration
 *  2. Hand back the original file for client-side AES encrypt + upload
 *
 * The server only ever receives ciphertext (existing crypto upload path).
 */
export async function prepareVideoAttachment(
  file: File,
  options?: { onProgress?: VideoPrepProgress; signal?: AbortSignal }
): Promise<PreparedVideoAttachment> {
  throwIfAborted(options?.signal);
  options?.onProgress?.(5, 'Checking video…');

  if (!isAllowedVideoFile(file)) {
    throw new Error('Unsupported video type. Use MP4, MOV, or WebM.');
  }

  if (file.size > MAX_VIDEO_INPUT_BYTES) {
    const maxMb = Math.floor(MAX_VIDEO_INPUT_BYTES / (1024 * 1024));
    throw new Error(`Videos must be ${maxMb} MB or smaller.`);
  }

  const maxMb = Math.floor(MAX_VIDEO_BYTES / (1024 * 1024));
  if (file.size > MAX_VIDEO_BYTES) {
    throw new Error(
      `Video is ${formatMb(file.size)} MB. Trim it in Photos to under ${maxMb} MB so it can upload with sound intact.`
    );
  }

  options?.onProgress?.(40, 'Reading video…');
  const durationSec = await readVideoDurationSec(file, options?.signal);
  throwIfAborted(options?.signal);

  if (!Number.isFinite(durationSec) || durationSec <= 0) {
    throw new Error('Unable to read this video. Try exporting it again from Photos.');
  }

  const maxMinutes = Math.floor(MAX_VIDEO_DURATION_SEC / 60);
  if (durationSec > MAX_VIDEO_DURATION_SEC) {
    throw new Error(`Videos must be ${maxMinutes} minutes or shorter.`);
  }

  options?.onProgress?.(100, 'Ready');
  return {
    file: normalizeVideoFile(file),
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

function formatMb(bytes: number): string {
  return String(Math.ceil(bytes / (1024 * 1024)));
}

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) {
    throw abortError();
  }
}

function abortError(): DOMException {
  return new DOMException('Attachment processing cancelled.', 'AbortError');
}
