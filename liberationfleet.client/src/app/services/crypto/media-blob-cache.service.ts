import { Injectable } from '@angular/core';

const DB_NAME = 'lf-decrypted-media-cache';
const DB_VERSION = 1;
const STORE_NAME = 'blobs';

/** Soft caps — LRU eviction when either is exceeded. */
const MAX_ENTRIES = 50;
const MAX_TOTAL_BYTES = 250 * 1024 * 1024;

interface MediaCacheRecord {
  key: string;
  blob: Blob;
  mime: string;
  size: number;
  lastAccessed: number;
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
 * Client-side cache of decrypted media Blobs (IndexedDB).
 * Server still only stores ciphertext; plaintext never leaves the device after decrypt.
 */
@Injectable({
  providedIn: 'root'
})
export class MediaBlobCacheService {
  private dbPromise: Promise<IDBDatabase> | null = null;

  async get(key: string): Promise<Blob | null> {
    try {
      const db = await this.openDb();
      const record = await this.idbRequest<MediaCacheRecord | undefined>(
        db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME).get(key)
      );
      if (!record?.blob) {
        return null;
      }

      // Touch LRU without blocking the caller on write failure.
      void this.touch(key, record);
      return record.blob;
    } catch {
      return null;
    }
  }

  async put(key: string, blob: Blob, mime?: string): Promise<void> {
    try {
      const db = await this.openDb();
      const record: MediaCacheRecord = {
        key,
        blob,
        mime: mime || blob.type || 'application/octet-stream',
        size: blob.size,
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
        if (!db.objectStoreNames.contains(STORE_NAME)) {
          db.createObjectStore(STORE_NAME, { keyPath: 'key' });
        }
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
