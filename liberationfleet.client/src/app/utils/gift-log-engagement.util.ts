import { GiftLogEntry } from '../models/gift.model';

/** Keep like/comment fields when a verify or decrypt payload omits them (defaults to 0). */
export function mergeGiftLogEntryPreserveEngagement<T extends GiftLogEntry>(
  existing: T,
  incoming: GiftLogEntry
): T {
  return {
    ...existing,
    ...incoming,
    timestamp: incoming.timestamp instanceof Date ? incoming.timestamp : new Date(incoming.timestamp),
    likeCount: existing.likeCount,
    likedByCurrentUser: existing.likedByCurrentUser,
    commentCount: existing.commentCount
  };
}
