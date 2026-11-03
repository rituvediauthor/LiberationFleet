/** True when an Android foreground push should be posted to the shade. */
export function shouldPresentForegroundPush(currentUrl: string, actionUrl: string | null | undefined): boolean {
  const target = pathOnly(actionUrl ?? '');
  if (!target) {
    return true;
  }

  return pathOnly(currentUrl) !== target;
}

export function foregroundNotificationId(raw: string | undefined): number {
  const parsed = Number(raw);
  if (Number.isInteger(parsed) && parsed > 0 && parsed <= 2147483647) {
    return parsed;
  }

  const fallback = Math.floor(Date.now() % 2147483647);
  return fallback > 0 ? fallback : 1;
}

function pathOnly(url: string): string {
  const withoutHash = url.split('#')[0] ?? '';
  const withoutQuery = withoutHash.split('?')[0] ?? '';
  if (withoutQuery.length > 1 && withoutQuery.endsWith('/')) {
    return withoutQuery.slice(0, -1);
  }
  return withoutQuery;
}
