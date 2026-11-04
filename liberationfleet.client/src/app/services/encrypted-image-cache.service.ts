import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ProposalCryptoService, ProposalCryptoScope } from './crypto/proposal-crypto.service';
import { CryptoSessionService } from './crypto/crypto-session.service';
import { CryptoApiService } from './crypto/crypto-api.service';
import { EncryptedContentType } from '../models/crypto.model';
import { resolveBlobMime } from '../utils/media-mime.util';

type AvatarContentType = Extract<EncryptedContentType, 'ProfileAvatar' | 'ImageAsset'>;

const DB_NAME = 'lf-encrypted-image-cache';
/** v2: persist keyVersion + nonce so unchanged media skips download/decrypt. */
const DB_VERSION = 2;
const STORE_NAME = 'images';
const MAX_ENTRIES = 200;
const MAX_TOTAL_BYTES = 40 * 1024 * 1024;
const MAX_ENTRY_BYTES = 4 * 1024 * 1024;

interface ImageCacheRecord {
  key: string;
  data: ArrayBuffer;
  mime: string;
  size: number;
  lastAccessed: number;
  keyVersion?: number;
  nonce?: string;
}

@Injectable({ providedIn: 'root' })
export class EncryptedImageCacheService {
  private readonly proposalCrypto = inject(ProposalCryptoService);
  private readonly cryptoSession = inject(CryptoSessionService);
  private readonly cryptoApi = inject(CryptoApiService);
  private readonly cache = new Map<string, Promise<string | null>>();
  private dbPromise: Promise<IDBDatabase> | null = null;
  private persistReady: Promise<void> = Promise.resolve();

  getDataUrl(
    scope: ProposalCryptoScope,
    resourceId: string | null | undefined,
    contentType: AvatarContentType = 'ProfileAvatar'
  ): Promise<string | null> {
    const id = resourceId?.trim();
    if (!id) {
      return Promise.resolve(null);
    }

    // Never surface decrypted images until the 12-word session is unlocked.
    if (!this.cryptoSession.isUnlocked()) {
      return Promise.resolve(null);
    }

    const key = this.cacheKey(scope, id, contentType);
    const cached = this.cache.get(key);
    if (cached) {
      return cached;
    }

    const pending = this.resolveDataUrl(scope, id, contentType, key);
    this.cache.set(key, pending);
    void pending.then(result => {
      if (result == null) {
        this.cache.delete(key);
      }
    });
    return pending;
  }

  /** Drop a cached image after the user uploads a replacement. */
  invalidate(resourceId: string | null | undefined, contentType: AvatarContentType = 'ProfileAvatar'): void {
    const id = resourceId?.trim();
    if (!id) {
      return;
    }
    for (const key of [...this.cache.keys()]) {
      if (this.matchesResource(key, id, contentType)) {
        this.cache.delete(key);
      }
    }
    this.persistReady = this.persistReady.then(() => this.deletePersistedMatching(id, contentType));
  }

  clear(): void {
    this.cache.clear();
    this.persistReady = this.persistReady.then(() => this.clearPersisted());
  }

  private cacheKey(scope: ProposalCryptoScope, id: string, contentType: AvatarContentType): string {
    return `${contentType}|${scope.crewId ?? ''}|${scope.fleetId ?? ''}|${id}`;
  }

  private matchesResource(key: string, id: string, contentType: AvatarContentType): boolean {
    return key.startsWith(`${contentType}|`) && key.endsWith(`|${id}`);
  }

  private async resolveDataUrl(
    scope: ProposalCryptoScope,
    id: string,
    contentType: AvatarContentType,
    key: string
  ): Promise<string | null> {
    if (!this.cryptoSession.isUnlocked()) {
      return null;
    }

    const persisted = await this.readPersisted(key);
    if (persisted) {
      const fingerprint = await this.probeFingerprint(scope, id, contentType);
      if (
        fingerprint
        && persisted.record.keyVersion === fingerprint.keyVersion
        && persisted.record.nonce === fingerprint.nonce
      ) {
        void this.touch(persisted.record);
        return URL.createObjectURL(persisted.blob);
      }
      if (!fingerprint) {
        // Offline / meta unavailable: reuse last good bytes rather than blocking UI.
        void this.touch(persisted.record);
        return URL.createObjectURL(persisted.blob);
      }
      // Envelope changed — drop stale plaintext and re-decrypt.
      void this.deletePersistedKey(key);
    }

    const url = await this.proposalCrypto.decryptImageDataUrl(scope, id, contentType).catch(() => null);
    if (!url) {
      return null;
    }

    const fingerprint = await this.probeFingerprint(scope, id, contentType);
    await this.persistFromObjectUrl(key, url, fingerprint);
    return url;
  }

  private async probeFingerprint(
    scope: ProposalCryptoScope,
    resourceId: string,
    contentType: AvatarContentType
  ): Promise<{ keyVersion: number; nonce: string } | null> {
    const crewId = scope.crewId && scope.crewId > 0 ? scope.crewId : undefined;
    const fleetId = scope.fleetId && scope.fleetId > 0 ? scope.fleetId : undefined;
    try {
      const meta = await firstValueFrom(
        this.cryptoApi.getEncryptedContentMeta(contentType, resourceId, crewId, fleetId)
      );
      return { keyVersion: meta.keyVersion, nonce: meta.nonce };
    } catch {
      if (contentType === 'ProfileAvatar' && (crewId || fleetId)) {
        try {
          const personal = await firstValueFrom(
            this.cryptoApi.getEncryptedContentMeta(contentType, resourceId)
          );
          return { keyVersion: personal.keyVersion, nonce: personal.nonce };
        } catch {
          return null;
        }
      }
      return null;
    }
  }

