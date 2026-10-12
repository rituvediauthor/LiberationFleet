export interface ForumListScrollState {
  scrollTop: number;
  loadedCount: number;
}

const PREFIX = 'lf.forum-list-scroll:';

export function saveForumListScrollState(key: string, state: ForumListScrollState): void {
  try {
    sessionStorage.setItem(PREFIX + key, JSON.stringify(state));
  } catch {
    // ignore quota / private mode
  }
}

export function readForumListScrollState(key: string): ForumListScrollState | null {
  try {
    const raw = sessionStorage.getItem(PREFIX + key);
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw) as ForumListScrollState;
    if (typeof parsed?.scrollTop !== 'number' || typeof parsed?.loadedCount !== 'number') {
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}

export function clearForumListScrollState(key: string): void {
  try {
    sessionStorage.removeItem(PREFIX + key);
  } catch {
    // ignore
  }
}

/** Clear all forum list scroll restores (logout / account switch). */
export function clearAllForumListScrollState(): void {
  try {
    const keys: string[] = [];
    for (let i = 0; i < sessionStorage.length; i++) {
      const key = sessionStorage.key(i);
      if (key?.startsWith(PREFIX)) {
        keys.push(key);
      }
    }
    for (const key of keys) {
      sessionStorage.removeItem(key);
    }
  } catch {
    // ignore
  }
}
