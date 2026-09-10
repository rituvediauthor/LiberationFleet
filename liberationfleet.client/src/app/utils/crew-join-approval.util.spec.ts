import {
  isCrewJoinRequestApprovedNotification,
  isNewSeasonNotification,
  JOIN_REQUEST_APPROVED_TITLE
} from './crew-join-approval.util';
import { NotificationItem } from '../models/notification.model';

function notification(partial: Partial<NotificationItem>): NotificationItem {
  return {
    id: 1,
    kind: 'ProposalAccepted',
    title: JOIN_REQUEST_APPROVED_TITLE,
    body: 'You were approved to join Test Crew.',
    actionUrl: '/app/crew',
    isRead: false,
    createdAt: new Date().toISOString(),
    ...partial
  };
}

describe('crew-join-approval.util', () => {
  it('detects join-request approved notifications', () => {
    expect(isCrewJoinRequestApprovedNotification(notification({}))).toBe(true);
    expect(isCrewJoinRequestApprovedNotification(notification({
      title: 'Proposal accepted',
      actionUrl: '/app/crew',
      body: 'You were approved to join Alpha.'
    }))).toBe(true);
    expect(isCrewJoinRequestApprovedNotification(notification({
      title: 'Proposal accepted',
      actionUrl: '/app/crew/proposals/list/approved',
      body: 'Your crew proposal was approved.'
    }))).toBe(false);
    expect(isCrewJoinRequestApprovedNotification(notification({
      kind: 'NewProposal'
    }))).toBe(false);
    // SignalR historically sent Kind as a number (ProposalAccepted = 3).
    expect(isCrewJoinRequestApprovedNotification(notification({
      kind: 3 as unknown as NotificationItem['kind'],
      title: JOIN_REQUEST_APPROVED_TITLE
    }))).toBe(true);
  });

  it('detects new season notifications', () => {
    expect(isNewSeasonNotification(notification({
      kind: 'NewSeason',
      title: 'New season',
      body: 'Season started',
      actionUrl: '/app/crew/gift-log'
    }))).toBe(true);
    expect(isNewSeasonNotification(notification({}))).toBe(false);
  });
});
