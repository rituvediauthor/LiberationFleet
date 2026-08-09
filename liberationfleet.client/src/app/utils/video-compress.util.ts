import { canCompressVideoWithAudio } from './webcodecs-capability.util';
import { hasNativeVideoCompressPlugin } from './video-platform.policy';

export { canCompressVideoWithAudio } from './webcodecs-capability.util';
export { canCompressVideoForUpload, hasNativeVideoCompressPlugin } from './video-platform.policy';

export type VideoCompressProgress = (percent: number, label: string) => void;

/** Already small enough that Signal-style re-encode is unnecessary. */
export const SKIP_COMPRESS_BYTES = 8 * 1024 * 1024;
const SKIP_COMPRESS_MAX_EDGE = 720;
const TARGET_WIDTH = 1280;
const TARGET_HEIGHT = 720;
const TARGET_FPS = 24;
const AUDIO_BITRATE = 64_000;

type MediabunnyModule = typeof import('mediabunny');

/**
 * Sync probe is {@link canCompressVideoWithAudio}; this adds encode-config support checks.
 * Also returns true when a native Capacitor compress plugin is present.
 */
export async function canCompressVideoWithAudioAsync(): Promise<boolean> {
  if (hasNativeVideoCompressPlugin()) {
    return true;
  }
  if (!canCompressVideoWithAudio()) {
    return false;
  }

  try {
    const mb = await loadMediabunny();
    const qualityMedium = new mb.Quality('medium');
    const audioQuality = new mb.Quality({ bitrate: AUDIO_BITRATE });
    const [avc, aac, vp9, opus] = await Promise.all([
      mb.canEncodeVideo('avc', { width: TARGET_WIDTH, height: TARGET_HEIGHT, quality: qualityMedium }),
      mb.canEncodeAudio('aac', { numberOfChannels: 2, sampleRate: 48_000, quality: audioQuality }),
      mb.canEncodeVideo('vp9', { width: TARGET_WIDTH, height: TARGET_HEIGHT, quality: qualityMedium }),
      mb.canEncodeAudio('opus', { numberOfChannels: 2, sampleRate: 48_000, quality: audioQuality })
    ]);
    return (avc && aac) || (vp9 && opus);
  } catch {
    return false;
  }
}

/** Alias used by the attachment pipeline (WebCodecs and/or native plugin). */
export async function canCompressVideoForUploadAsync(): Promise<boolean> {
  return canCompressVideoWithAudioAsync();
}

/**
 * Skip compress when the file is already chat-sized (≤8 MB and ≤720p long edge).
 */
export async function isAlreadyChatSizedVideo(file: File): Promise<boolean> {
  if (file.size > SKIP_COMPRESS_BYTES) {
    return false;
  }

  try {
    const edge = await readVideoMaxEdge(file);
    return edge > 0 && edge <= SKIP_COMPRESS_MAX_EDGE;
  } catch {
    // Unknown dimensions — do NOT assume it's fine (a hidden 1080p clip must still downscale).
    return false;
  }
}

/** Long-edge (max of width/height) in pixels, or 0 when dimensions can't be read. */
export async function readVideoMaxEdgeSafe(file: File): Promise<number> {
  try {
    return await readVideoMaxEdge(file);
  } catch {
    return 0;
  }
}

/** 720p long-edge cap enforced for uploads. */
export const VIDEO_MAX_EDGE = SKIP_COMPRESS_MAX_EDGE;

/**
 * Signal-like standard quality: 720p box, medium video, ~64 kbps audio, prefer MP4.
 * Never strips audio: if the source has audio and conversion would discard it, throws.
 * Returns the original file when the compressed result is not smaller.
 *
 * Mediabunny / native compressor are loaded on demand so they stay out of the initial bundle.
 */
export async function compressVideoForUpload(
  file: File,
  options?: { onProgress?: VideoCompressProgress; signal?: AbortSignal }
): Promise<File> {
  throwIfAborted(options?.signal);

  // Native shells: AVFoundation / MediaCodec on real file URIs first.
  if (hasNativeVideoCompressPlugin()) {
    try {
      const { compressVideoNativeForUpload } = await import('./native-video-compress.util');
      return await compressVideoNativeForUpload(file, options);
    } catch (error) {
      if (isAbortError(error)) {
        throw error;
      }
      // Fall through to Mediabunny when WebCodecs A/V encode is available (e.g. Cap Android).
      if (!canCompressVideoWithAudio()) {
        throw error instanceof Error ? error : new Error('Native video compression failed.');
      }
    }
  }

  if (!(await canCompressVideoWithAudioAsync())) {
    throw new Error('This browser cannot compress video with audio.');
  }

  const mb = await loadMediabunny();
  options?.onProgress?.(5, 'Compressing video…');

  const qualityMedium = new mb.Quality('medium');
  const audioQuality = new mb.Quality({ bitrate: AUDIO_BITRATE });
  const preferMp4 = (await mb.canEncodeVideo('avc', {
    width: TARGET_WIDTH,
    height: TARGET_HEIGHT,
    quality: qualityMedium
  })) && (await mb.canEncodeAudio('aac', {
    numberOfChannels: 2,
    sampleRate: 48_000,
    quality: audioQuality
  }));

  const attempts: Array<'mp4' | 'webm'> = preferMp4 ? ['mp4', 'webm'] : ['webm', 'mp4'];
  let lastError: unknown;

  for (const format of attempts) {
    throwIfAborted(options?.signal);
    try {
      const compressed = await convertOnce(mb, file, format, options);
      if (compressed.size >= file.size) {
        options?.onProgress?.(100, 'Ready');
        return file;
      }
      options?.onProgress?.(100, 'Ready');
      return compressed;
    } catch (error) {
      if (isAbortError(error)) {
        throw error;
      }
      lastError = error;
    }
  }

  const message = lastError instanceof Error ? lastError.message : 'Video compression failed.';
  throw new Error(message);
}

