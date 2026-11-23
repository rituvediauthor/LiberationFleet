import { commentRepliesNeedLoad, mergeCommentRepliesById } from './comment-replies.util';

describe('comment-replies.util', () => {
  it('detects when loaded replies are short of replyCount', () => {
    expect(commentRepliesNeedLoad(2, undefined)).toBeTrue();
    expect(commentRepliesNeedLoad(2, [])).toBeTrue();
    expect(commentRepliesNeedLoad(2, [{ id: 1 }])).toBeTrue();
    expect(commentRepliesNeedLoad(2, [{ id: 1 }, { id: 2 }])).toBeFalse();
    expect(commentRepliesNeedLoad(0, undefined)).toBeFalse();
  });

  it('merges reply lists by id preferring later entries', () => {
    const merged = mergeCommentRepliesById(
      [{ id: 1, body: 'a' }, { id: 2, body: 'b' }],
      [{ id: 2, body: 'b2' }, { id: 3, body: 'c' }]
    );

    expect(merged).toEqual([
      { id: 1, body: 'a' },
      { id: 2, body: 'b2' },
      { id: 3, body: 'c' }
    ]);
  });
});
