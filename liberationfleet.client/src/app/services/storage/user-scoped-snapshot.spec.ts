import { AppStorageService, StorageScope } from './app-storage.service';
import { AUTH_TOKEN_STORAGE_KEY } from './storage-keys';
import {
  clearUserScopedJson,
  readUserScopedJson,
  writeUserScopedJson
} from './user-scoped-snapshot';

function testJwt(userId: number): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const payload = btoa(JSON.stringify({
    sub: String(userId),
    exp: Math.floor(Date.now() / 1000) + 3600
  }));
  return `${header}.${payload}.sig`;
}

describe('user-scoped-snapshot', () => {
  const storage = new AppStorageService();
  const key = 'lf-test-snapshot';

  afterEach(() => {
    storage.remove(StorageScope.Persistent, key);
    storage.remove(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY);
  });

  it('round-trips a value for the signed-in user', () => {
    storage.set(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY, testJwt(9));
    writeUserScopedJson(storage, key, { crewName: 'Alpha' });
    expect(readUserScopedJson<{ crewName: string }>(storage, key)).toEqual({ crewName: 'Alpha' });
  });

  it('does not return another user\'s snapshot', () => {
    storage.set(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY, testJwt(1));
    writeUserScopedJson(storage, key, { crewName: 'Alpha' });
    storage.set(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY, testJwt(2));
    expect(readUserScopedJson(storage, key)).toBeNull();
  });

  it('clears the snapshot on logout', () => {
    storage.set(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY, testJwt(1));
    writeUserScopedJson(storage, key, { crewName: 'Alpha' });
    clearUserScopedJson(storage, key);
    expect(readUserScopedJson(storage, key)).toBeNull();
  });
});
