/**
 * Sniff common media MIME types from magic bytes.
 * Safari often refuses to render img[src=blob:…] when the Blob type is
 * missing or application/octet-stream; Chrome is more forgiving.
 */
export function sniffMediaMime(
  bytes: ArrayBuffer | Uint8Array | null | undefined,
  options?: { preferAudio?: boolean }
): string | null {
  if (!bytes) {
    return null;
  }
  const view = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  if (view.length < 12) {
    return null;
  }

  // JPEG
  if (view[0] === 0xff && view[1] === 0xd8 && view[2] === 0xff) {
    return 'image/jpeg';
  }
  // PNG
  if (view[0] === 0x89 && view[1] === 0x50 && view[2] === 0x4e && view[3] === 0x47) {
    return 'image/png';
  }
  // GIF
  if (view[0] === 0x47 && view[1] === 0x49 && view[2] === 0x46 && view[3] === 0x38) {
    return 'image/gif';
  }
  // WEBP: RIFF....WEBP
  if (
    view[0] === 0x52 && view[1] === 0x49 && view[2] === 0x46 && view[3] === 0x46
    && view[8] === 0x57 && view[9] === 0x45 && view[10] === 0x42 && view[11] === 0x50
  ) {
    return 'image/webp';
  }
  // MP4 / MOV / M4A (ftyp box)
  if (view[4] === 0x66 && view[5] === 0x74 && view[6] === 0x79 && view[7] === 0x70) {
    const brand = String.fromCharCode(view[8], view[9], view[10], view[11]).toLowerCase();
    if (brand.includes('m4a') || brand.includes('mp4a') || options?.preferAudio) {
      return 'audio/mp4';
    }
    if (brand.startsWith('qt') || brand.includes('qt')) {
      return 'video/quicktime';
    }
    return 'video/mp4';
  }
  // WebM / Matroska — EBML cannot distinguish audio-only vs video without parsing.
  if (view[0] === 0x1a && view[1] === 0x45 && view[2] === 0xdf && view[3] === 0xa3) {
    return options?.preferAudio ? 'audio/webm' : 'video/webm';
  }
  // OGG (audio/ogg or video/ogg)
  if (view[0] === 0x4f && view[1] === 0x67 && view[2] === 0x67 && view[3] === 0x53) {
    return options?.preferAudio ? 'audio/ogg' : 'audio/ogg';
  }
  // WAV
  if (
    view[0] === 0x52 && view[1] === 0x49 && view[2] === 0x46 && view[3] === 0x46
    && view[8] === 0x57 && view[9] === 0x41 && view[10] === 0x56 && view[11] === 0x45
  ) {
    return 'audio/wav';
  }
  // MP3 ID3 or frame sync
  if (view[0] === 0x49 && view[1] === 0x44 && view[2] === 0x33) {
    return 'audio/mpeg';
  }
  if (view[0] === 0xff && (view[1] & 0xe0) === 0xe0) {
    return 'audio/mpeg';
  }

  return null;
}

/** Normalize MIME for HTML5 media elements (strip codecs; map aliases). */
export function normalizeMediaMime(mime: string | null | undefined): string {
  const raw = (mime || '').trim().toLowerCase();
  if (!raw) {
    return '';
  }
  const base = raw.split(';')[0].trim();
  if (base === 'audio/m4a' || base === 'audio/x-m4a') {
    return 'audio/mp4';
  }
  if (base === 'audio/mp3') {
    return 'audio/mpeg';
  }
  if (base === 'audio/wave' || base === 'audio/x-wav') {
    return 'audio/wav';
  }
  return base;
}

/** Prefer an explicit image/video/audio type; otherwise sniff bytes. */
export function resolveBlobMime(
  declared: string | null | undefined,
  bytes: ArrayBuffer | Uint8Array | null | undefined,
  options?: { preferAudio?: boolean }
): string {
  const declaredTrimmed = normalizeMediaMime(declared);
  const preferAudio = options?.preferAudio
    || declaredTrimmed.startsWith('audio/');

  if (
    declaredTrimmed
    && declaredTrimmed !== 'application/octet-stream'
    && declaredTrimmed !== 'binary/octet-stream'
  ) {
    // Keep audio/* even when magic bytes look like video/webm (voice notes).
    if (preferAudio && declaredTrimmed.startsWith('video/')) {
      return declaredTrimmed.replace(/^video\//, 'audio/');
    }
    return declaredTrimmed;
  }

  return sniffMediaMime(bytes, { preferAudio })
    || declaredTrimmed
    || 'application/octet-stream';
}

export function blobWithResolvedMime(blob: Blob, bytes?: ArrayBuffer): Blob {
  const data = bytes ?? null;
  // Sync path when bytes already loaded; otherwise keep declared type if useful.
  if (data) {
    const mime = resolveBlobMime(blob.type, data);
    if (mime === blob.type) {
      return blob;
    }
    return new Blob([data], { type: mime });
  }
  if (blob.type && blob.type !== 'application/octet-stream') {
    return blob;
  }
  return blob;
}
