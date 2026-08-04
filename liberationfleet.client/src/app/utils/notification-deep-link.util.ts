import { ActivatedRoute, Router } from '@angular/router';

/** Query keys used in notification ActionUrls to target an on-page element. */
export const NOTIFICATION_HIGHLIGHT_QUERY_KEYS = [
  'highlightId',
  'messageId',
  'commentId'
] as const;

/**
 * Read a one-shot highlight target id from the current route query.
 * Prefer highlightId, then messageId, then commentId.
 */
export function readNotificationHighlightId(route: ActivatedRoute): number | null {
  const params = route.snapshot.queryParamMap;
  for (const key of NOTIFICATION_HIGHLIGHT_QUERY_KEYS) {
    const raw = params.get(key);
    if (!raw) {
      continue;
    }
    const id = Number(raw);
    if (Number.isFinite(id) && id > 0) {
      return id;
    }
  }
  return null;
}

/**
 * Strip highlight-related query params so a later visit without a notification
 * link does not re-highlight. Keeps highlight state in the component for this visit.
 */
export function clearNotificationHighlightParams(router: Router, route: ActivatedRoute): void {
  const params = route.snapshot.queryParamMap;
  const hasAny = NOTIFICATION_HIGHLIGHT_QUERY_KEYS.some(key => params.has(key));
  if (!hasAny) {
    return;
  }

  const queryParams: Record<string, null> = {};
  for (const key of NOTIFICATION_HIGHLIGHT_QUERY_KEYS) {
    queryParams[key] = null;
  }

  void router.navigate([], {
    relativeTo: route,
    queryParams,
    queryParamsHandling: 'merge',
    replaceUrl: true
  });
}

export function applyNotificationHighlight(element: HTMLElement | null | undefined): void {
  if (!element) {
    return;
  }
  element.classList.add('lf-notification-highlight');
  element.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

export function isNotificationHighlightTarget(
  highlightId: number | null | undefined,
  entityId: number | null | undefined
): boolean {
  return !!highlightId && !!entityId && highlightId === entityId;
}
