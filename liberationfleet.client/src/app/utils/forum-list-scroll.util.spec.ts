import {
  clearAllForumListScrollState,
  clearForumListScrollState,
  readForumListScrollState,
  saveForumListScrollState
} from './forum-list-scroll.util';

describe('forum-list-scroll.util', () => {
  beforeEach(() => {
    clearAllForumListScrollState();
  });

  afterEach(() => {
    clearAllForumListScrollState();
  });

  it('clearAllForumListScrollState should remove crew and fleet restores', () => {
    saveForumListScrollState('crew', { scrollTop: 120, loadedCount: 20 });
    saveForumListScrollState('fleet', { scrollTop: 40, loadedCount: 10 });

    expect(readForumListScrollState('crew')).not.toBeNull();
    expect(readForumListScrollState('fleet')).not.toBeNull();

    clearAllForumListScrollState();

    expect(readForumListScrollState('crew')).toBeNull();
    expect(readForumListScrollState('fleet')).toBeNull();
  });

  it('clearForumListScrollState should remove a single key', () => {
    saveForumListScrollState('crew', { scrollTop: 1, loadedCount: 1 });
    clearForumListScrollState('crew');
    expect(readForumListScrollState('crew')).toBeNull();
  });
});
