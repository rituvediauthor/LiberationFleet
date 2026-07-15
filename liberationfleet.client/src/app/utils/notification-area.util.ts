import { NotificationItem } from '../models/notification.model';

export type CrewNotificationArea =
  | 'chats'
  | 'forums'
  | 'proposals'
  | 'giftLog'
  | 'rules'
  | 'library'
  | 'crewmates'
  | 'fleet';

export type CrewNotificationAreaCounts = Record<CrewNotificationArea, number>;

export function emptyAreaCounts(): CrewNotificationAreaCounts {
  return {
    chats: 0,
    forums: 0,
    proposals: 0,
    giftLog: 0,
    rules: 0,
    library: 0,
    crewmates: 0,
    fleet: 0
  };
}

export function resolveNotificationArea(item: NotificationItem): CrewNotificationArea | null {
  const path = item.actionUrl.split('?')[0];

  if (path.startsWith('/app/crew/chats/') || path.startsWith('/app/fleet/chats/')) {
    return 'chats';
  }
  if (path.startsWith('/app/crew/forums/') || path.startsWith('/app/fleet/forums/')) {
    return 'forums';
  }
  if (path.startsWith('/app/crew/proposals') || path.startsWith('/app/fleet/proposals')) {
    return 'proposals';
  }
  if (path.startsWith('/app/crew/library-of-things') || path.startsWith('/app/fleet/library')) {
    return 'library';
  }
  if (path.startsWith('/app/crew/rules') || path.startsWith('/app/fleet/rules')) {
    return 'rules';
  }
  if (path.startsWith('/app/crew/crewmates') || path.startsWith('/app/fleet/crews')) {
    return 'crewmates';
  }
  if (
    path === '/app/crew/gift-log'
    || path.startsWith('/app/crew/season-setup')
    || path.startsWith('/app/crew/join-season')
    || path === '/app/fleet/gift-log'
    || path.startsWith('/app/fleet/emergency')
  ) {
    return 'giftLog';
  }
  if (path.startsWith('/app/fleet/')) {
    return 'fleet';
  }

  switch (item.kind) {
    case 'NewChatMessage':
    case 'NewFleetChatMessage':
      return 'chats';
    case 'NewForumPost':
    case 'NewForumComment':
      return 'forums';
    case 'NewProposal':
    case 'NewFleetProposal':
    case 'ProposalRejected':
    case 'ProposalAccepted':
    case 'NewReply':
      return 'proposals';
    case 'NewGifts':
    case 'NewCycle':
    case 'NewSeason':
    case 'SurvivalThresholdsRefreshed':
    case 'NewFleetGifts':
      return 'giftLog';
    case 'NewRule':
    case 'RuleDeleted':
    case 'RuleEdited':
    case 'CrewSettingChanged':
    case 'FleetSettingChanged':
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

export function formatBadgeCount(count: number): string {
  if (count <= 0) {
    return '';
  }
  return count > 9 ? '9+' : String(count);
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
