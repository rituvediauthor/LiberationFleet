import { bytesToBase64, base64ToBytes, bytesToUtf8, utf8ToBytes } from '../services/crypto/crypto-encoding.util';
import { resolveBlobMime } from './media-mime.util';

const AES_ALGORITHM = 'AES-GCM';

/** Plaintext bytes per AES-GCM chunk (Signal-style paging keeps peak RAM low). */
export const MEDIA_CRYPTO_CHUNK_PLAINTEXT = 512 * 1024;

/** Ciphertext framing version: clear header + per-chunk AES-GCM (not one giant GCM). */
export const MEDIA_CRYPTO_V2 = 2;

export type MediaEncryptProgress = (percent: number, label: string) => void;

/**
 * Encrypt a media Blob as v2 chunked AES-GCM.
 * Peak memory ≈ one chunk (~0.5 MB) plus growing Blob parts — not 3–5× the whole file.
 * Envelope nonce is the 12-byte base IV (chunk index occupies the last 4 bytes per chunk).
 */
export async function encryptMediaBlobChunked(
  crewAesKey: CryptoKey,
  source: Blob,
  mimeType: string,
  options?: { onProgress?: MediaEncryptProgress; chunkPlaintextBytes?: number }
): Promise<{ nonce: string; ciphertext: Blob }> {
  const chunkSize = Math.max(64 * 1024, options?.chunkPlaintextBytes ?? MEDIA_CRYPTO_CHUNK_PLAINTEXT);
  const mimeBytes = utf8ToBytes(mimeType || 'application/octet-stream');
  if (mimeBytes.length > 0xffff) {
    throw new Error('Media MIME type is too long.');
  }

  const baseNonce = crypto.getRandomValues(new Uint8Array(12));
  // Chunk counter lives in the last 4 bytes; keep those zero on the stored envelope nonce.
  baseNonce[8] = 0;
  baseNonce[9] = 0;
  baseNonce[10] = 0;
  baseNonce[11] = 0;

  const totalBytes = source.size;
  const chunkCount = Math.max(1, Math.ceil(totalBytes / chunkSize));

  const header = new Uint8Array(2 + 4 + 2 + mimeBytes.length + 4);
  let o = 0;
  header[o++] = MEDIA_CRYPTO_V2;
  header[o++] = 0;
  writeU32LE(header, o, chunkSize); o += 4;
  header[o++] = mimeBytes.length & 0xff;
  header[o++] = (mimeBytes.length >> 8) & 0xff;
  header.set(mimeBytes, o); o += mimeBytes.length;
  writeU32LE(header, o, chunkCount);

  const parts: BlobPart[] = [header];
  options?.onProgress?.(5, 'Encrypting…');

  for (let i = 0; i < chunkCount; i++) {
    const start = i * chunkSize;
    const end = Math.min(totalBytes, start + chunkSize);
    const plainChunk = new Uint8Array(await source.slice(start, end).arrayBuffer());
    const iv = chunkIv(baseNonce, i);
    const cipherBuf = await crypto.subtle.encrypt(
      { name: AES_ALGORITHM, iv },
      crewAesKey,
      plainChunk
    );
    const cipherBytes = new Uint8Array(cipherBuf);
    const lenPrefix = new Uint8Array(4);
    writeU32LE(lenPrefix, 0, cipherBytes.length);
    parts.push(lenPrefix, cipherBytes);

    const pct = 5 + Math.round(((i + 1) / chunkCount) * 90);
    options?.onProgress?.(Math.min(95, pct), 'Encrypting…');

    // Let the UI / iOS watchdog breathe between chunks.
    if ((i & 3) === 3) {
      await yieldToMain();
    }
  }

  options?.onProgress?.(100, 'Encrypted');
  return {
    nonce: bytesToBase64(baseNonce),
    ciphertext: new Blob(parts, { type: 'application/octet-stream' })
  };
}

/**
 * Decrypt media ciphertext to a Blob.
 * Supports: v2 chunked framing, v1 single-GCM binary, legacy JSON {dataUrl}.
 */
