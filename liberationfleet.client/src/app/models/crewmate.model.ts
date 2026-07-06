export type CrewmateFriendshipState = 'none' | 'requestSent' | 'requestReceived' | 'friends' | 'blocked';

export interface CrewmatePlatformDisplay {
  platformName: string;
  handle: string;
  isSharedWithViewer: boolean;
}

export interface CrewmateListItem {
  userId: number;
  username: string;
  lastLoginAt: string | null;
  isSelf: boolean;
  isPlaceholderMember: boolean;
  platformDisplay: CrewmatePlatformDisplay | null;
  friendshipState: CrewmateFriendshipState;
}

export interface CrewmateListResponse {
  success: boolean;
  message: string;
  items: CrewmateListItem[];
}

export interface CrewmatePaymentPlatform {
  platformId: number;
  platformName: string;
  handle: string;
  isPreferred: boolean;
}

export interface CrewmateElectedRole {
  role: string;
  displayName: string;
}

export interface CrewRoleDefinition {
  role: string;
  displayName: string;
  description: string;
}

export interface CrewRoleDefinitionsResponse {
  success: boolean;
  message: string;
  roles: CrewRoleDefinition[];
}

export interface CrewRoleChangeResponse {
  success: boolean;
  message: string;
  proposalId: number;
}

export interface CrewmateProfile {
  userId: number;
  username: string;
  roles: string[];
  electedRoles: CrewmateElectedRole[];
  paymentPlatforms: CrewmatePaymentPlatform[];
  sacrificeCountLastSeason: number;
  averageMonthlyContributions: number;
  membershipStatus: boolean;
  lifetimeContributions: number;
  receptionThisYear: number;
  priorityScore: number;
  inNeedOfAid: boolean;
  emergencyLevel: number;
  isSurvivalThresholdRecipient: boolean;
  friendshipState: CrewmateFriendshipState;
  isSelf: boolean;
  canAttachFiles: boolean;
  canToggleCanAttachFiles: boolean;
  canModerateAttachments: boolean;
  canExportCrewData: boolean;
  isPlaceholderMember: boolean;
  canClaimIdentity: boolean;
}

export interface CrewmateProfileResponse {
  success: boolean;
  message: string;
  profile: CrewmateProfile | null;
}

export interface CrewmateOperationResponse {
  success: boolean;
  message: string;
  friendshipState: CrewmateFriendshipState;
}

export interface CrewmateKickResponse {
  success: boolean;
  message: string;
  proposalId: number;
}

export interface AddPlaceholderCrewmateResponse {
  success: boolean;
  message: string;
  userId: number;
}

export interface KickedCrewmateListItem {
  userId: number;
  username: string;
}

export interface KickedCrewmateListResponse {
  success: boolean;
  message: string;
  items: KickedCrewmateListItem[];
}

export function mapFriendshipState(value: number | string): CrewmateFriendshipState {
  const normalized = typeof value === 'string' ? value.toLowerCase() : '';
  if (normalized === 'requestsent' || value === 1) return 'requestSent';
  if (normalized === 'requestreceived' || value === 2) return 'requestReceived';
  if (normalized === 'friends' || value === 3) return 'friends';
  if (normalized === 'blocked' || value === 4) return 'blocked';
  return 'none';
}

export function formatLastActive(lastLoginAt: string | null, isSelf = false, isPlaceholderMember = false): string {
  if (isPlaceholderMember) {
    return 'Non-member';
  }

  if (isSelf) {
    return 'Active';
  }

  if (!lastLoginAt) {
    return 'No recent activity';
  }

  const then = new Date(lastLoginAt).getTime();
  const now = Date.now();
  const diffMs = Math.max(0, now - then);
  const diffMinutes = Math.floor(diffMs / 60000);

  if (diffMinutes < 5) {
    return 'Active now';
  }

  if (diffMinutes < 60) {
    return `Active ${diffMinutes} minute${diffMinutes === 1 ? '' : 's'} ago`;
  }

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 48) {
    return `Active ${diffHours} hour${diffHours === 1 ? '' : 's'} ago`;
  }

  const diffDays = Math.floor(diffHours / 24);
  return `Active ${diffDays} day${diffDays === 1 ? '' : 's'} ago`;
}

export function formatPlatformDisplay(platform: CrewmatePlatformDisplay | null): string {
  if (!platform?.platformName) {
    return 'No payment platform';
  }

  if (platform.handle) {
    return `${platform.platformName} · ${platform.handle}`;
  }

  return platform.platformName;
}
