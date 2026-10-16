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
  return `${normalized.slice(0, maxLength - 1).trimEnd()}…`;
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