export async function decryptMediaCiphertextToBlob(
  crewAesKey: CryptoKey,
  nonce: string,
  ciphertextBytes: Uint8Array | ArrayBuffer
): Promise<Blob> {
  const ciphertext = ciphertextBytes instanceof Uint8Array
    ? ciphertextBytes
    : new Uint8Array(ciphertextBytes);

  if (ciphertext.length > 0 && ciphertext[0] === MEDIA_CRYPTO_V2) {
    return decryptMediaV2(crewAesKey, nonce, ciphertext);
  }

  const decrypted = new Uint8Array(
    await crypto.subtle.decrypt(
      { name: AES_ALGORITHM, iv: base64ToBytes(nonce) },
      crewAesKey,
      ciphertext
    )
  );

  if (decrypted.length > 3 && decrypted[0] === 1) {
    const mimeLen = decrypted[1] | (decrypted[2] << 8);
    const mimeStart = 3;
    const dataStart = mimeStart + mimeLen;
    if (dataStart > decrypted.length) {
      throw new Error('Invalid media payload.');
    }
    const mime = resolveBlobMime(
      bytesToUtf8(decrypted.subarray(mimeStart, dataStart)),
      decrypted.subarray(dataStart)
    );
    const fileBytes = decrypted.subarray(dataStart);
    return new Blob([fileBytes], { type: mime });
  }

  const payload = JSON.parse(bytesToUtf8(decrypted)) as { dataUrl?: string };
  if (!payload?.dataUrl) {
    throw new Error('Unrecognized media payload.');
  }
  const response = await fetch(payload.dataUrl);
  const legacyBlob = await response.blob();
  const legacyBytes = new Uint8Array(await legacyBlob.arrayBuffer());
  return new Blob([legacyBytes], { type: resolveBlobMime(legacyBlob.type, legacyBytes) });
}

async function decryptMediaV2(
  crewAesKey: CryptoKey,
  nonceBase64: string,
  ciphertext: Uint8Array
): Promise<Blob> {
  if (ciphertext.length < 12) {
    throw new Error('Invalid chunked media payload.');
  }

  let o = 0;
  o++; // version
  o++; // reserved
  const chunkSize = readU32LE(ciphertext, o); o += 4;
  const mimeLen = ciphertext[o] | (ciphertext[o + 1] << 8); o += 2;
  if (o + mimeLen + 4 > ciphertext.length) {
    throw new Error('Invalid chunked media header.');
  }
  const mime = bytesToUtf8(ciphertext.subarray(o, o + mimeLen)) || 'application/octet-stream';
  o += mimeLen;
  const chunkCount = readU32LE(ciphertext, o); o += 4;

  if (chunkSize < 1 || chunkCount < 1 || chunkCount > 1_000_000) {
    throw new Error('Invalid chunked media framing.');
  }

  const baseNonce = base64ToBytes(nonceBase64);
  if (baseNonce.length !== 12) {
    throw new Error('Invalid media nonce.');
  }

  const parts: BlobPart[] = [];
  for (let i = 0; i < chunkCount; i++) {
    if (o + 4 > ciphertext.length) {
      throw new Error('Truncated chunked media payload.');
    }
    const cipherLen = readU32LE(ciphertext, o); o += 4;
    if (cipherLen < 1 || o + cipherLen > ciphertext.length) {
      throw new Error('Truncated chunked media chunk.');
    }
    const chunkCipher = ciphertext.subarray(o, o + cipherLen);
    o += cipherLen;

    const plain = await crypto.subtle.decrypt(
      { name: AES_ALGORITHM, iv: chunkIv(baseNonce, i) },
      crewAesKey,
      chunkCipher
    );
    parts.push(new Uint8Array(plain));

    if ((i & 3) === 3) {
      await yieldToMain();
    }
  }

  const firstPart = parts[0];
  const sniffBytes = firstPart instanceof Uint8Array ? firstPart : null;
  return new Blob(parts, { type: resolveBlobMime(mime, sniffBytes) });
}

function chunkIv(baseNonce: Uint8Array, chunkIndex: number): Uint8Array {
  const iv = new Uint8Array(baseNonce);
  iv[8] = (chunkIndex >>> 24) & 0xff;
  iv[9] = (chunkIndex >>> 16) & 0xff;
  iv[10] = (chunkIndex >>> 8) & 0xff;
  iv[11] = chunkIndex & 0xff;
  return iv;
}

function writeU32LE(target: Uint8Array, offset: number, value: number): void {
  target[offset] = value & 0xff;
  target[offset + 1] = (value >>> 8) & 0xff;
  target[offset + 2] = (value >>> 16) & 0xff;
  target[offset + 3] = (value >>> 24) & 0xff;
}

function readU32LE(source: Uint8Array, offset: number): number {
  return (
    source[offset]
    | (source[offset + 1] << 8)
    | (source[offset + 2] << 16)
    | (source[offset + 3] << 24)
  ) >>> 0;
}

function yieldToMain(): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, 0));
}
