/** Merge reply lists by id, keeping later entries on conflict. */
export function mergeCommentRepliesById<T extends { id: number }>(
  ...groups: Array<T[] | null | undefined>
): T[] {
  const byId = new Map<number, T>();
  for (const group of groups) {
    for (const reply of group ?? []) {
      byId.set(reply.id, reply);
    }
  }
  return Array.from(byId.values());
}

/** True when local replies cannot yet represent replyCount (e.g. never expanded). */
export function commentRepliesNeedLoad(
  replyCount: number,
  replies: { length: number } | null | undefined
): boolean {
  return (replies?.length ?? 0) < replyCount;
}
