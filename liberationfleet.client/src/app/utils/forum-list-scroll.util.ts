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