async function convertOnce(
  mb: MediabunnyModule,
  file: File,
  format: 'mp4' | 'webm',
  options?: { onProgress?: VideoCompressProgress; signal?: AbortSignal }
): Promise<File> {
  const input = new mb.Input({
    source: new mb.BlobSource(file),
    formats: mb.ALL_FORMATS
  });

  let conversion: Awaited<ReturnType<MediabunnyModule['Conversion']['init']>> | null = null;
  const onAbort = () => {
    void conversion?.cancel();
  };

  try {
    if (options?.signal?.aborted) {
      throw abortError();
    }
    options?.signal?.addEventListener('abort', onAbort, { once: true });

    const audioTrack = await input.getPrimaryAudioTrack();
    const hadAudio = audioTrack != null;

    const target = new mb.BufferTarget();
    const output = new mb.Output({
      format: format === 'mp4' ? new mb.Mp4OutputFormat() : new mb.WebMOutputFormat(),
      target
    });

    conversion = await mb.Conversion.init({
      input,
      output,
      tracks: 'primary',
      showWarnings: false,
      video: {
        width: TARGET_WIDTH,
        height: TARGET_HEIGHT,
        fit: 'contain',
        frameRate: TARGET_FPS,
        ...(format === 'mp4' ? { codec: 'avc' as const } : { codec: 'vp9' as const }),
        quality: new mb.Quality('medium'),
        hardwareAcceleration: 'prefer-hardware'
      },
      audio: hadAudio
        ? {
            ...(format === 'mp4' ? { codec: 'aac' as const } : { codec: 'opus' as const }),
            quality: new mb.Quality({ bitrate: AUDIO_BITRATE })
          }
        : { discard: true }
    });

    if (!conversion.isValid) {
      throw new Error(describeDiscardReasons(conversion.discardedTracks) || 'Unable to compress this video.');
    }

    if (hadAudio && conversion.discardedTracks.some((d: { track: { type: string } }) => d.track.type === 'audio')) {
      throw new Error('Could not keep audio while compressing this video.');
    }

    conversion.onProgress = (progress: number) => {
      const pct = 5 + Math.round(Math.min(1, Math.max(0, progress)) * 90);
      options?.onProgress?.(pct, 'Compressing video…');
    };

    await conversion.execute();
    throwIfAborted(options?.signal);

    const buffer = target.buffer;
    if (!buffer || buffer.byteLength === 0) {
      throw new Error('Compression produced an empty file.');
    }

    const mime = format === 'mp4' ? 'video/mp4' : 'video/webm';
    const ext = format === 'mp4' ? 'mp4' : 'webm';
    const baseName = (file.name || 'video').replace(/\.[^.]+$/, '') || 'video';
    return new File([buffer], `${baseName}.${ext}`, {
      type: mime,
      lastModified: Date.now()
    });
  } finally {
    options?.signal?.removeEventListener('abort', onAbort);
    try {
      input.dispose();
    } catch {
      // ignore
    }
  }
}

function describeDiscardReasons(
  discarded: Array<{ track: { type: string }; reason: string }>
): string {
  if (discarded.length === 0) {
    return '';
  }
  return discarded.map(d => `${d.track.type}: ${d.reason}`).join('; ');
}

let mediabunnyPromise: Promise<MediabunnyModule> | null = null;

function loadMediabunny(): Promise<MediabunnyModule> {
  if (!mediabunnyPromise) {
    mediabunnyPromise = import('mediabunny');
  }
  return mediabunnyPromise;
}

function readVideoMaxEdge(file: File): Promise<number> {
  return new Promise((resolve, reject) => {
    const objectUrl = URL.createObjectURL(file);
    const video = document.createElement('video');
    video.preload = 'metadata';
    video.muted = true;
    video.playsInline = true;
    video.src = objectUrl;

    const timeout = window.setTimeout(() => {
      cleanup();
      reject(new Error('Timed out reading video dimensions.'));
    }, 15_000);

    const cleanup = () => {
      window.clearTimeout(timeout);
      video.onloadedmetadata = null;
      video.onerror = null;
      URL.revokeObjectURL(objectUrl);
      video.removeAttribute('src');
      video.load();
    };

    video.onloadedmetadata = () => {
      const edge = Math.max(video.videoWidth || 0, video.videoHeight || 0);
      cleanup();
      resolve(edge);
    };
    video.onerror = () => {
      cleanup();
      reject(new Error('Unable to read video dimensions.'));
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
    || (error instanceof Error && error.name === 'ConversionCanceledError');
}
