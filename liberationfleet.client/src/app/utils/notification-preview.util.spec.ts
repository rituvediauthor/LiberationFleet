import { truncateNotificationPreview } from './notification-preview.util';

describe('truncateNotificationPreview', () => {
  it('keeps short text unchanged', () => {
    expect(truncateNotificationPreview('hello world')).toBe('hello world');
  });

  it('ends truncated previews with ...', () => {
    const truncated = truncateNotificationPreview('a'.repeat(220));
    expect(truncated.endsWith('...')).toBeTrue();
    expect(truncated.length).toBe(200);
    expect(truncated.slice(0, 197)).toBe('a'.repeat(197));
  });
});
