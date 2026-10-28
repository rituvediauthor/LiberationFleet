import { LibraryTaskListItem, LibraryUnitListItem } from '../models/library.model';
import { CustomGiftCategory } from '../models/gift.model';

export const DASHBOARD_PREVIEW_MAX_CHARS = 500;

/** Truncate dashboard preview text at maxLength (default 500). */
export function truncateDashboardPreview(
  text: string | null | undefined,
  maxLength = DASHBOARD_PREVIEW_MAX_CHARS
): string {
  const normalized = (text ?? '').replace(/\s+/g, ' ').trim();
  if (!normalized) {
    return '';
  }
  if (normalized.length <= maxLength) {
    return normalized;
  }
  return `${normalized.slice(0, Math.max(0, maxLength - 3)).trimEnd()}...`;
}

export function giftCategoryLabel(category: CustomGiftCategory | null | undefined): string {
  switch (category) {
    case 'cycle':
      return 'Cycle';
    case 'survivalThreshold':
      return 'Survival threshold';
    case 'emergency':
      return 'Emergency';
    case 'payback':
      return 'Pay-back';
    case 'representative':
      return 'Representative';
    case 'other':
      return 'Other';
    default:
      return '';
  }
}

export function formatDashboardTimestamp(date: Date | string | null | undefined): string {
  if (!date) {
    return '';
  }
  return new Date(date).toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  });
}

export function formatQuestDueLabel(task: LibraryTaskListItem): string {
  if (task.hasDeadline === false || !task.nextDueAt) {
    return task.hasDeadline === false ? 'When you can' : (task.scheduleSummary || '');
  }
  return `Due ${formatDashboardTimestamp(task.nextDueAt)}`;
}

export type LibraryPreviewCandidate =
  | { type: 'unit'; unit: LibraryUnitListItem }
  | { type: 'quest'; task: LibraryTaskListItem };

export const ENCRYPTED_QUEST_PLACEHOLDER_TITLE = 'Encrypted quest';

/** True when a decrypted title is safe to show on the dashboard carousel. */
export function hasUsableLibraryPreviewTitle(title: string | null | undefined): boolean {
  const normalized = (title ?? '').trim();
  return !!normalized && normalized !== ENCRYPTED_QUEST_PLACEHOLDER_TITLE;
}

/**
 * Keep plaintext items with a title, plus encrypted quests whose title is still
 * empty until the crew key decrypts them.
 */
export function isLibraryPreviewCandidateEligible(candidate: LibraryPreviewCandidate): boolean {
  if (candidate.type === 'unit') {
    return hasUsableLibraryPreviewTitle(candidate.unit.title);
  }
  return !!candidate.task.hasEncryptedContent || hasUsableLibraryPreviewTitle(candidate.task.title);
}

export function shuffleArray<T>(items: T[], random: () => number = Math.random): T[] {
  const pool = [...items];
  for (let i = pool.length - 1; i > 0; i -= 1) {
    const j = Math.floor(random() * (i + 1));
    [pool[i], pool[j]] = [pool[j]!, pool[i]!];
  }
  return pool;
}

/** Pick a random candidate from the pool, or null when empty. */
export function pickRandomLibraryPreview(
  candidates: LibraryPreviewCandidate[],
  random: () => number = Math.random
): LibraryPreviewCandidate | null {
  if (!candidates.length) {
    return null;
  }
  const index = Math.floor(random() * candidates.length);
  return candidates[Math.min(index, candidates.length - 1)] ?? null;
}

/** Pick up to maxItems distinct candidates from the pool in randomized order. */
export function pickLibraryPreviewCarousel(
  candidates: LibraryPreviewCandidate[],
  maxItems: number,
  random: () => number = Math.random
): LibraryPreviewCandidate[] {
  if (!candidates.length || maxItems <= 0) {
    return [];
  }
  return shuffleArray(candidates, random).slice(0, maxItems);
}

/**
 * Prefer the first item that should be shown under the adult preference.
 * Returns null when every item is blocked.
 */
export function pickFirstAllowedAdult<T>(
  items: T[],
  isAdult: (item: T) => boolean | undefined,
  shouldShow: (isAdultContent: boolean | undefined) => boolean
): T | null {
  for (const item of items) {
    if (shouldShow(isAdult(item))) {
      return item;
    }
  }
  return null;
}
