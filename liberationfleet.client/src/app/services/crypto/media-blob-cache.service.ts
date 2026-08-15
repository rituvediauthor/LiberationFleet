import { Injectable } from '@angular/core';
import { resolveBlobMime } from '../../utils/media-mime.util';

const DB_NAME = 'lf-decrypted-media-cache';
/** v2: store ArrayBuffer instead of Blob (iOS Safari PWA Blobs go stale after relaunch). */
const DB_VERSION = 2;
const STORE_NAME = 'blobs';

/** Soft caps — LRU eviction when either is exceeded. */
const MAX_ENTRIES = 50;
const MAX_TOTAL_BYTES = 250 * 1024 * 1024;

/**
 * Skip caching individual blobs larger than this.
 * Unencrypted videos can be hundreds of MB; copying them into IndexedDB
 * (via arrayBuffer) freezes playback and often OOMs mobile Safari.
 */
export const MAX_CACHEABLE_MEDIA_ENTRY_BYTES = 32 * 1024 * 1024;

export function shouldCacheMediaBlob(sizeBytes: number): boolean {
  return sizeBytes > 0 && sizeBytes <= MAX_CACHEABLE_MEDIA_ENTRY_BYTES;
}

interface MediaCacheRecord {
  key: string;
  /** Raw bytes — do not store Blob; iOS home-screen Safari returns empty Blobs after relaunch. */
  data: ArrayBuffer;
  mime: string;
  size: number;
  lastAccessed: number;
}

/** Legacy v1 shape (Blob in IDB) — treat as miss and delete. */
interface LegacyMediaCacheRecord {
  key: string;
  blob?: Blob;
  data?: ArrayBuffer;
  mime?: string;
  size?: number;
  lastAccessed?: number;
}

export interface MediaCacheScope {
  crewId?: number;
  fleetId?: number;
}

export function buildMediaCacheKey(
  scope: MediaCacheScope,
  resourceId: string,
  keyVersion: number
): string {
  const scopePart = scope.fleetId != null && scope.fleetId > 0
    ? `fleet:${scope.fleetId}`
    : `crew:${scope.crewId ?? 0}`;
  return `${scopePart}:${resourceId}:v${keyVersion}`;
}

/**
 * Client-side cache of decrypted media (IndexedDB).
 * Server still only stores ciphertext; plaintext never leaves the device after decrypt.
 */
@Injectable({
  providedIn: 'root'
})
export class MediaBlobCacheService {
  private dbPromise: Promise<IDBDatabase> | null = null;

  /** True when a usable ArrayBuffer entry exists (does not load bytes into a Blob). */
  async has(key: string): Promise<boolean> {
    try {
      const db = await this.openDb();
      const record = await this.idbRequest<LegacyMediaCacheRecord | undefined>(
        db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME).get(key)
      );
      return !!(record?.data instanceof ArrayBuffer && record.data.byteLength > 0);
    } catch {
      return false;
    }
  }

  async get(key: string): Promise<Blob | null> {
    try {
      const db = await this.openDb();
      const record = await this.idbRequest<LegacyMediaCacheRecord | undefined>(
        db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME).get(key)
      );
      if (!record) {
        return null;
      }

      // Drop legacy Blob records and empty/corrupt entries so decrypt can re-run.
      const data = record.data;
      if (!(data instanceof ArrayBuffer) || data.byteLength === 0) {
        void this.deleteKey(key);
        return null;
      }

      const mime = resolveBlobMime(record.mime, data);
      const blob = new Blob([data], { type: mime });
      if (blob.size === 0) {
        void this.deleteKey(key);
        return null;
      }

      void this.touch(key, {
        key,
        data,
        mime,
        size: record.size || data.byteLength,
        lastAccessed: Date.now()
      });
      return blob;
    } catch {
      return null;
    }
  }

  async put(key: string, blob: Blob, mime?: string): Promise<void> {
    try {
      if (!blob || !shouldCacheMediaBlob(blob.size)) {
        return;
      }

      const data = await blob.arrayBuffer();
      if (data.byteLength === 0) {
        return;
      }

      const db = await this.openDb();
      const record: MediaCacheRecord = {
        key,
        data,
        mime: resolveBlobMime(mime || blob.type, data),
        size: data.byteLength,
        lastAccessed: Date.now()
      };

      await this.idbRequest(
        db.transaction(STORE_NAME, 'readwrite').objectStore(STORE_NAME).put(record)
      );
      await this.evictIfNeeded();
    } catch {
      // Cache is best-effort.
    }
  }

  async clear(): Promise<void> {
    try {
      const db = await this.openDb();
      await this.idbRequest(
        db.transaction(STORE_NAME, 'readwrite').objectStore(STORE_NAME).clear()
      );
    } catch {
      // Ignore clear failures (private mode, etc.).
    }
  }

  private async deleteKey(key: string): Promise<void> {
    try {
      const db = await this.openDb();
      await this.idbRequest(
        db.transaction(STORE_NAME, 'readwrite').objectStore(STORE_NAME).delete(key)
      );
    } catch {
      // Ignore.
    }
  }

  private async touch(key: string, record: MediaCacheRecord): Promise<void> {
    try {
      const db = await this.openDb();
      const updated: MediaCacheRecord = { ...record, lastAccessed: Date.now() };
      await this.idbRequest(
        db.transaction(STORE_NAME, 'readwrite').objectStore(STORE_NAME).put(updated)
      );
    } catch {
      // Ignore.
    }
  }

  private async evictIfNeeded(): Promise<void> {
    const db = await this.openDb();
    const all = await this.idbRequest<MediaCacheRecord[]>(
      db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME).getAll()
    );
    if (!all?.length) {
      return;
    }

    let totalBytes = all.reduce((sum, r) => sum + (r.size || 0), 0);
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

    if (keysToRemove.length === 0) {
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
        // Wipe on upgrade so legacy Blob entries cannot poison cache hits on iOS.
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
