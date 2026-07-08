import { Injectable } from '@angular/core';
import { AppStorageService, StorageScope } from './storage/app-storage.service';
import {
  PERSISTENT_RECOVERY_PHRASES_STORAGE_KEY,
  SAVE_DECRYPTION_KEY_STORAGE_KEY
} from './storage/storage-keys';

type RecoveryPhraseMap = Record<string, string>;

@Injectable({
  providedIn: 'root'
})
export class SavedRecoveryPhraseService {
  constructor(private storage: AppStorageService) {}

  isSaveEnabled(): boolean {
    return this.storage.get(StorageScope.Persistent, SAVE_DECRYPTION_KEY_STORAGE_KEY) === 'true';
  }

  setSaveEnabled(enabled: boolean): void {
    if (enabled) {
      this.storage.set(StorageScope.Persistent, SAVE_DECRYPTION_KEY_STORAGE_KEY, 'true');
      return;
    }

    this.storage.remove(StorageScope.Persistent, SAVE_DECRYPTION_KEY_STORAGE_KEY);
    this.storage.remove(StorageScope.Persistent, PERSISTENT_RECOVERY_PHRASES_STORAGE_KEY);
  }

  getPhrase(userId: number): string | null {
    const map = this.readMap();
    return map[String(userId)] ?? null;
  }

  savePhrase(userId: number, phrase: string): void {
    const map = this.readMap();
    map[String(userId)] = phrase;
    this.writeMap(map);
  }

  removePhrase(userId: number): void {
    const map = this.readMap();
    delete map[String(userId)];
    this.writeMap(map);
  }

  private readMap(): RecoveryPhraseMap {
    const raw = this.storage.get(StorageScope.Persistent, PERSISTENT_RECOVERY_PHRASES_STORAGE_KEY);
    if (!raw) {
      return {};
    }

    try {
      const parsed = JSON.parse(raw) as RecoveryPhraseMap;
      return parsed && typeof parsed === 'object' ? parsed : {};
    } catch {
      return {};
    }
  }

  private writeMap(map: RecoveryPhraseMap): void {
    this.storage.set(StorageScope.Persistent, PERSISTENT_RECOVERY_PHRASES_STORAGE_KEY, JSON.stringify(map));
  }
}
