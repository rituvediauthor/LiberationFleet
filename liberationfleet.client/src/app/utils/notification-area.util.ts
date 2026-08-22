import { NotificationItem, NotificationKind } from '../models/notification.model';

export type CrewNotificationArea =
  | 'crewChats'
  | 'fleetChats'
  | 'crewForums'
  | 'fleetForums'
  | 'crewProposals'
  | 'fleetProposals'
  | 'crewGiftLog'
  | 'fleetGiftLog'
  | 'crewRules'
  | 'fleetRules'
  | 'crewSettings'
  | 'fleetSettings'
  | 'crewLibrary'
  | 'crewCrewmates'
  | 'fleetCrewmates'
  | 'userInvitations'
  | 'fleet';

export type CrewNotificationAreaCounts = Record<CrewNotificationArea, number>;

export function emptyAreaCounts(): CrewNotificationAreaCounts {
  return {
    crewChats: 0,
    fleetChats: 0,
    crewForums: 0,
    fleetForums: 0,
    crewProposals: 0,
    fleetProposals: 0,
    crewGiftLog: 0,
    fleetGiftLog: 0,
    crewRules: 0,
    fleetRules: 0,
    crewSettings: 0,
    fleetSettings: 0,
    crewLibrary: 0,
    crewCrewmates: 0,
    fleetCrewmates: 0,
    userInvitations: 0,
    fleet: 0
  };
}

function isForumPath(path: string): boolean {
  return path.startsWith('/app/crew/forums/') || path.startsWith('/app/fleet/forums/');
}

/** Mirrors server NotificationBadgeBuilder.ResolveArea — keep in sync. */
export function resolveNotificationArea(item: NotificationItem): CrewNotificationArea | null {
  const path = item.actionUrl.split('?')[0];

  if (path.startsWith('/app/crew/invitations')) {
    return 'userInvitations';
  }
  if (path.startsWith('/app/crew/chats/')) {
    return 'crewChats';
  }
  if (path.startsWith('/app/fleet/chats/')) {
    return 'fleetChats';
  }
  if (path.startsWith('/app/crew/forums/')) {
    return 'crewForums';
  }
  if (path.startsWith('/app/fleet/forums/')) {
    return 'fleetForums';
  }
  if (path.startsWith('/app/crew/proposals')) {
    return 'crewProposals';
  }
  if (path.startsWith('/app/fleet/proposals')) {
    return 'fleetProposals';
  }
  if (path.startsWith('/app/crew/library-of-things')) {
    return 'crewLibrary';
  }
  if (path.startsWith('/app/crew/rules')) {
    return 'crewRules';
  }
  if (path.startsWith('/app/fleet/rules')) {
    return 'fleetRules';
  }
  if (path === '/app/crew/edit') {
    return 'crewSettings';
  }
  if (path === '/app/fleet/edit') {
    return 'fleetSettings';
  }
  if (path.startsWith('/app/crew/crewmates')) {
    return 'crewCrewmates';
  }
  if (path.startsWith('/app/fleet/crews')) {
    return 'fleetCrewmates';
  }
  if (
    path === '/app/crew/gift-log'
    || path.startsWith('/app/crew/gift-log/')
    || path.startsWith('/app/crew/season-setup')
    || path.startsWith('/app/crew/join-season')
    || path.startsWith('/app/crew/emergency-requests')
  ) {
    return 'crewGiftLog';
  }
  if (path === '/app/fleet/gift-log' || path.startsWith('/app/fleet/emergency')) {
    return 'fleetGiftLog';
  }
  if (path.startsWith('/app/fleet/')) {
    return 'fleet';
  }

  switch (item.kind as NotificationKind) {
    case 'NewChatMessage':
      return 'crewChats';
    case 'NewFleetChatMessage':
      return 'fleetChats';
    case 'NewForumPost':
    case 'NewForumComment':
    case 'ForumPostLiked':
    case 'ForumCommentLiked':
    case 'NewReply':
      return 'crewForums';
    case 'Mention':
      return isForumPath(path) ? 'crewForums' : null;
    case 'NewFleetForumPost':
    case 'NewFleetForumComment':
    case 'NewFleetReply':
    case 'FleetForumPostLiked':
    case 'FleetForumCommentLiked':
      return 'fleetForums';
    case 'FleetMention':
      return isForumPath(path) ? 'fleetForums' : null;
    case 'NewProposal':
    case 'ProposalRejected':
    case 'ProposalAccepted':
    case 'NewProposalReply':
      return 'crewProposals';
    case 'NewFleetProposal':
    case 'FleetProposalAccepted':
    case 'FleetProposalRejected':
    case 'NewFleetProposalReply':
      return 'fleetProposals';
    case 'NewGifts':
    case 'NewCycle':
    case 'NewSeason':
    case 'SurvivalThresholdsRefreshed':
    case 'NewEmergencyRequest':
    case 'NewGiftComment':
    case 'GiftEntryLiked':
    case 'GiftCommentLiked':
    case 'NewGiftReply':
      return 'crewGiftLog';
    case 'NewFleetGifts':
      return 'fleetGiftLog';
    case 'NewRule':
    case 'RuleDeleted':
    case 'RuleEdited':
      return 'crewRules';
    case 'NewFleetRule':
    case 'FleetRuleDeleted':
    case 'FleetRuleEdited':
      return 'fleetRules';
    case 'CrewSettingChanged':
      return 'crewSettings';
    case 'FleetSettingChanged':
      return 'fleetSettings';
    case 'NewCrewmate':
    case 'CrewmateKicked':
    case 'CrewmateRejoinAllowed':
    case 'JoinRequestFromPerson':
      return 'crewCrewmates';
    case 'JoinRequestFromCrew':
      return 'userInvitations';
    case 'NewLibraryRequest':
    case 'LibraryRequestDenied':
    case 'LibraryRequestCompleted':
    case 'NewLibraryRequestMessage':
    case 'LibraryUnitBrokenReported':
    case 'LibraryUnitBrokenConfirmed':
    case 'LibraryUnitReportedFixed':
      return 'crewLibrary';
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
