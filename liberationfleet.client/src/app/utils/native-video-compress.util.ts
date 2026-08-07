import { Capacitor } from '@capacitor/core';
import { Directory, Filesystem } from '@capacitor/filesystem';
import { VideoCompressor } from '@honem/native-video-compressor';
import write_blob from 'capacitor-blob-writer';
import { hasNativeVideoCompressPlugin } from './video-platform.policy';

export type NativeVideoCompressProgress = (percent: number, label: string) => void;

/** @see {@link hasNativeVideoCompressPlugin} */
export function isNativeVideoCompressorAvailable(): boolean {
  return hasNativeVideoCompressPlugin();
}

/**
 * AVFoundation / MediaCodec compress via real file URIs (not blob-only WebCodecs).
 * Writes the picker File to Cache when it has no filesystem path, compresses on disk,
 * then reads the compressed output back as a File for encrypt/upload.
 */
export async function compressVideoNativeForUpload(
  file: File,
  options?: { onProgress?: NativeVideoCompressProgress; signal?: AbortSignal }
): Promise<File> {
  if (!isNativeVideoCompressorAvailable()) {
    throw new Error('Native video compression is not available on this device.');
  }

  throwIfAborted(options?.signal);
  options?.onProgress?.(5, 'Preparing video…');

  const staged = await materializeInputPath(file);
  throwIfAborted(options?.signal);

  let outputPath: string | null = null;
  try {
    options?.onProgress?.(20, 'Compressing video…');
    const result = await VideoCompressor.compressVideo({
      inputPath: staged.path,
      quality: 'medium',
      format: 'mp4'
    });
    throwIfAborted(options?.signal);

    outputPath = result.outputPath;
    if (!outputPath) {
      throw new Error('Native compression returned no output path.');
    }

    options?.onProgress?.(85, 'Reading compressed video…');
    const compressed = await readNativeFileAsFile(outputPath, file);
    throwIfAborted(options?.signal);

    if (compressed.size >= file.size) {
      options?.onProgress?.(100, 'Ready');
      return file;
    }

    options?.onProgress?.(100, 'Ready');
    return compressed;
  } finally {
    if (staged.cleanup) {
      await safeDeleteNativePath(staged.path);
    }
    if (outputPath) {
      await safeDeleteNativePath(outputPath);
    }
  }
}

/**
 * Prefer an existing absolute / file:// path on the File (some Cap pickers attach one).
 * content:// and blob: URIs are not accepted by the plugin — those are staged to Cache.
 */
function tryNativePathFromFile(file: File): string | null {
  const anyFile = file as File & { path?: string; nativePath?: string };
  const candidate = (anyFile.path || anyFile.nativePath || '').trim();
  if (!candidate) {
    return null;
  }
  if (candidate.startsWith('file://') || candidate.startsWith('/')) {
    return candidate;
  }
  return null;
}

async function materializeInputPath(
  file: File
): Promise<{ path: string; cleanup: boolean }> {
  const existing = tryNativePathFromFile(file);
  if (existing) {
    return { path: existing, cleanup: false };
  }

  const ext = extensionForVideoFile(file);
  const relativePath = `lf-video-in-${Date.now()}-${randomId()}.${ext}`;
  const uri = await write_blob({
    path: relativePath,
    directory: Directory.Cache,
    blob: file,
    recursive: true
  });

  // Prefer the absolute file:// URI from getUri when write_blob returns a relative form.
  try {
    const { uri: resolved } = await Filesystem.getUri({
      path: relativePath,
      directory: Directory.Cache
    });
    return { path: resolved || uri, cleanup: true };
  } catch {
    return { path: uri, cleanup: true };
  }
}

async function readNativeFileAsFile(nativePath: string, source: File): Promise<File> {
  const webSrc = Capacitor.convertFileSrc(nativePath);
  const response = await fetch(webSrc);
  if (!response.ok) {
    throw new Error(`Unable to read compressed video (HTTP ${response.status}).`);
  }
  const blob = await response.blob();
  if (blob.size <= 0) {
    throw new Error('Compression produced an empty file.');
  }

  const baseName = (source.name || 'video').replace(/\.[^.]+$/, '') || 'video';
  return new File([blob], `${baseName}.mp4`, {
    type: 'video/mp4',
    lastModified: Date.now()
  });
}

async function safeDeleteNativePath(path: string): Promise<void> {
  try {
    await VideoCompressor.deleteFile({ path });
  } catch {
    try {
      if (path.startsWith('file://') || path.startsWith('/')) {
        await Filesystem.deleteFile({ path });
      }
    } catch {
      // Best-effort cleanup of temp cache files.
    }
  }
}

function extensionForVideoFile(file: File): string {
  const name = (file.name || '').toLowerCase();
  if (name.endsWith('.mov')) {
    return 'mov';
  }
  if (name.endsWith('.webm')) {
    return 'webm';
  }
  if (name.endsWith('.m4v')) {
    return 'm4v';
  }
  const mime = (file.type || '').toLowerCase();
  if (mime.includes('quicktime')) {
    return 'mov';
  }
  if (mime.includes('webm')) {
    return 'webm';
  }
  return 'mp4';
}

function randomId(): string {
  return Math.random().toString(36).slice(2, 10);
}

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) {
    throw new DOMException('Attachment processing cancelled.', 'AbortError');
  }
}
