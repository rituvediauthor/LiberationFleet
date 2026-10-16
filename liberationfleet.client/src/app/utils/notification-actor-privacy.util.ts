import { NotificationItem, NotificationKind } from '../models/notification.model';

const ALWAYS_ANONYMOUS_KINDS = new Set<NotificationKind>([
  'NewProposal',
  'NewFleetProposal',
  'NewProposalReply',
  'NewFleetProposalReply'
]);

const CHAT_OR_MENTION_KINDS = new Set<NotificationKind>([
  'NewChatMessage',
  'NewFleetChatMessage',
  'Mention',
  'FleetMention'
]);

export function shouldOmitNotificationActor(item: Pick<NotificationItem, 'kind' | 'actionUrl' | 'title'>): boolean {
  if (ALWAYS_ANONYMOUS_KINDS.has(item.kind)) {
    return true;
  }

  const path = (item.actionUrl ?? '').split('?')[0];
  const isProposalPath = path.startsWith('/app/crew/proposals') || path.startsWith('/app/fleet/proposals');
  if ((item.kind === 'Mention' || item.kind === 'FleetMention') && isProposalPath) {
    return true;
  }

  const title = (item.title ?? '').trimStart();
  return CHAT_OR_MENTION_KINDS.has(item.kind) && title.toLowerCase().startsWith('anonymous');
}

export function stripAnonymousNotificationActor<T extends Pick<NotificationItem, 'kind' | 'actionUrl' | 'title' | 'actorUserId' | 'actorAvatarResourceId'>>(
  item: T
): T {
  if (!shouldOmitNotificationActor(item)) {
    return item;
  }

  return {
    ...item,
    actorUserId: null,
    actorAvatarResourceId: null
  };
}
