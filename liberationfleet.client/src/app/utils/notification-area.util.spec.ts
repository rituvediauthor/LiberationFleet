import { NotificationItem } from '../models/notification.model';
import { resolveNotificationArea } from './notification-area.util';

describe('resolveNotificationArea', () => {
  function item(partial: Partial<NotificationItem>): NotificationItem {
    return {
      id: 1,
      kind: 'NewChatMessage',
      title: 't',
      body: 'b',
      actionUrl: '/app/crew',
      isRead: false,
      createdAt: new Date().toISOString(),
      ...partial
    };
  }

  it('maps crew invitations to userInvitations', () => {
    expect(
      resolveNotificationArea(
        item({
          kind: 'JoinRequestFromCrew',
          actionUrl: '/app/crew/invitations/12'
        })
      )
    ).toBe('userInvitations');
  });

  it('maps proposal replies to proposal areas', () => {
    expect(
      resolveNotificationArea(
        item({
          kind: 'NewProposalReply',
          actionUrl: '/app/crew/proposals/3?commentId=9'
        })
      )
    ).toBe('crewProposals');

    expect(
      resolveNotificationArea(
        item({
          kind: 'NewFleetProposalReply',
          actionUrl: '/app/fleet/proposals/4?commentId=1'
        })
      )
    ).toBe('fleetProposals');
  });

  it('keeps forum replies on forum areas', () => {
    expect(
      resolveNotificationArea(
        item({
          kind: 'NewReply',
          actionUrl: '/app/crew/forums/8?commentId=2'
        })
      )
    ).toBe('crewForums');
  });

  it('maps emergency requests to crewEmergency', () => {
    expect(
      resolveNotificationArea(
        item({
          kind: 'NewEmergencyRequest',
          actionUrl: '/app/crew/emergency-requests/3?highlightId=3'
        })
      )
    ).toBe('crewEmergency');
  });
});
