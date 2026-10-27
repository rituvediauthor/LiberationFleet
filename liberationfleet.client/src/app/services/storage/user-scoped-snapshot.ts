import { getUserIdFromToken } from '../../utils/jwt.util';
import { AppStorageService, StorageScope } from './app-storage.service';
import { AUTH_TOKEN_STORAGE_KEY } from './storage-keys';

interface UserScopedRecord<T> {
  userId: number;
  value: T;
}

export function readStoredUserId(storage: AppStorageService): number | null {
  const token = storage.get(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY)
    ?? storage.get(StorageScope.Session, AUTH_TOKEN_STORAGE_KEY);
  return token ? getUserIdFromToken(token) : null;
}

export function readUserScopedJson<T>(storage: AppStorageService, key: string): T | null {
  const userId = readStoredUserId(storage);
  if (userId == null) {
    return null;
  }

  const raw = storage.get(StorageScope.Persistent, key);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as UserScopedRecord<T>;
    if (parsed.userId !== userId || parsed.value === undefined) {
      return null;
    }
    return parsed.value;
  } catch {
    return null;
  }
}

export function writeUserScopedJson<T>(storage: AppStorageService, key: string, value: T): void {
  const userId = readStoredUserId(storage);
  if (userId == null) {
    return;
  }

  const payload: UserScopedRecord<T> = { userId, value };
  storage.set(StorageScope.Persistent, key, JSON.stringify(payload));
}

export function clearUserScopedJson(storage: AppStorageService, key: string): void {
  storage.remove(StorageScope.Persistent, key);
}
