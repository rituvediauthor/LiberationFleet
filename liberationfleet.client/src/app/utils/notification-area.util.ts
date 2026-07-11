import { NotificationItem } from '../models/notification.model';

export type CrewNotificationArea =
  | 'chats'
  | 'forums'
  | 'proposals'
  | 'giftLog'
  | 'rules'
  | 'library'
  | 'crewmates';

export type CrewNotificationAreaCounts = Record<CrewNotificationArea, number>;

export function emptyAreaCounts(): CrewNotificationAreaCounts {
  return {
    chats: 0,
    forums: 0,
    proposals: 0,
    giftLog: 0,
    rules: 0,
    library: 0,
    crewmates: 0
  };
}

export function resolveNotificationArea(item: NotificationItem): CrewNotificationArea | null {
  const path = item.actionUrl.split('?')[0];

  if (path.startsWith('/app/crew/chats/')) {
    return 'chats';
  }
  if (path.startsWith('/app/crew/forums/')) {
    return 'forums';
  }
  if (path.startsWith('/app/crew/proposals')) {
    return 'proposals';
  }
  if (path.startsWith('/app/crew/library-of-things')) {
    return 'library';
  }
  if (path.startsWith('/app/crew/rules')) {
    return 'rules';
  }
  if (path.startsWith('/app/crew/crewmates')) {
    return 'crewmates';
  }
  if (
    path === '/app/crew/gift-log'
    || path.startsWith('/app/crew/season-setup')
    || path.startsWith('/app/crew/join-season')
  ) {
    return 'giftLog';
  }

  switch (item.kind) {
    case 'NewChatMessage':
      return 'chats';
    case 'NewForumPost':
    case 'NewForumComment':
      return 'forums';
    case 'NewProposal':
    case 'ProposalRejected':
    case 'ProposalAccepted':
    case 'NewReply':
      return 'proposals';
    case 'NewGifts':
    case 'NewCycle':
    case 'NewSeason':
      return 'giftLog';
    case 'NewRule':
    case 'RuleDeleted':
    case 'RuleEdited':
    case 'CrewSettingChanged':
      return 'rules';
    case 'NewCrewmate':
    case 'CrewmateKicked':
    case 'CrewmateRejoinAllowed':
    case 'JoinRequestFromPerson':
    case 'JoinRequestFromCrew':
      return 'crewmates';
    default:
      return null;
  }
}

export function buildAreaCounts(items: NotificationItem[]): CrewNotificationAreaCounts {
  const counts = emptyAreaCounts();
  for (const item of items) {
    if (item.isRead) {
      continue;
    }
    const area = resolveNotificationArea(item);
    if (area) {
      counts[area]++;
    }
  }
  return counts;
}

export function formatBadgeCount(count: number, showPlusAtNine = false): string {
  if (count <= 0) {
    return '';
  }
  if (showPlusAtNine && count > 9) {
    return '9+';
  }
  return count > 9 ? '9' : String(count);
}

export function resourceCount(
  resourceCounts: Record<string, number>,
  key: string
): number {
  return resourceCounts[key] ?? 0;
}

export function badgeForResource(
  resourceCounts: Record<string, number>,
  key: string
): string {
  return formatBadgeCount(resourceCount(resourceCounts, key));
}
