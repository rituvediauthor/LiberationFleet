import {
  MAX_VIDEO_BYTES,
  MAX_VIDEO_DURATION_SEC,
  MAX_VIDEO_INPUT_BYTES
} from './media-attachment-allowlist.util';

const MAX_IMAGE_DIMENSION = 1920;
const JPEG_QUALITY = 0.82;
/** Only skip re-encode for already-safe JPEG under size/dimension limits. */
const SKIP_SAFE_JPEG_BYTES = 250 * 1024;
/** Skip re-encode target size when forced to canvas-compress oversized clips. */
const TARGET_VIDEO_BYTES = 12 * 1024 * 1024;
/** 720p — keep bitrate low so longer clips stay under MAX_VIDEO_BYTES. */
const MAX_VIDEO_DIMENSION = 720;
/** Soft ceiling; actual bitrate is adapted to hit TARGET_VIDEO_BYTES. */
const MAX_VIDEO_BITRATE = 1_200_000;
const MIN_VIDEO_BITRATE = 400_000;
const VIDEO_FPS = 24;
const SKIP_SAFE_AUDIO_BYTES = 200 * 1024;
const AUDIO_BITRATE = 64_000;

export type MediaCompressProgress = (percent: number, label: string) => void;

/** Shared AudioContext — must be resumed during a user gesture (file picker tap). */
let sharedAudioContext: AudioContext | null = null;

/**
 * Call from the attach-button click (user gesture) so later video compression
 * can tap audio without hanging on AudioContext.resume().
 */
export async function warmMediaAudioContext(): Promise<void> {
  const AudioCtx =
    window.AudioContext
    || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
  if (!AudioCtx) {
    return;
  }

  if (!sharedAudioContext || sharedAudioContext.state === 'closed') {
    sharedAudioContext = new AudioCtx();
  }

  if (sharedAudioContext.state === 'suspended') {
    await sharedAudioContext.resume();
  }
}

async function getRunningAudioContext(): Promise<AudioContext> {
  await warmMediaAudioContext();
  if (!sharedAudioContext || sharedAudioContext.state === 'closed') {
    throw new Error(
      'Could not unlock audio for video upload. Tap Attach again, then reselect the video.'
    );
  }

  if (sharedAudioContext.state === 'suspended') {
    try {
      await Promise.race([
        sharedAudioContext.resume(),
        new Promise<never>((_, reject) => {
          setTimeout(() => reject(new Error('AudioContext resume timed out')), 2000);
        })
      ]);
    } catch {
      throw new Error(
        'Could not unlock audio for video upload. Tap Attach again, then reselect the video.'
      );
    }
  }

  if (sharedAudioContext.state !== 'running') {
    throw new Error(
      'Could not unlock audio for video upload. Tap Attach again, then reselect the video.'
    );
  }

  return sharedAudioContext;
}

export async function compressMediaFile(
  file: File,
  type: 'image' | 'video' | 'audio',
  options?: { onProgress?: MediaCompressProgress; signal?: AbortSignal }
): Promise<File> {
  throwIfAborted(options?.signal);

  if (type === 'image') {
    options?.onProgress?.(10, 'Processing image…');
    const result = await compressImage(file);
    throwIfAborted(options?.signal);
    options?.onProgress?.(100, 'Ready');
    return result;
  }

  if (type === 'video') {
    return compressVideo(file, options);
  }

  if (type === 'audio') {
    options?.onProgress?.(10, 'Processing audio…');
    const result = await compressAudio(file);
    throwIfAborted(options?.signal);
    options?.onProgress?.(100, 'Ready');
    return result;
  }

  return file;
}

