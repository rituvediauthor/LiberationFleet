import { Injectable } from '@angular/core';

export enum StorageScope {
  Persistent = 'persistent',
  Session = 'session'
}

/**
 * Web storage abstraction. Native builds can later swap sensitive session values
 * to Capacitor Secure Storage / Keychain without touching feature code.
 */
@Injectable({
  providedIn: 'root'
})
export class AppStorageService {
  get(scope: StorageScope, key: string): string | null {
    const storage = this.resolveStorage(scope);
    if (!storage) {
      return null;
    }

    try {
      return storage.getItem(key);
    } catch {
      return null;
    }
  }

  set(scope: StorageScope, key: string, value: string): void {
    const storage = this.resolveStorage(scope);
    if (!storage) {
      return;
    }

    try {
      storage.setItem(key, value);
    } catch {
      // Ignore quota / private-mode failures.
    }
  }

  remove(scope: StorageScope, key: string): void {
    const storage = this.resolveStorage(scope);
    if (!storage) {
      return;
    }

    try {
      storage.removeItem(key);
    } catch {
      // Ignore storage failures.
    }
  }

  private resolveStorage(scope: StorageScope): Storage | null {
    if (typeof window === 'undefined') {
      return null;
    }

    try {
      return scope === StorageScope.Persistent ? window.localStorage : window.sessionStorage;
    } catch {
      return null;
    }
  }
}
