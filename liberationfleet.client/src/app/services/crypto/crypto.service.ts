import { Injectable } from '@angular/core';
import { base64ToBytes, bytesToBase64, bytesToUtf8, utf8ToBytes } from './crypto-encoding.util';

const IDENTITY_ALGORITHM: EcKeyGenParams = { name: 'ECDH', namedCurve: 'P-256' };
const AES_ALGORITHM = 'AES-GCM';
const PBKDF2_ITERATIONS = 120_000;

@Injectable({
  providedIn: 'root'
})
export class CryptoService {
  async generateIdentityKeyPair(): Promise<CryptoKeyPair> {
    return crypto.subtle.generateKey(IDENTITY_ALGORITHM, true, ['deriveKey', 'deriveBits']);
  }

  async exportPublicKeySpki(publicKey: CryptoKey): Promise<string> {
    const exported = await crypto.subtle.exportKey('spki', publicKey);
    return bytesToBase64(new Uint8Array(exported));
  }

  async importPublicKeySpki(spkiBase64: string): Promise<CryptoKey> {
    return crypto.subtle.importKey(
      'spki',
      base64ToBytes(spkiBase64),
      IDENTITY_ALGORITHM,
      true,
      []
    );
  }

  async exportPublicKeyFromPrivateKey(privateKey: CryptoKey): Promise<string> {
    const jwk = await crypto.subtle.exportKey('jwk', privateKey);
    if (!jwk.x || !jwk.y) {
      throw new Error('Invalid private key material.');
    }

    const publicJwk: JsonWebKey = {
      kty: 'EC',
      crv: 'P-256',
      x: jwk.x,
      y: jwk.y
    };
    const publicKey = await crypto.subtle.importKey(
      'jwk',
      publicJwk,
      IDENTITY_ALGORITHM,
      true,
      []
    );
    return this.exportPublicKeySpki(publicKey);
  }

  async exportPrivateKeyPkcs8(privateKey: CryptoKey): Promise<Uint8Array> {
    const exported = await crypto.subtle.exportKey('pkcs8', privateKey);
    return new Uint8Array(exported);
  }

  async importPrivateKeyPkcs8(pkcs8: Uint8Array): Promise<CryptoKey> {
    return crypto.subtle.importKey(
      'pkcs8',
      pkcs8,
      IDENTITY_ALGORITHM,
      true,
      ['deriveKey', 'deriveBits']
    );
  }

  async wrapPrivateKeyBackup(privateKey: CryptoKey, password: string): Promise<{
    salt: string;
    iv: string;
    ciphertext: string;
  }> {
    const salt = crypto.getRandomValues(new Uint8Array(16));
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const aesKey = await this.derivePasswordKey(password, salt);
    const pkcs8 = await this.exportPrivateKeyPkcs8(privateKey);
    const encrypted = await crypto.subtle.encrypt(
      { name: AES_ALGORITHM, iv },
      aesKey,
      pkcs8
    );

    return {
      salt: bytesToBase64(salt),
      iv: bytesToBase64(iv),
      ciphertext: bytesToBase64(new Uint8Array(encrypted))
    };
  }

  async unwrapPrivateKeyBackup(
    backup: { salt: string; iv: string; ciphertext: string },
    password: string
  ): Promise<CryptoKey> {
    const salt = base64ToBytes(backup.salt);
    const iv = base64ToBytes(backup.iv);
    const ciphertext = base64ToBytes(backup.ciphertext);
    const aesKey = await this.derivePasswordKey(password, salt);
    const pkcs8 = await crypto.subtle.decrypt(
      { name: AES_ALGORITHM, iv },
      aesKey,
      ciphertext
    );
    return this.importPrivateKeyPkcs8(new Uint8Array(pkcs8));
  }

  generateCrewKeyBytes(): Uint8Array {
    return crypto.getRandomValues(new Uint8Array(32));
  }

