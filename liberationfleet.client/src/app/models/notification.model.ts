export type NotificationKind =
  | 'NewProposal'
  | 'ProposalRejected'
  | 'ProposalAccepted'
  | 'NewGifts'
  | 'NewCycle'
  | 'NewSeason'
  | 'NewChatMessage'
  | 'NewReply'
  | 'NewForumPost'
  | 'NewForumComment'
  | 'NewCrewmate'
  | 'JoinRequestFromPerson'
  | 'JoinRequestFromCrew'
  | 'NewRule'
  | 'RuleDeleted'
  | 'RuleEdited'
  | 'CrewSettingChanged'
  | 'CrewmateKicked'
  | 'CrewmateRejoinAllowed'
  | 'Mention'
  | 'NewLibraryRequest'
  | 'LibraryRequestDenied'
  | 'LibraryRequestCompleted'
  | 'NewLibraryRequestMessage'
  | 'LibraryUnitBrokenReported'
  | 'LibraryUnitBrokenConfirmed'
  | 'LibraryUnitReportedFixed'
  | 'SurvivalThresholdsRefreshed'
  | 'NewFleetGifts'
  | 'NewFleetProposal'
  | 'FleetSettingChanged'
  | 'NewFleetChatMessage'
  | 'NewFleetForumPost'
  | 'NewFleetForumComment'
  | 'NewEmergencyRequest'
  | 'ForumPostLiked'
  | 'ForumCommentLiked'
  | 'NewFleetRule'
  | 'FleetRuleDeleted'
  | 'FleetRuleEdited'
  | 'FleetProposalAccepted'
  | 'FleetProposalRejected'
  | 'NewFleetReply'
  | 'FleetMention'
  | 'FleetForumPostLiked'
  | 'FleetForumCommentLiked'
  | 'NewGiftComment'
  | 'GiftEntryLiked'
  | 'GiftCommentLiked'
  | 'NewGiftReply'
  | 'NewProposalReply'
  | 'NewFleetProposalReply'
  | 'FriendRequest'
  | 'FriendRequestAccepted'
  | 'NewDirectMessage'
  | 'ChatMessageLiked'
  | 'LibraryTaskScheduleChanged'
  | 'CrewJoinSwitchOffer';

export type NotificationPreferenceCategory = 'Crew' | 'Fleet' | 'Friends';

export type NotificationFilterCategory = 'All' | 'Comments' | 'Mentions' | 'Proposals' | 'Rules';

export type MutedContentType = 'ChatRoom' | 'Forum' | 'Friend' | 'CrewSpace' | 'FleetSpace';

export type MuteDuration = 'OneHour' | 'EightHours' | 'OneDay' | 'OneWeek' | 'Always';

export const MUTE_DURATION_OPTIONS: { value: MuteDuration; label: string }[] = [
  { value: 'OneHour', label: '1 hour' },
  { value: 'EightHours', label: '8 hours' },
  { value: 'OneDay', label: '1 day' },
  { value: 'OneWeek', label: '1 week' },
  { value: 'Always', label: 'Always' }
];

const MUTE_DURATION_MS: Record<Exclude<MuteDuration, 'Always'>, number> = {
  OneHour: 60 * 60 * 1000,
  EightHours: 8 * 60 * 60 * 1000,
  OneDay: 24 * 60 * 60 * 1000,
  OneWeek: 7 * 24 * 60 * 60 * 1000
};

export function mutedUntilFor(duration: MuteDuration): string | null {
  if (duration === 'Always') {
    return null;
  }
  return new Date(Date.now() + MUTE_DURATION_MS[duration]).toISOString();
}

export function withMute(
  items: MutedContentItem[],
  contentType: MutedContentType,
  resourceId: number,
  duration: MuteDuration | null
): MutedContentItem[] {
  const next = items.filter(item => !(item.contentType === contentType && item.resourceId === resourceId));
  if (!duration) {
    return next;
  }
  return [...next, { contentType, resourceId, mutedUntil: mutedUntilFor(duration) }];
}

export interface MutedContentItem {
  contentType: MutedContentType;
  resourceId: number;
  mutedUntil?: string | null;
}

export interface MutedContentListResponse {
  success: boolean;
  message: string;
  items: MutedContentItem[];
}

export interface HiddenContentItem {
  contentType: MutedContentType;
  resourceId: number;
}

export interface HiddenContentListResponse {
  success: boolean;
  message: string;
  items: HiddenContentItem[];
}

export interface NotificationItem {
  id: number;
  crewId?: number | null;
  kind: NotificationKind;
  title: string;
  body: string;
  actionUrl: string;
  relatedEntityId?: number | null;
  secondaryEntityId?: number | null;
  actorUserId?: number | null;
  actorAvatarResourceId?: string | null;
  isRead: boolean;
  createdAt: string;
  isTargetAvailable?: boolean | null;
}

export interface NotificationListResponse {
  success: boolean;
  message: string;
  items: NotificationItem[];
  unreadCount: number;
}

export interface NotificationPreference {
  kind: NotificationKind;
  label: string;
  category: NotificationPreferenceCategory;
  isEnabled: boolean;
}

export interface NotificationPreferencesResponse {
  success: boolean;
  message: string;
  preferences: NotificationPreference[];
}

export interface NotificationPreferencesUpdateRequest {
  preferences: NotificationPreference[];
  settingsPassword?: string;
}

export interface NotificationOperationResponse {
  success: boolean;
  message: string;
  unreadCount: number;
}

export interface NotificationBadgeSummaryResponse {
  success: boolean;
  message: string;
  unreadCount: number;
  areaCounts: Record<string, number>;
  resourceCounts: Record<string, number>;
}

export interface MarkNotificationsReadByContentRequest {
  actionUrlPrefix?: string | null;
  relatedEntityId?: number | null;
}

export const NOTIFICATION_FILTER_OPTIONS: { value: NotificationFilterCategory; label: string }[] = [
  { value: 'All', label: 'All' },
  { value: 'Comments', label: 'Comments' },
  { value: 'Mentions', label: 'Mentions' },
  { value: 'Proposals', label: 'Proposals' },
  { value: 'Rules', label: 'Rules' }
];
