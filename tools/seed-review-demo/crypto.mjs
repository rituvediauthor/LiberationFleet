/**
 * Port of liberationfleet.client crypto primitives (Web Crypto / Node 20+).
 * Keep algorithms / info strings in sync with crypto.service.ts.
 */

import { webcrypto } from 'node:crypto';

const subtle = webcrypto.subtle;
const getRandomValues = (arr) => webcrypto.getRandomValues(arr);

const IDENTITY_ALGORITHM = { name: 'ECDH', namedCurve: 'P-256' };
const AES_ALGORITHM = 'AES-GCM';
const PBKDF2_ITERATIONS = 120_000;
export const BACKUP_WRAP_RECOVERY_KEY = 2;

const textEncoder = new TextEncoder();
const textDecoder = new TextDecoder();

export function bytesToBase64(bytes) {
  return Buffer.from(bytes).toString('base64');
}

export function base64ToBytes(value) {
  return new Uint8Array(Buffer.from(value, 'base64'));
}

export function utf8ToBytes(value) {
  return textEncoder.encode(value);
}

export function bytesToUtf8(bytes) {
  return textDecoder.decode(bytes);
}

export async function generateIdentityKeyPair() {
  return subtle.generateKey(IDENTITY_ALGORITHM, true, ['deriveKey', 'deriveBits']);
}

export async function exportPublicKeySpki(publicKey) {
  const exported = await subtle.exportKey('spki', publicKey);
  return bytesToBase64(new Uint8Array(exported));
}

export async function importPublicKeySpki(spkiBase64) {
  return subtle.importKey('spki', base64ToBytes(spkiBase64), IDENTITY_ALGORITHM, true, []);
}

export async function exportPrivateKeyPkcs8(privateKey) {
  const exported = await subtle.exportKey('pkcs8', privateKey);
  return new Uint8Array(exported);
}

export async function importPrivateKeyPkcs8(pkcs8) {
  return subtle.importKey('pkcs8', pkcs8, IDENTITY_ALGORITHM, true, ['deriveKey', 'deriveBits']);
}

async function deriveSecretKey(secret, salt, wrapVersion) {
  const info = wrapVersion === BACKUP_WRAP_RECOVERY_KEY
    ? utf8ToBytes('lf-recovery-key-wrap-v2')
    : utf8ToBytes('lf-login-password-wrap-v1');

  const secretKey = await subtle.importKey('raw', utf8ToBytes(secret), 'PBKDF2', false, ['deriveBits']);
  const derivedBits = await subtle.deriveBits(
    { name: 'PBKDF2', salt, iterations: PBKDF2_ITERATIONS, hash: 'SHA-256' },
    secretKey,
    256
  );
  const hkdfKey = await subtle.importKey('raw', new Uint8Array(derivedBits), { name: 'HKDF' }, false, ['deriveKey']);
  return subtle.deriveKey(
    { name: 'HKDF', hash: 'SHA-256', salt: new Uint8Array(), info },
    hkdfKey,
    { name: AES_ALGORITHM, length: 256 },
    false,
    ['encrypt', 'decrypt']
  );
}

export async function wrapPrivateKeyBackup(privateKey, secret, wrapVersion = BACKUP_WRAP_RECOVERY_KEY) {
  const salt = getRandomValues(new Uint8Array(16));
  const iv = getRandomValues(new Uint8Array(12));
  const aesKey = await deriveSecretKey(secret, salt, wrapVersion);
  const pkcs8 = await exportPrivateKeyPkcs8(privateKey);
  const encrypted = await subtle.encrypt({ name: AES_ALGORITHM, iv }, aesKey, pkcs8);
  return {
    salt: bytesToBase64(salt),
    iv: bytesToBase64(iv),
    ciphertext: bytesToBase64(new Uint8Array(encrypted)),
    keyVersion: wrapVersion
  };
}

export async function unwrapPrivateKeyBackup(backup, secret) {
  const wrapVersion = backup.keyVersion ?? 1;
  const salt = base64ToBytes(backup.salt);
  const iv = base64ToBytes(backup.iv);
  const ciphertext = base64ToBytes(backup.ciphertext);
  const aesKey = await deriveSecretKey(secret, salt, wrapVersion);
  const pkcs8 = await subtle.decrypt({ name: AES_ALGORITHM, iv }, aesKey, ciphertext);
  return importPrivateKeyPkcs8(new Uint8Array(pkcs8));
}

