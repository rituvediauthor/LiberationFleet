import { foregroundNotificationId, shouldPresentForegroundPush } from './foreground-push.util';

describe('foreground push', () => {
  it('shows a DM while the user is in a different conversation', () => {
    expect(shouldPresentForegroundPush(
      '/app/friends/messages/3',
      '/app/friends/messages/12'
    )).toBeTrue();
  });

  it('skips the shade when that conversation is already open', () => {
    expect(shouldPresentForegroundPush(
      '/app/crew/chats/5',
      '/app/crew/chats/5?messageId=9'
    )).toBeFalse();
    expect(shouldPresentForegroundPush(
      '/app/friends/messages/12',
      '/app/friends/messages/12'
    )).toBeFalse();
  });

  it('shows alerts that are not for the current page', () => {
    expect(shouldPresentForegroundPush('/app/crew/chats/5', '/app/friends/messages/12')).toBeTrue();
    expect(shouldPresentForegroundPush('/app/friends', null)).toBeTrue();
  });

  it('uses the server notification id when it fits an Android int', () => {
    expect(foregroundNotificationId('42')).toBe(42);
    expect(foregroundNotificationId('0')).toBeGreaterThan(0);
    expect(foregroundNotificationId(undefined)).toBeGreaterThan(0);
  });
});
