import {
  collectMentionedUserIds,
  findActiveMentionQuery,
  insertMention,
  parseMentionSegments
} from './mention.util';

describe('mention.util', () => {
  it('findActiveMentionQuery reads the token before the cursor', () => {
    expect(findActiveMentionQuery('hi @ja', 6)).toBe('ja');
    expect(findActiveMentionQuery('hi there', 8)).toBeNull();
  });

  it('insertMention replaces the active query with a username', () => {
    const result = insertMention('hello @ja', 9, 'James');
    expect(result.text).toBe('hello @James ');
    expect(result.cursorIndex).toBe('hello @James '.length);
  });

  it('collectMentionedUserIds maps usernames case-insensitively', () => {
    const map = new Map([['james', 1], ['ritu', 2]]);
    expect(collectMentionedUserIds('ping @James and @missing', map)).toEqual([1]);
  });

  it('parseMentionSegments marks known mentions in display mode', () => {
    const segments = parseMentionSegments('hi @James!', 'display', new Set(['james']));
    expect(segments).toEqual([
      { type: 'text', value: 'hi ' },
      { type: 'mention', value: 'James' },
      { type: 'text', value: '!' }
    ]);
  });
});
