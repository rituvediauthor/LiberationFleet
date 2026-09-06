import {
  isCrewDashboardRedirectUrl,
  isCrewJoinRequestApprovedNotification,
  JOIN_REQUEST_APPROVED_TITLE
} from './crew-join-approval.util';
import { NotificationItem } from '../models/notification.model';

function notification(partial: Partial<NotificationItem>): NotificationItem {
  return {
    id: 1,
    kind: 'ProposalAccepted',
    title: JOIN_REQUEST_APPROVED_TITLE,
    body: '',
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
      title: 'Proposal accepted'
    }))).toBe(false);
    expect(isCrewJoinRequestApprovedNotification(notification({
      kind: 'NewProposal'
    }))).toBe(false);
  });

  it('recognizes crew dashboard redirect URLs', () => {
    expect(isCrewDashboardRedirectUrl('/app/crew')).toBe(true);
    expect(isCrewDashboardRedirectUrl('/app/crew?x=1')).toBe(true);
    expect(isCrewDashboardRedirectUrl('/app/crew/join')).toBe(true);
    expect(isCrewDashboardRedirectUrl('/app/crew/join-requests')).toBe(true);
    expect(isCrewDashboardRedirectUrl('/app/crew/create')).toBe(true);
    expect(isCrewDashboardRedirectUrl('/app/crew/invitations')).toBe(true);
    expect(isCrewDashboardRedirectUrl('/app/fleet')).toBe(false);
    expect(isCrewDashboardRedirectUrl('/app/crew/crewmates')).toBe(false);
  });
});
