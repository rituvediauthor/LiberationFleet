import { shouldOmitNotificationActor, stripAnonymousNotificationActor } from './notification-actor-privacy.util';

describe('notification-actor-privacy.util', () => {
  it('omits actor identity for proposal notifications', () => {
    expect(shouldOmitNotificationActor({
      kind: 'NewProposal',
      actionUrl: '/app/crew/proposals/list/pending?highlightId=1',
      title: 'Anonymous submitted a crew proposal'
    })).toBeTrue();
  });

  it('omits actor identity for anonymous chat notifications', () => {
    expect(shouldOmitNotificationActor({
      kind: 'NewChatMessage',
      actionUrl: '/app/crew/chats/5?messageId=9',
      title: 'Anonymous posted in General'
    })).toBeTrue();
  });

  it('keeps actor identity for named chat notifications', () => {
    expect(shouldOmitNotificationActor({
      kind: 'NewChatMessage',
      actionUrl: '/app/crew/chats/5?messageId=9',
      title: 'Ada posted in General'
    })).toBeFalse();
  });

  it('omits actor identity for proposal mention notifications', () => {
    expect(shouldOmitNotificationActor({
      kind: 'Mention',
      actionUrl: '/app/crew/proposals/4?commentId=2',
      title: 'River mentioned you'
    })).toBeTrue();
  });

  it('strips actor fields when identity should be omitted', () => {
    const stripped = stripAnonymousNotificationActor({
      kind: 'NewProposalReply' as const,
      actionUrl: '/app/crew/proposals/4?commentId=2',
      title: 'River mentioned you',
      actorUserId: 12,
      actorAvatarResourceId: 'avatar'
    });

    expect(stripped.actorUserId).toBeNull();
    expect(stripped.actorAvatarResourceId).toBeNull();
  });
});
