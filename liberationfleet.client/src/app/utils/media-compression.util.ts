import { MAX_VIDEO_BYTES, MAX_VIDEO_INPUT_BYTES } from './media-attachment-allowlist.util';

const MAX_IMAGE_DIMENSION = 1920;
const JPEG_QUALITY = 0.82;
/** Only skip re-encode for already-safe JPEG under size/dimension limits. */
const SKIP_SAFE_JPEG_BYTES = 250 * 1024;
const TARGET_VIDEO_BYTES = 2 * 1024 * 1024;
const MAX_VIDEO_DIMENSION = 1280;
const MAX_VIDEO_DURATION_SEC = 45;
const VIDEO_BITRATE = 1_500_000;
const SKIP_SAFE_AUDIO_BYTES = 200 * 1024;
const AUDIO_BITRATE = 64_000;

export async function compressMediaFile(
  file: File,
  type: 'image' | 'video' | 'audio'
): Promise<File> {
  if (type === 'image') {
    return compressImage(file);
  }

  if (type === 'video') {
    return compressVideo(file);
  }

  if (type === 'audio') {
    return compressAudio(file);
  }

  return file;
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

async function compressVideo(file: File): Promise<File> {
  if (file.size > MAX_VIDEO_INPUT_BYTES) {
    throw new Error('Videos must be 500 MB or smaller before compression.');
  }

  const mime = (file.type || '').toLowerCase();
  const alreadyWebFriendly =
    file.size <= TARGET_VIDEO_BYTES
    && (mime.includes('webm') || mime.includes('mp4'));
  if (alreadyWebFriendly) {
    return file;
  }

  let compressed: File;
  try {
    compressed = await reencodeVideoByPlayback(file);
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Failed to compress video.';
    throw new Error(message);
  }

  if (compressed.size > MAX_VIDEO_BYTES) {
    throw new Error(
      `Video is still ${Math.ceil(compressed.size / (1024 * 1024))} MB after compression. Please use a shorter or lower-resolution clip (max ${Math.floor(MAX_VIDEO_BYTES / (1024 * 1024))} MB).`
    );
  }

  return compressed;
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

async function reencodeVideoByPlayback(file: File): Promise<File> {
  const objectUrl = URL.createObjectURL(file);
  const video = document.createElement('video');
  video.src = objectUrl;
  video.muted = true;
  video.playsInline = true;
  video.preload = 'auto';

  try {
    await waitForVideoMetadata(video);
    if (!Number.isFinite(video.duration) || video.duration <= 0) {
      throw new Error('Unable to read video duration.');
    }
    if (video.duration > MAX_VIDEO_DURATION_SEC) {
      throw new Error(`Videos must be ${MAX_VIDEO_DURATION_SEC} seconds or shorter.`);
    }

    const scale = Math.min(1, MAX_VIDEO_DIMENSION / Math.max(video.videoWidth, video.videoHeight));
    const width = Math.max(2, Math.round(video.videoWidth * scale));
    const height = Math.max(2, Math.round(video.videoHeight * scale));

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext('2d');
    if (!context) {
      return file;
    }

    const stream = canvas.captureStream(24);
    const preferredMime = MediaRecorder.isTypeSupported('video/webm;codecs=vp9')
      ? 'video/webm;codecs=vp9'
      : MediaRecorder.isTypeSupported('video/webm;codecs=vp8')
        ? 'video/webm;codecs=vp8'
        : 'video/webm';

    const recorder = new MediaRecorder(stream, {
      mimeType: preferredMime,
      videoBitsPerSecond: VIDEO_BITRATE
    });

    const chunks: Blob[] = [];
    recorder.ondataavailable = event => {
      if (event.data.size > 0) {
        chunks.push(event.data);
      }
    };

    const recordingDone = new Promise<Blob>((resolve, reject) => {
      recorder.onstop = () => resolve(new Blob(chunks, { type: preferredMime }));
      recorder.onerror = () => reject(new Error('Video compression failed'));
    });

    recorder.start(250);

    await new Promise<void>((resolve, reject) => {
      let rafId = 0;
      const drawFrame = () => {
        context.drawImage(video, 0, 0, width, height);
        if (video.ended) {
          resolve();
          return;
        }
        rafId = requestAnimationFrame(drawFrame);
      };

      video.onended = () => {
        cancelAnimationFrame(rafId);
        resolve();
      };
      video.onerror = () => {
        cancelAnimationFrame(rafId);
        reject(new Error('Unable to compress video'));
      };

      void video.play().then(() => {
        drawFrame();
      }).catch(reject);
    });

    recorder.stop();
    const compressed = await recordingDone;
    if (compressed.size >= file.size) {
      return file;
    }

    const baseName = file.name.replace(/\.[^.]+$/, '') || 'video';
    return new File([compressed], `${baseName}.webm`, {
      type: preferredMime,
      lastModified: Date.now()
    });
  } finally {
    URL.revokeObjectURL(objectUrl);
    video.removeAttribute('src');
    video.load();
  }
}

function waitForVideoMetadata(video: HTMLVideoElement): Promise<void> {
  return new Promise((resolve, reject) => {
    if (video.readyState >= 1) {
      resolve();
      return;
    }

    video.onloadedmetadata = () => resolve();
    video.onerror = () => reject(new Error('Unable to read video metadata'));
  });
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
