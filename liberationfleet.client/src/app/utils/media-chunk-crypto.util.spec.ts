import { decryptMediaCiphertextToBlob, encryptMediaBlobChunked } from './media-chunk-crypto.util';

describe('media-chunk-crypto', () => {
  async function aesKey(): Promise<CryptoKey> {
    return crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
  }

  it('round-trips a multi-chunk blob', async () => {
    const key = await aesKey();
    // Slightly over one 512 KiB chunk so framing is exercised.
    const plain = new Uint8Array(600_000);
    for (let i = 0; i < plain.length; i++) {
      plain[i] = i & 0xff;
    }
    const source = new Blob([plain], { type: 'video/mp4' });

    const { nonce, ciphertext } = await encryptMediaBlobChunked(key, source, 'video/mp4');
    const cipherBytes = new Uint8Array(await ciphertext.arrayBuffer());
    expect(cipherBytes[0]).toBe(2);

    const out = await decryptMediaCiphertextToBlob(key, nonce, cipherBytes);
    expect(out.type).toBe('video/mp4');
    const outBytes = new Uint8Array(await out.arrayBuffer());
    expect(outBytes.length).toBe(plain.length);
    expect(Array.from(outBytes.slice(0, 32))).toEqual(Array.from(plain.slice(0, 32)));
    expect(Array.from(outBytes.slice(-32))).toEqual(Array.from(plain.slice(-32)));
  });
});
