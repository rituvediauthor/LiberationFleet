import { MAX_VIDEO_BYTES } from './media-attachment-allowlist.util';

const MAX_IMAGE_DIMENSION = 1920;
const JPEG_QUALITY = 0.82;
/** Only skip re-encode for already-safe JPEG under size/dimension limits. */
const SKIP_SAFE_JPEG_BYTES = 250 * 1024;
const TARGET_VIDEO_BYTES = 8 * 1024 * 1024;
const MAX_VIDEO_DIMENSION = 1280;
const MAX_VIDEO_DURATION_SEC = 45;
const VIDEO_BITRATE = 1_500_000;

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
  if (file.size > MAX_VIDEO_BYTES) {
    throw new Error('Videos must be 50 MB or smaller.');
  }

  if (file.size <= TARGET_VIDEO_BYTES) {
    return file;
  }

  try {
    return await reencodeVideoByPlayback(file);
  } catch {
    return file;
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
    if (!Number.isFinite(video.duration) || video.duration > MAX_VIDEO_DURATION_SEC) {
      return file;
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
