const MAX_IMAGE_DIMENSION = 1920;
const JPEG_QUALITY = 0.82;
/** Only skip re-encode for already-safe JPEG under size/dimension limits. */
const SKIP_SAFE_JPEG_BYTES = 250 * 1024;
/** Poster frame max edge. */
const MAX_POSTER_DIMENSION = 720;
const SKIP_SAFE_AUDIO_BYTES = 200 * 1024;
const AUDIO_BITRATE = 64_000;

export type MediaCompressProgress = (percent: number, label: string) => void;

/**
 * Image / audio compression, plus video prep via {@link prepareVideoAttachment}.
 * Video uses Mediabunny (WebCodecs) when available — never canvas/MediaRecorder
 * (that path broke audio on iPhone).
 */
export async function compressMediaFile(
  file: File,
  type: 'image' | 'video' | 'audio',
  options?: { onProgress?: MediaCompressProgress; signal?: AbortSignal; encrypt?: boolean }
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
    // Lazy: keeps Mediabunny / native compress out of the initial production bundle.
    const { prepareVideoAttachment } = await import('./video-attachment.pipeline');
    const prepared = await prepareVideoAttachment(file, {
      onProgress: options?.onProgress,
      signal: options?.signal,
      encrypt: options?.encrypt
    });
    return prepared.file;
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
  // iOS Safari: decode frames for canvas only after muted inline play.
  video.muted = true;
  video.defaultMuted = true;
  video.playsInline = true;
  video.setAttribute('playsinline', '');
  video.setAttribute('webkit-playsinline', '');
  video.preload = 'auto';
  // Do not set crossOrigin on blob: URLs — it can prevent canvas reads on WebKit.
  video.disableRemotePlayback = true;
  video.style.cssText = 'position:fixed;left:-99999px;top:0;width:2px;height:2px;opacity:0;pointer-events:none;';
  video.src = objectUrl;
  document.body.appendChild(video);

  try {
    await waitForVideoMetadata(video);
    await kickVideoDecoder(video);
    await waitForVideoDimensions(video);

    const duration = Number.isFinite(video.duration) && video.duration > 0 ? video.duration : 0;
    const candidates = [0.05, 0.12, 0.25, 0.5, 0.75, 1.0, 1.25, 0]
      .map(t => (duration > 0 ? Math.min(t, Math.max(0, duration - 0.05)) : t))
      .filter((t, i, arr) => arr.indexOf(t) === i);

    const scale = Math.min(1, MAX_POSTER_DIMENSION / Math.max(video.videoWidth || 1, video.videoHeight || 1));
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
      await waitForPaintedFrame(video);
      context.drawImage(video, 0, 0, width, height);
      const score = frameVisibilityScore(context, width, height);
      if (score > bestScore) {
        bestScore = score;
        bestBlob = await canvasToBlob(canvas, 'image/jpeg', JPEG_QUALITY);
      }
      if (score >= 18) {
        break;
      }
    }

    // Black / empty frames are common on iOS without a decoded frame — don't ship them.
    if (!bestBlob || bestScore < 3) {
      throw new Error('Unable to capture video preview.');
    }

    return new File([bestBlob], `video-poster-${Date.now()}.jpg`, {
      type: 'image/jpeg',
      lastModified: Date.now()
    });
  } finally {
    try {
      video.pause();
    } catch {
      // ignore
    }
    video.removeAttribute('src');
    video.load();
    video.remove();
    URL.revokeObjectURL(objectUrl);
  }
}

async function compressImage(file: File): Promise<File> {
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

/** Force WebKit to decode at least one frame (seek alone often yields black canvases). */
async function kickVideoDecoder(video: HTMLVideoElement): Promise<void> {
  try {
    const playResult = video.play();
    if (playResult && typeof (playResult as Promise<void>).then === 'function') {
      await playResult;
    }
    await waitForPaintedFrame(video);
  } catch {
    // Autoplay may still fail; seek/capture loop is the fallback.
  } finally {
    try {
      video.pause();
    } catch {
      // ignore
    }
  }
}

function waitForPaintedFrame(video: HTMLVideoElement): Promise<void> {
  const anyVideo = video as HTMLVideoElement & {
    requestVideoFrameCallback?: (cb: (now: number) => void) => number;
  };

  if (typeof anyVideo.requestVideoFrameCallback === 'function') {
    return new Promise(resolve => {
      const timeout = window.setTimeout(() => resolve(), 1200);
      anyVideo.requestVideoFrameCallback!(() => {
        window.clearTimeout(timeout);
        resolve();
      });
    });
  }

  return new Promise(resolve => {
    requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
  });
}

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