/** Capture a JPEG poster from an early visible frame (avoids black/grey openers). */
export async function extractVideoPosterFrame(source: File | Blob): Promise<File> {
  const objectUrl = URL.createObjectURL(source);
  const video = document.createElement('video');
  video.src = objectUrl;
  video.muted = true;
  video.playsInline = true;
  video.preload = 'auto';
  video.crossOrigin = 'anonymous';

  try {
    await waitForVideoMetadata(video);
    await waitForVideoDimensions(video);

    const duration = Number.isFinite(video.duration) && video.duration > 0 ? video.duration : 0;
    // ~1st frame, ~2nd frame @24fps, then slightly later fallbacks if the opener is black.
    const candidates = [0, 1 / 24, 2 / 24, 0.12, 0.35, 0.75, 1.25]
      .map(t => (duration > 0 ? Math.min(t, Math.max(0, duration - 0.05)) : t))
      .filter((t, i, arr) => arr.indexOf(t) === i);

    const scale = Math.min(1, MAX_VIDEO_DIMENSION / Math.max(video.videoWidth || 1, video.videoHeight || 1));
    const width = Math.max(2, Math.round((video.videoWidth || 640) * scale));
    const height = Math.max(2, Math.round((video.videoHeight || 360) * scale));
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext('2d', { willReadFrequently: true });
    if (!context) {
      throw new Error('Unable to capture video preview.');
    }

    let bestBlob: Blob | null = null;
    let bestScore = -1;

    for (const seekTo of candidates) {
      await seekVideo(video, seekTo);
      // Give the decoder a paint after seek (Safari often needs this).
      await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      context.drawImage(video, 0, 0, width, height);
      const score = frameVisibilityScore(context, width, height);
      if (score > bestScore) {
        bestScore = score;
        bestBlob = await canvasToBlob(canvas, 'image/jpeg', JPEG_QUALITY);
      }
      // Good enough: not a near-black / flat grey frame.
      if (score >= 18) {
        break;
      }
    }

    if (!bestBlob) {
      throw new Error('Unable to capture video preview.');
    }

    return new File([bestBlob], `video-poster-${Date.now()}.jpg`, {
      type: 'image/jpeg',
      lastModified: Date.now()
    });
  } finally {
    URL.revokeObjectURL(objectUrl);
    video.removeAttribute('src');
    video.load();
  }
}

async function compressImage(file: File): Promise<File> {
  // Always rasterize through canvas → JPEG so SVG/polyglots cannot be stored as-is.
  // Skip only for small, already-JPEG files that decode successfully.
  const mime = (file.type || '').toLowerCase();
  const bitmap = await createImageBitmap(file);
  try {
    const longestEdge = Math.max(bitmap.width, bitmap.height);
    const alreadySafeJpeg =
      (mime === 'image/jpeg' || mime === 'image/jpg')
      && file.size <= SKIP_SAFE_JPEG_BYTES
      && longestEdge <= MAX_IMAGE_DIMENSION;

    if (alreadySafeJpeg) {
      return file;
    }

    const scale = Math.min(1, MAX_IMAGE_DIMENSION / longestEdge);
    const width = Math.max(1, Math.round(bitmap.width * scale));
    const height = Math.max(1, Math.round(bitmap.height * scale));

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext('2d');
    if (!context) {
      throw new Error('Unable to process image.');
    }

    context.drawImage(bitmap, 0, 0, width, height);
    const blob = await canvasToBlob(canvas, 'image/jpeg', JPEG_QUALITY);
    const baseName = file.name.replace(/\.[^.]+$/, '') || 'image';
    return new File([blob], `${baseName}.jpg`, {
      type: 'image/jpeg',
      lastModified: Date.now()
    });
  } finally {
    bitmap.close();
  }
}

async function compressVideo(
  file: File,
  options?: { onProgress?: MediaCompressProgress; signal?: AbortSignal }
): Promise<File> {
  throwIfAborted(options?.signal);
  if (file.size > MAX_VIDEO_INPUT_BYTES) {
    throw new Error('Videos must be 500 MB or smaller before compression.');
  }

  const maxMb = Math.floor(MAX_VIDEO_BYTES / (1024 * 1024));

  // Prefer the original file whenever it fits. That is how audio stayed intact before:
  // canvas re-encode on iPhone often cannot keep sound because opening Photos suspends
  // AudioContext, and muted captureStream drops audio tracks.
  if (file.size <= MAX_VIDEO_BYTES && isPassthroughVideoFile(file)) {
    options?.onProgress?.(15, 'Checking video…');
    await assertVideoDuration(file, options?.signal);
    options?.onProgress?.(100, 'Ready');
    return file;
  }

  options?.onProgress?.(5, 'Compressing video…');
  try {
    await getRunningAudioContext();
  } catch {
    throw new Error(
      `This video is over ${maxMb} MB and needs compression, but the browser blocked audio unlock after the file picker (common on iPhone). Trim it in Photos to under ${maxMb} MB and attach again — that uploads with sound intact.`
    );
  }

  let compressed: File;
  try {
    compressed = await reencodeVideoByPlayback(file, options);
  } catch (error) {
    if (isAbortError(error)) {
      throw error;
    }
    const message = error instanceof Error ? error.message : 'Failed to compress video.';
    throw new Error(
      `${message} Or trim the clip to under ${maxMb} MB in Photos and attach again.`
    );
  }

  throwIfAborted(options?.signal);
  if (compressed.size > MAX_VIDEO_BYTES) {
    throw new Error(
      `Video is still ${Math.ceil(compressed.size / (1024 * 1024))} MB after compression. Please use a shorter or lower-resolution clip (max ${maxMb} MB).`
    );
  }

  options?.onProgress?.(100, 'Ready');
  return compressed;
}

