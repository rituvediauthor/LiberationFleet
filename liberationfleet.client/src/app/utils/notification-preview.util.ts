/** Truncate plaintext for notification previews (server also caps at 200). */
export function truncateNotificationPreview(text: string | null | undefined, maxLength = 200): string {
  const normalized = (text ?? '').replace(/\s+/g, ' ').trim();
  if (!normalized) {
    return '';
  }
  if (normalized.length <= maxLength) {
    return normalized;
  }
  return `${normalized.slice(0, maxLength - 1).trimEnd()}…`;
}
