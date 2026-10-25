import { GiftLogEntry } from '../models/gift.model';
import { mergeGiftLogEntryPreserveEngagement } from './gift-log-engagement.util';

function entry(overrides: Partial<GiftLogEntry> = {}): GiftLogEntry {
  return {
    id: 7,
    type: 'direct',
    giverId: 1,
    giverName: 'Ada',
    recipientId: 2,
    recipientName: 'Bob',
    amount: 20,
    platform: 'PayPal',
    timestamp: new Date('2026-10-01T12:00:00Z'),
    message: 'Ada gave $20 to Bob via PayPal',
    relatedUserIds: [1, 2],
    likeCount: 4,
    likedByCurrentUser: true,
    commentCount: 2,
    ...overrides
  };
}

describe('mergeGiftLogEntryPreserveEngagement', () => {
  it('keeps existing likes and comments when the incoming entry defaults them', () => {
    const merged = mergeGiftLogEntryPreserveEngagement(
      entry(),
      entry({
        message: 'Ada gave $20 to Bob via PayPal (Completed)',
        status: 'completed',
        likeCount: 0,
        likedByCurrentUser: false,
        commentCount: 0
      })
    );

    expect(merged.message).toContain('Completed');
    expect(merged.status).toBe('completed');
    expect(merged.likeCount).toBe(4);
    expect(merged.likedByCurrentUser).toBeTrue();
    expect(merged.commentCount).toBe(2);
  });
});