  private async persistFromObjectUrl(
    key: string,
    url: string,
    fingerprint: { keyVersion: number; nonce: string } | null
  ): Promise<void> {
    try {
      const blob = await fetch(url).then(response => response.blob());
      if (!blob || blob.size <= 0 || blob.size > MAX_ENTRY_BYTES) {
        return;
      }
      const data = await blob.arrayBuffer();
      if (data.byteLength === 0) {
        return;
      }
      await this.writePersisted({
        key,
        data,
        mime: resolveBlobMime(blob.type, data),
        size: data.byteLength,
        lastAccessed: Date.now(),
        keyVersion: fingerprint?.keyVersion,
        nonce: fingerprint?.nonce
      });
    } catch {
      // Persistence is best-effort.
    }
  }

  private async readPersisted(
    key: string
  ): Promise<{ blob: Blob; record: ImageCacheRecord } | null> {
    try {
      await this.persistReady;
      const db = await this.openDb();
      const record = await this.idbRequest<ImageCacheRecord | undefined>(
        db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME).get(key)
      );
      if (!(record?.data instanceof ArrayBuffer) || record.data.byteLength === 0) {
        return null;
      }
      const mime = resolveBlobMime(record.mime, record.data);
      const blob = new Blob([record.data], { type: mime });
      return blob.size > 0 ? { blob, record } : null;
    } catch {
      return null;
    }
  }

  private async writePersisted(record: ImageCacheRecord): Promise<void> {
    await this.persistReady;
    const db = await this.openDb();
    await this.idbRequest(
      db.transaction(STORE_NAME, 'readwrite').objectStore(STORE_NAME).put(record)
    );
    await this.evictIfNeeded();
  }

  private async touch(record: ImageCacheRecord): Promise<void> {
    try {
      await this.persistReady;
      const db = await this.openDb();
      await this.idbRequest(
        db.transaction(STORE_NAME, 'readwrite').objectStore(STORE_NAME).put({
          ...record,
          lastAccessed: Date.now()
        })
      );
    } catch {
      // Ignore.
    }
  }

  private async deletePersistedKey(key: string): Promise<void> {
    try {
      const db = await this.openDb();
      await this.idbRequest(
        db.transaction(STORE_NAME, 'readwrite').objectStore(STORE_NAME).delete(key)
      );
    } catch {
      // Ignore.
    }
  }

  private async deletePersistedMatching(id: string, contentType: AvatarContentType): Promise<void> {
    try {
      const db = await this.openDb();
      const all = await this.idbRequest<ImageCacheRecord[]>(
        db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME).getAll()
      );
      const keys = (all ?? [])
        .map(record => record.key)
        .filter(key => this.matchesResource(key, id, contentType));
      if (!keys.length) {
        return;
      }
      const tx = db.transaction(STORE_NAME, 'readwrite');
      const store = tx.objectStore(STORE_NAME);
      for (const key of keys) {
        store.delete(key);
      }
      await this.idbTransactionDone(tx);
    } catch {
      // Ignore.
    }
  }

  private async clearPersisted(): Promise<void> {
    try {
      const db = await this.openDb();
      await this.idbRequest(
        db.transaction(STORE_NAME, 'readwrite').objectStore(STORE_NAME).clear()
      );
    } catch {
      // Ignore private-mode / IDB failures.
    }
  }

  private async evictIfNeeded(): Promise<void> {
    const db = await this.openDb();
    const all = await this.idbRequest<ImageCacheRecord[]>(
      db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME).getAll()
    );
    if (!all?.length) {
      return;
    }

    let totalBytes = all.reduce((sum, record) => sum + (record.size || 0), 0);
    if (all.length <= MAX_ENTRIES && totalBytes <= MAX_TOTAL_BYTES) {
      return;
    }

    const remaining = [...all].sort((a, b) => a.lastAccessed - b.lastAccessed);
    const keysToRemove: string[] = [];
    let entryCount = remaining.length;
    while (entryCount > MAX_ENTRIES || totalBytes > MAX_TOTAL_BYTES) {
      const victim = remaining.shift();
      if (!victim) {
        break;
      }
      keysToRemove.push(victim.key);
      totalBytes -= victim.size || 0;
      entryCount -= 1;
    }
    if (!keysToRemove.length) {
      return;
    }

    const tx = db.transaction(STORE_NAME, 'readwrite');
    const store = tx.objectStore(STORE_NAME);
    for (const key of keysToRemove) {
      store.delete(key);
    }
    await this.idbTransactionDone(tx);
  }

  private openDb(): Promise<IDBDatabase> {
    if (this.dbPromise) {
      return this.dbPromise;
    }

    this.dbPromise = new Promise((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, DB_VERSION);
      request.onerror = () => reject(request.error ?? new Error('IndexedDB open failed'));
      request.onupgradeneeded = () => {
        const db = request.result;
        // Wipe on upgrade so legacy records without fingerprints cannot serve stale media.
        if (db.objectStoreNames.contains(STORE_NAME)) {
          db.deleteObjectStore(STORE_NAME);
        }
        db.createObjectStore(STORE_NAME, { keyPath: 'key' });
      };
      request.onsuccess = () => resolve(request.result);
    });

    return this.dbPromise;
  }

  private idbRequest<T>(request: IDBRequest<T>): Promise<T> {
    return new Promise((resolve, reject) => {
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error ?? new Error('IndexedDB request failed'));
    });
  }

  private idbTransactionDone(tx: IDBTransaction): Promise<void> {
    return new Promise((resolve, reject) => {
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error ?? new Error('IndexedDB transaction failed'));
      tx.onabort = () => reject(tx.error ?? new Error('IndexedDB transaction aborted'));
    });
  }
}