function isPassthroughVideoFile(file: File): boolean {
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

async function assertVideoDuration(
  file: File,
  signal?: AbortSignal
): Promise<void> {
  const objectUrl = URL.createObjectURL(file);
  const video = document.createElement('video');
  video.src = objectUrl;
  video.muted = true;
  video.playsInline = true;
  video.preload = 'metadata';

  try {
    await waitForVideoMetadata(video);
    throwIfAborted(signal);
    if (!Number.isFinite(video.duration) || video.duration <= 0) {
      throw new Error('Unable to read video duration.');
    }
    if (video.duration > MAX_VIDEO_DURATION_SEC) {
      const minutes = Math.floor(MAX_VIDEO_DURATION_SEC / 60);
      throw new Error(`Videos must be ${minutes} minutes or shorter.`);
    }
  } finally {
    URL.revokeObjectURL(objectUrl);
    video.removeAttribute('src');
    video.load();
  }
}

async function compressAudio(file: File): Promise<File> {
  const mime = (file.type || '').toLowerCase();
  if (file.size <= SKIP_SAFE_AUDIO_BYTES && (mime.includes('webm') || mime.includes('ogg') || mime.includes('opus'))) {
    return file;
  }

  try {
    return await reencodeAudio(file);
  } catch {
    return file;
  }
}

async function reencodeAudio(file: File): Promise<File> {
  const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
  const audioContext = new AudioCtx();
  try {
    const arrayBuffer = await file.arrayBuffer();
    const decoded = await audioContext.decodeAudioData(arrayBuffer.slice(0));
    const destination = audioContext.createMediaStreamDestination();
    const source = audioContext.createBufferSource();
    source.buffer = decoded;
    source.connect(destination);

    const preferredMime = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
      ? 'audio/webm;codecs=opus'
      : MediaRecorder.isTypeSupported('audio/webm')
        ? 'audio/webm'
        : MediaRecorder.isTypeSupported('audio/mp4')
          ? 'audio/mp4'
          : '';

    if (!preferredMime) {
      return file;
    }

    const recorder = new MediaRecorder(destination.stream, {
      mimeType: preferredMime,
      audioBitsPerSecond: AUDIO_BITRATE
    });

    const chunks: Blob[] = [];
    recorder.ondataavailable = event => {
      if (event.data.size > 0) {
        chunks.push(event.data);
      }
    };

    const recordingDone = new Promise<Blob>((resolve, reject) => {
      recorder.onstop = () => resolve(new Blob(chunks, { type: preferredMime }));
      recorder.onerror = () => reject(new Error('Audio compression failed'));
    });

    recorder.start(250);
    source.start(0);
    await new Promise<void>(resolve => {
      source.onended = () => resolve();
    });
    recorder.stop();

    const compressed = await recordingDone;
    if (compressed.size === 0 || compressed.size >= file.size) {
      return file;
    }

    const extension = preferredMime.includes('mp4') ? 'm4a' : 'webm';
    const baseName = file.name.replace(/\.[^.]+$/, '') || 'audio';
    return new File([compressed], `${baseName}.${extension}`, {
      type: preferredMime,
      lastModified: Date.now()
    });
  } finally {
    await audioContext.close().catch(() => undefined);
  }
}

async function reencodeVideoByPlayback(
  file: File,
  options?: { onProgress?: MediaCompressProgress; signal?: AbortSignal }
): Promise<File> {
  const objectUrl = URL.createObjectURL(file);
  const video = document.createElement('video');
  video.src = objectUrl;
  video.muted = true;
  video.playsInline = true;
  video.preload = 'auto';

  try {
    await waitForVideoMetadata(video);
    throwIfAborted(options?.signal);
    if (!Number.isFinite(video.duration) || video.duration <= 0) {
      throw new Error('Unable to read video duration.');
    }
    if (video.duration > MAX_VIDEO_DURATION_SEC) {
      const minutes = Math.floor(MAX_VIDEO_DURATION_SEC / 60);
      throw new Error(`Videos must be ${minutes} minutes or shorter.`);
    }

    const scale = Math.min(1, MAX_VIDEO_DIMENSION / Math.max(video.videoWidth, video.videoHeight));
    const width = Math.max(2, Math.round(video.videoWidth * scale / 2) * 2);
    const height = Math.max(2, Math.round(video.videoHeight * scale / 2) * 2);

    // Adaptive bitrate: aim for TARGET_VIDEO_BYTES over the clip length (TikTok-style).
    const adaptiveBitrate = Math.floor((TARGET_VIDEO_BYTES * 8) / Math.max(1, video.duration));
    const videoBitrate = Math.max(MIN_VIDEO_BITRATE, Math.min(MAX_VIDEO_BITRATE, adaptiveBitrate));

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext('2d');
    if (!context) {
      return file;
    }

    const stream = canvas.captureStream(VIDEO_FPS);
    let detachAudio: (() => void) | null = null;

    const audioKnownAbsent = mediaAudioTracksAbsent(video);
    if (!audioKnownAbsent) {
      // Keep original audio — never fall back to silent video.
      detachAudio = await attachVideoAudioRequired(video, stream);
    } else {
      video.muted = true;
    }

    const hasAudio = stream.getAudioTracks().length > 0;
    if (!audioKnownAbsent && !hasAudio) {
      throw new Error(
        'Could not keep audio while compressing this video. Tap Attach again and retry, or try a shorter clip.'
      );
    }

    const preferredMime = pickVideoRecorderMime(hasAudio);
    if (hasAudio && !preferredMime) {
      throw new Error('This browser cannot record video with audio. Try Chrome or an updated Safari.');
    }

    let recorder: MediaRecorder;
    try {
      recorder = new MediaRecorder(stream, {
        mimeType: preferredMime,
        videoBitsPerSecond: videoBitrate,
        ...(hasAudio ? { audioBitsPerSecond: AUDIO_BITRATE } : {})
      });
    } catch (error) {
      if (hasAudio) {
        throw new Error(
          'Could not start video+audio compression in this browser. Try a shorter clip or update your browser.'
        );
      }
      throw error instanceof Error ? error : new Error('Video compression failed to start.');
    }

    const chunks: Blob[] = [];
    recorder.ondataavailable = event => {
      if (event.data.size > 0) {
        chunks.push(event.data);
      }
    };

    const outputMime = recorder.mimeType || preferredMime;
    const recordingDone = new Promise<Blob>((resolve, reject) => {
      recorder.onstop = () => resolve(new Blob(chunks, { type: outputMime }));
      recorder.onerror = () => reject(new Error('Video compression failed'));
    });

    recorder.start(250);
    options?.onProgress?.(8, 'Compressing video…');

    try {
      await new Promise<void>((resolve, reject) => {
        let rafId = 0;
        let lastReported = 8;
        let lastTime = -1;
        let lastAdvanceAt = Date.now();
        let settled = false;
        const hardDeadline = Date.now() + Math.min(180_000, Math.max(20_000, video.duration * 1000 * 2.5));

        const finish = () => {
          if (settled) {
            return;
          }
          settled = true;
          cancelAnimationFrame(rafId);
          resolve();
        };

        const fail = (error: Error) => {
          if (settled) {
            return;
          }
          settled = true;
          cancelAnimationFrame(rafId);
          try {
            if (recorder.state !== 'inactive') {
              recorder.stop();
            }
          } catch {
            // ignore
          }
          reject(error);
        };

        const drawFrame = () => {
          if (settled) {
            return;
          }
          if (options?.signal?.aborted) {
            fail(abortError());
            return;
          }
          if (Date.now() > hardDeadline) {
            fail(new Error('Video compression timed out. Try a shorter clip.'));
            return;
          }

          context.drawImage(video, 0, 0, width, height);
          if (video.duration > 0) {
            const pct = Math.min(95, Math.max(8, Math.round((video.currentTime / video.duration) * 90) + 8));
            if (pct >= lastReported + 2) {
              lastReported = pct;
              options?.onProgress?.(pct, 'Compressing video…');
            }
          }

          if (video.currentTime > lastTime + 0.02) {
            lastTime = video.currentTime;
            lastAdvanceAt = Date.now();
          } else if (
            !video.paused
            && !video.ended
            && Date.now() - lastAdvanceAt > 10_000
          ) {
            fail(new Error('Video compression stalled. Try another clip or format.'));
            return;
          }

          if (video.ended || (video.duration > 0 && video.currentTime >= video.duration - 0.05)) {
            finish();
            return;
          }
          rafId = requestAnimationFrame(drawFrame);
        };

        video.onended = () => finish();
        video.onerror = () => fail(new Error('Unable to compress video'));

        // Must play with audio graph attached (unmuted, volume 0). Do not mute-fallback —
        // that would drop audio from the recording.
        void video.play().then(() => {
          drawFrame();
        }).catch(err => {
          fail(
            err instanceof Error
              ? new Error(`Unable to play video with audio for compression: ${err.message}`)
              : new Error('Unable to play video with audio for compression.')
          );
        });
      });

      throwIfAborted(options?.signal);
      if (recorder.state !== 'inactive') {
        recorder.stop();
      }
      const compressed = await recordingDone;
      if (compressed.size >= file.size) {
        return file;
      }

      const extension = outputMime.includes('mp4') ? 'mp4' : 'webm';
      const baseName = file.name.replace(/\.[^.]+$/, '') || 'video';
      return new File([compressed], `${baseName}.${extension}`, {
        type: outputMime.split(';')[0] || outputMime,
        lastModified: Date.now()
      });
    } finally {
      detachAudio?.();
    }
  } finally {
    URL.revokeObjectURL(objectUrl);
    video.removeAttribute('src');
    video.load();
  }
}

/** True only when the browser explicitly reports the file has no audio (Firefox). */
function mediaAudioTracksAbsent(video: HTMLVideoElement): boolean {
  const withTracks = video as HTMLVideoElement & { mozHasAudio?: boolean };
  // Do not trust audioTracks.length — Chrome often reports 0 even when audio exists.
  return withTracks.mozHasAudio === false;
}

/**
 * Tap element audio into the canvas capture stream. Throws if audio cannot be preserved.
 * Uses the gesture-warmed shared AudioContext (do not close it).
 */
async function attachVideoAudioRequired(
  video: HTMLVideoElement,
  stream: MediaStream
): Promise<() => void> {
  const audioCtx = await getRunningAudioContext();
  video.muted = false;
  video.volume = 0;

  // Prefer element captureStream audio when the browser provides it (Chrome/Firefox).
  try {
    const capture = (
      video as HTMLVideoElement & {
        captureStream?: () => MediaStream;
        mozCaptureStream?: () => MediaStream;
      }
    );
    const sourceStream = capture.captureStream?.() ?? capture.mozCaptureStream?.();
    const capturedAudio = sourceStream?.getAudioTracks() ?? [];
    if (capturedAudio.length > 0) {
      for (const track of capturedAudio) {
        stream.addTrack(track);
      }
      return () => {
        for (const track of capturedAudio) {
          try {
            stream.removeTrack(track);
            track.stop();
          } catch {
            // ignore
          }
        }
      };
    }
  } catch {
    // Fall through to Web Audio tap.
  }

  let source: MediaElementAudioSourceNode;
  try {
    source = audioCtx.createMediaElementSource(video);
  } catch (error) {
    throw new Error(
      'Could not attach video audio for compression. Tap Attach again and retry the same clip.'
    );
  }

  const dest = audioCtx.createMediaStreamDestination();
  // Keep the graph alive on Safari with a silent path to the destination.
  const silent = audioCtx.createGain();
  silent.gain.value = 0;
  source.connect(dest);
  source.connect(silent);
  silent.connect(audioCtx.destination);

  const tracks = dest.stream.getAudioTracks();
  if (tracks.length === 0) {
    try {
      source.disconnect();
      silent.disconnect();
    } catch {
      // ignore
    }
    throw new Error(
      'Could not keep audio while compressing this video. Try another clip or update your browser.'
    );
  }

  for (const track of tracks) {
    stream.addTrack(track);
  }

  return () => {
    try {
      source.disconnect();
      silent.disconnect();
    } catch {
      // ignore
    }
    for (const track of tracks) {
      try {
        stream.removeTrack(track);
        track.stop();
      } catch {
        // ignore
      }
    }
  };
}

function pickVideoRecorderMime(hasAudio: boolean): string {
  const withAudio = [
    'video/mp4',
    'video/webm;codecs=vp9,opus',
    'video/webm;codecs=vp8,opus',
    'video/webm;codecs=vp9',
    'video/webm;codecs=vp8',
    'video/webm'
  ];
  const videoOnly = [
    'video/mp4',
    'video/webm;codecs=vp9',
    'video/webm;codecs=vp8',
    'video/webm'
  ];
  for (const mime of hasAudio ? withAudio : videoOnly) {
    if (MediaRecorder.isTypeSupported(mime)) {
      return mime;
    }
  }
  return hasAudio ? '' : 'video/webm';
}

function waitForVideoMetadata(video: HTMLVideoElement): Promise<void> {
  return new Promise((resolve, reject) => {
    if (video.readyState >= 1) {
      resolve();
      return;
    }

    const timeout = window.setTimeout(() => {
      cleanup();
      reject(new Error('Unable to read video metadata'));
    }, 15_000);

    const cleanup = () => {
      window.clearTimeout(timeout);
      video.onloadedmetadata = null;
      video.onerror = null;
    };

    video.onloadedmetadata = () => {
      cleanup();
      resolve();
    };
    video.onerror = () => {
      cleanup();
      reject(new Error('Unable to read video metadata'));
    };
  });
}

function waitForVideoDimensions(video: HTMLVideoElement): Promise<void> {
  if (video.videoWidth > 0 && video.videoHeight > 0) {
    return Promise.resolve();
  }

  return new Promise((resolve, reject) => {
    const timeout = window.setTimeout(() => {
      cleanup();
      // Allow poster attempt even if dimensions stay 0 (canvas uses fallbacks).
      resolve();
    }, 4000);

    const onReady = () => {
      if (video.videoWidth > 0 && video.videoHeight > 0) {
        cleanup();
        resolve();
      }
    };

    const cleanup = () => {
      window.clearTimeout(timeout);
      video.removeEventListener('loadeddata', onReady);
      video.removeEventListener('loadedmetadata', onReady);
      video.onerror = null;
    };

    video.addEventListener('loadeddata', onReady);
    video.addEventListener('loadedmetadata', onReady);
    video.onerror = () => {
      cleanup();
      reject(new Error('Unable to read video frame'));
    };
  });
}

function seekVideo(video: HTMLVideoElement, time: number): Promise<void> {
  return new Promise((resolve, reject) => {
    if (!Number.isFinite(time) || time < 0) {
      time = 0;
    }

    // Safari often skips the seeked event when already at the target time.
    if (Math.abs(video.currentTime - time) < 0.001 && video.readyState >= 2) {
      resolve();
      return;
    }

    const timeout = window.setTimeout(() => {
      cleanup();
      resolve();
    }, 2500);

    const onSeeked = () => {
      cleanup();
      resolve();
    };

    const cleanup = () => {
      window.clearTimeout(timeout);
      video.removeEventListener('seeked', onSeeked);
    };

    video.addEventListener('seeked', onSeeked);
    try {
      video.currentTime = time;
    } catch (error) {
      cleanup();
      reject(error);
    }
  });
}

/** Higher score = more visible content (not a black/flat grey opener). */
function frameVisibilityScore(
  context: CanvasRenderingContext2D,
  width: number,
  height: number
): number {
  try {
    const sampleW = Math.min(width, 64);
    const sampleH = Math.min(height, 64);
    const data = context.getImageData(0, 0, sampleW, sampleH).data;
    let sum = 0;
    let sumSq = 0;
    let count = 0;
    for (let i = 0; i < data.length; i += 16) {
      const r = data[i];
      const g = data[i + 1];
      const b = data[i + 2];
      const luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
      sum += luma;
      sumSq += luma * luma;
      count += 1;
    }
    if (count === 0) {
      return 0;
    }
    const mean = sum / count;
    const variance = Math.max(0, sumSq / count - mean * mean);
    // Prefer frames with some brightness and contrast.
    return mean * 0.65 + Math.sqrt(variance) * 0.35;
  } catch {
    return 10;
  }
}

function throwIfAborted(signal?: AbortSignal) {
  if (signal?.aborted) {
    throw abortError();
  }
}

function abortError(): DOMException {
  return new DOMException('Attachment processing cancelled.', 'AbortError');
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

function canvasToBlob(
  canvas: HTMLCanvasElement,
  type: string,
  quality: number
): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      blob => blob ? resolve(blob) : reject(new Error('Image compression failed')),
      type,
      quality
    );
  });
}