  async wrapCrewKeyForUser(
    crewKeyBytes: Uint8Array,
    recipientPublicKeySpki: string,
    wrapperPrivateKey: CryptoKey
  ): Promise<{ wrappedCrewKey: string; wrapNonce: string }> {
    const recipientPublicKey = await this.importPublicKeySpki(recipientPublicKeySpki);
    const wrapKey = await this.deriveSharedAesKey(wrapperPrivateKey, recipientPublicKey, 'crew-key-wrap-v1');
    const wrapNonce = crypto.getRandomValues(new Uint8Array(12));
    const encrypted = await crypto.subtle.encrypt(
      { name: AES_ALGORITHM, iv: wrapNonce },
      wrapKey,
      crewKeyBytes
    );

    return {
      wrappedCrewKey: bytesToBase64(new Uint8Array(encrypted)),
      wrapNonce: bytesToBase64(wrapNonce)
    };
  }

  async unwrapCrewKey(
    wrappedCrewKey: string,
    wrapNonce: string,
    wrappedByPublicKeySpki: string,
    recipientPrivateKey: CryptoKey
  ): Promise<Uint8Array> {
    const wrappedByPublicKey = await this.importPublicKeySpki(wrappedByPublicKeySpki);
    const wrapKey = await this.deriveSharedAesKey(recipientPrivateKey, wrappedByPublicKey, 'crew-key-wrap-v1');
    const decrypted = await crypto.subtle.decrypt(
      { name: AES_ALGORITHM, iv: base64ToBytes(wrapNonce) },
      wrapKey,
      base64ToBytes(wrappedCrewKey)
    );
    return new Uint8Array(decrypted);
  }

  async importCrewAesKey(rawKey: Uint8Array): Promise<CryptoKey> {
    return crypto.subtle.importKey('raw', rawKey, { name: AES_ALGORITHM }, false, ['encrypt', 'decrypt']);
  }

  async encryptJson<T>(crewAesKey: CryptoKey, payload: T): Promise<{ nonce: string; ciphertext: string }> {
    const nonce = crypto.getRandomValues(new Uint8Array(12));
    const plaintext = utf8ToBytes(JSON.stringify(payload));
    const encrypted = await crypto.subtle.encrypt(
      { name: AES_ALGORITHM, iv: nonce },
      crewAesKey,
      plaintext
    );

    return {
      nonce: bytesToBase64(nonce),
      ciphertext: bytesToBase64(new Uint8Array(encrypted))
    };
  }

  async decryptJson<T>(crewAesKey: CryptoKey, nonce: string, ciphertext: string): Promise<T> {
    const decrypted = await crypto.subtle.decrypt(
      { name: AES_ALGORITHM, iv: base64ToBytes(nonce) },
      crewAesKey,
      base64ToBytes(ciphertext)
    );
    return JSON.parse(bytesToUtf8(new Uint8Array(decrypted))) as T;
  }

  private async derivePasswordKey(password: string, salt: Uint8Array): Promise<CryptoKey> {
    const passwordKey = await crypto.subtle.importKey(
      'raw',
      utf8ToBytes(password),
      'PBKDF2',
      false,
      ['deriveKey']
    );

    return crypto.subtle.deriveKey(
      {
        name: 'PBKDF2',
        salt,
        iterations: PBKDF2_ITERATIONS,
        hash: 'SHA-256'
      },
      passwordKey,
      { name: AES_ALGORITHM, length: 256 },
      false,
      ['encrypt', 'decrypt']
    );
  }

  private async deriveSharedAesKey(
    privateKey: CryptoKey,
    publicKey: CryptoKey,
    info: string
  ): Promise<CryptoKey> {
    const sharedBits = await crypto.subtle.deriveBits(
      { name: 'ECDH', public: publicKey },
      privateKey,
      256
    );

    const ikm = await crypto.subtle.importKey(
      'raw',
      new Uint8Array(sharedBits),
      { name: 'HKDF' },
      false,
      ['deriveKey']
    );

    return crypto.subtle.deriveKey(
      {
        name: 'HKDF',
        hash: 'SHA-256',
        salt: new Uint8Array(),
        info: utf8ToBytes(info)
      },
      ikm,
      { name: AES_ALGORITHM, length: 256 },
      false,
      ['encrypt', 'decrypt']
    );
  }
}
