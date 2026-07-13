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
  | 'NewFleetChatMessage';

export type NotificationPreferenceCategory = 'Crew' | 'Fleet';

export type NotificationFilterCategory = 'All' | 'Comments' | 'Mentions' | 'Proposals' | 'Rules';

export type MutedContentType = 'ChatRoom' | 'Forum' | 'Friend';

export interface MutedContentItem {
  contentType: MutedContentType;
  resourceId: number;
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