export async function exportPublicKeyFromPrivateKey(privateKey) {
  const jwk = await subtle.exportKey('jwk', privateKey);
  const publicKey = await subtle.importKey(
    'jwk',
    { kty: 'EC', crv: 'P-256', x: jwk.x, y: jwk.y },
    IDENTITY_ALGORITHM,
    true,
    []
  );
  return exportPublicKeySpki(publicKey);
}

async function deriveSharedAesKey(privateKey, publicKey, info) {
  const sharedBits = await subtle.deriveBits({ name: 'ECDH', public: publicKey }, privateKey, 256);
  const ikm = await subtle.importKey('raw', new Uint8Array(sharedBits), { name: 'HKDF' }, false, ['deriveKey']);
  return subtle.deriveKey(
    { name: 'HKDF', hash: 'SHA-256', salt: new Uint8Array(), info: utf8ToBytes(info) },
    ikm,
    { name: AES_ALGORITHM, length: 256 },
    false,
    ['encrypt', 'decrypt']
  );
}

export function generateCrewKeyBytes() {
  return getRandomValues(new Uint8Array(32));
}

export async function importCrewAesKey(rawKey) {
  return subtle.importKey('raw', rawKey, { name: AES_ALGORITHM }, false, ['encrypt', 'decrypt']);
}

export async function wrapCrewKeyForUser(crewKeyBytes, recipientPublicKeySpki, wrapperPrivateKey) {
  const recipientPublicKey = await importPublicKeySpki(recipientPublicKeySpki);
  const wrapKey = await deriveSharedAesKey(wrapperPrivateKey, recipientPublicKey, 'crew-key-wrap-v1');
  const wrapNonce = getRandomValues(new Uint8Array(12));
  const encrypted = await subtle.encrypt({ name: AES_ALGORITHM, iv: wrapNonce }, wrapKey, crewKeyBytes);
  return {
    wrappedCrewKey: bytesToBase64(new Uint8Array(encrypted)),
    wrapNonce: bytesToBase64(wrapNonce)
  };
}

export async function wrapFleetKeyForUser(fleetKeyBytes, recipientPublicKeySpki, wrapperPrivateKey) {
  const recipientPublicKey = await importPublicKeySpki(recipientPublicKeySpki);
  const wrapKey = await deriveSharedAesKey(wrapperPrivateKey, recipientPublicKey, 'fleet-key-wrap-v1');
  const wrapNonce = getRandomValues(new Uint8Array(12));
  const encrypted = await subtle.encrypt({ name: AES_ALGORITHM, iv: wrapNonce }, wrapKey, fleetKeyBytes);
  return {
    wrappedFleetKey: bytesToBase64(new Uint8Array(encrypted)),
    wrapNonce: bytesToBase64(wrapNonce)
  };
}

export async function unwrapCrewKey(wrappedCrewKey, wrapNonce, wrappedByPublicKeySpki, recipientPrivateKey) {
  const wrappedByPublicKey = await importPublicKeySpki(wrappedByPublicKeySpki);
  const wrapKey = await deriveSharedAesKey(recipientPrivateKey, wrappedByPublicKey, 'crew-key-wrap-v1');
  const decrypted = await subtle.decrypt(
    { name: AES_ALGORITHM, iv: base64ToBytes(wrapNonce) },
    wrapKey,
    base64ToBytes(wrappedCrewKey)
  );
  return new Uint8Array(decrypted);
}

export async function unwrapFleetKey(wrappedFleetKey, wrapNonce, wrappedByPublicKeySpki, recipientPrivateKey) {
  const wrappedByPublicKey = await importPublicKeySpki(wrappedByPublicKeySpki);
  const wrapKey = await deriveSharedAesKey(recipientPrivateKey, wrappedByPublicKey, 'fleet-key-wrap-v1');
  const decrypted = await subtle.decrypt(
    { name: AES_ALGORITHM, iv: base64ToBytes(wrapNonce) },
    wrapKey,
    base64ToBytes(wrappedFleetKey)
  );
  return new Uint8Array(decrypted);
}

export async function encryptJson(aesKey, payload) {
  const nonce = getRandomValues(new Uint8Array(12));
  const plaintext = utf8ToBytes(JSON.stringify(payload));
  const encrypted = await subtle.encrypt({ name: AES_ALGORITHM, iv: nonce }, aesKey, plaintext);
  return {
    nonce: bytesToBase64(nonce),
    ciphertext: bytesToBase64(new Uint8Array(encrypted))
  };
}
