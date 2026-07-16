/** Strict media allowlist for attachments (MIME ∩ extension). Blocks SVG and exotic types. */

export type AttachmentMediaKind = 'image' | 'video' | 'audio';

export type AttachmentValidationResult =
  | { ok: true; kind: AttachmentMediaKind }
  | { ok: false; reason: 'unsupported' | 'too-large' | 'blocked' };

export const ALLOWED_IMAGE_MIME = new Set([
  'image/jpeg',
  'image/jpg',
  'image/png',
  'image/webp',
  'image/gif'
]);

export const ALLOWED_VIDEO_MIME = new Set([
  'video/mp4',
  'video/webm',
  'video/quicktime'
]);

export const ALLOWED_AUDIO_MIME = new Set([
  'audio/mpeg',
  'audio/mp3',
  'audio/mp4',
  'audio/m4a',
  'audio/aac',
  'audio/wav',
  'audio/wave',
  'audio/x-wav',
  'audio/webm',
  'audio/ogg',
  'audio/flac'
]);

const ALLOWED_IMAGE_EXT = new Set(['jpg', 'jpeg', 'png', 'webp', 'gif']);
const ALLOWED_VIDEO_EXT = new Set(['mp4', 'webm', 'mov']);
const ALLOWED_AUDIO_EXT = new Set(['mp3', 'wav', 'm4a', 'ogg', 'aac', 'flac', 'webm']);

/** Rejected even if claimed as image/* (scriptable / polyglot-prone). */
const BLOCKED_MIME = new Set([
  'image/svg+xml',
  'image/svg',
  'text/html',
  'application/xhtml+xml',
  'application/javascript',
  'text/javascript',
  'application/x-msdownload',
  'application/x-msdos-program'
]);

const DANGEROUS_NAME = /\.(svg|html?|xhtml|js|mjs|cjs|exe|dll|msi|bat|cmd|ps1|vbs|wsf|scr|com|jar|apk|sh|php|asp|aspx|cgi)(\.|$)/i;

export const MAX_IMAGE_BYTES = 12 * 1024 * 1024;
export const MAX_VIDEO_BYTES = 50 * 1024 * 1024;
export const MAX_AUDIO_BYTES = 15 * 1024 * 1024;

/** Max ciphertext characters accepted for a single media asset. */
export const MAX_MEDIA_CIPHERTEXT_CHARS = 20 * 1024 * 1024;

export const SAFE_DATA_URL_PREFIXES = [
  'data:image/jpeg;',
  'data:image/jpg;',
  'data:image/png;',
  'data:image/webp;',
  'data:image/gif;',
  'data:video/mp4;',
  'data:video/webm;',
  'data:video/quicktime;',
  'data:audio/mpeg;',
  'data:audio/mp3;',
  'data:audio/mp4;',
  'data:audio/m4a;',
  'data:audio/aac;',
  'data:audio/wav;',
  'data:audio/wave;',
  'data:audio/x-wav;',
  'data:audio/webm;',
  'data:audio/ogg;',
  'data:audio/flac;'
] as const;

export function defaultAcceptAttribute(kinds: AttachmentMediaKind[] = ['image', 'video', 'audio']): string {
  const parts: string[] = [];
  if (kinds.includes('image')) {
    parts.push('image/jpeg,image/png,image/webp,image/gif,.jpg,.jpeg,.png,.webp,.gif');
  }
  if (kinds.includes('video')) {
    parts.push('video/mp4,video/webm,video/quicktime,.mp4,.webm,.mov');
  }
  if (kinds.includes('audio')) {
    parts.push('audio/mpeg,audio/mp4,audio/wav,audio/ogg,audio/webm,audio/aac,audio/flac,.mp3,.m4a,.wav,.ogg,.aac,.flac,.webm');
  }
  return parts.join(',');
}

export function validateAttachmentFile(
  file: Pick<File, 'name' | 'type' | 'size'>,
  allowedKinds: AttachmentMediaKind[] = ['image', 'video', 'audio']
): AttachmentValidationResult {
  if (DANGEROUS_NAME.test(file.name)) {
    return { ok: false, reason: 'blocked' };
  }

  const mime = (file.type || '').toLowerCase().trim();
  if (mime && BLOCKED_MIME.has(mime)) {
    return { ok: false, reason: 'blocked' };
  }

  // Empty MIME alone is OK if extension is allowlisted; bare octet-stream needs extension.
  if (mime === 'application/octet-stream') {
    // Fall through to extension check only.
  } else if (mime && !kindFromAllowedMime(mime) && !mime.startsWith('image/') && !mime.startsWith('video/') && !mime.startsWith('audio/')) {
    return { ok: false, reason: 'unsupported' };
  } else if (mime.startsWith('image/') || mime.startsWith('video/') || mime.startsWith('audio/')) {
    if (!kindFromAllowedMime(mime)) {
      return { ok: false, reason: 'blocked' };
    }
  }

  const extension = file.name.includes('.')
    ? file.name.split('.').pop()!.toLowerCase()
    : '';

  const kindFromMime = kindFromAllowedMime(mime);
  const kindFromExt = kindFromAllowedExtension(extension);

  let kind: AttachmentMediaKind | null = null;
  if (kindFromMime && kindFromExt) {
    kind = kindFromMime === kindFromExt ? kindFromMime : null;
  } else if (kindFromMime) {
    kind = kindFromMime;
  } else if (kindFromExt && (!mime || mime === 'application/octet-stream')) {
    kind = kindFromExt;
  }

  if (!kind || !allowedKinds.includes(kind)) {
    return { ok: false, reason: 'unsupported' };
  }

  const maxBytes = maxBytesForKind(kind);
  if (file.size > maxBytes) {
    return { ok: false, reason: 'too-large' };
  }

  return { ok: true, kind };
}

export function maxBytesForKind(kind: AttachmentMediaKind): number {
  if (kind === 'image') {
    return MAX_IMAGE_BYTES;
  }
  if (kind === 'video') {
    return MAX_VIDEO_BYTES;
  }
  return MAX_AUDIO_BYTES;
}

export function isSafeMediaDataUrl(dataUrl: string | null | undefined): boolean {
  if (!dataUrl) {
    return false;
  }
  const lower = dataUrl.slice(0, 64).toLowerCase();
  return SAFE_DATA_URL_PREFIXES.some(prefix => lower.startsWith(prefix));
}

function kindFromAllowedMime(mime: string): AttachmentMediaKind | null {
  if (!mime) {
    return null;
  }
  if (ALLOWED_IMAGE_MIME.has(mime)) {
    return 'image';
  }
  if (ALLOWED_VIDEO_MIME.has(mime)) {
    return 'video';
  }
  if (ALLOWED_AUDIO_MIME.has(mime)) {
    return 'audio';
  }
  return null;
}

function kindFromAllowedExtension(extension: string): AttachmentMediaKind | null {
  if (!extension) {
    return null;
  }
  if (ALLOWED_IMAGE_EXT.has(extension)) {
    return 'image';
  }
  if (ALLOWED_VIDEO_EXT.has(extension)) {
    return 'video';
  }
  if (ALLOWED_AUDIO_EXT.has(extension)) {
    return 'audio';
  }
  return null;
}
