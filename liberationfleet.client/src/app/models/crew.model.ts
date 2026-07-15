export type CrewPrivacy = 'Public' | 'Private' | 'InviteOnly';
export type CrewScope = 'Local' | 'Online';
export type CycleCapMode = 'CapacityBased' | 'Fixed';

export interface Crew {
  id: number;
  name: string;
  maxSize: number;
  memberCount: number;
  privacy: CrewPrivacy;
  scope: CrewScope;
  zipCode?: string;
  radiusMiles?: number;
  joinCode: string;
  distanceMiles?: number;
  allowSurvivalThresholds?: boolean;
  allowCrossCrewGiving?: boolean;
  requireApprovalForEdits?: boolean;
  inNeedDefaultThreshold?: number;
  libraryOfThingsEnabled?: boolean;
  memberCycleCapMode?: CycleCapMode;
  memberCycleCapFixedAmount?: number;
  memberCycleCapMultiplier?: number;
  nonMemberCycleCapMode?: CycleCapMode;
  nonMemberCycleCapFixedAmount?: number;
  nonMemberCycleCapMultiplier?: number;
  monthlyGivingCapacity?: number;
  allowCrewmateFileAttachments?: boolean;
  minimumCrewmateTenureDaysForAttachments?: number;
  minimumContributionForAttachments?: number;
  minimumCrewmateTenureDaysForProposals?: number;
  minimumContributionForProposals?: number;
}

export interface CrewMembershipStatus {
  hasCrew: boolean;
  crewId?: number;
  crewName?: string;
  joinCode?: string;
  libraryOfThingsEnabled?: boolean;
  isOrganizer?: boolean;
  canAttachFilesToCrewContent?: boolean;
  canCreateProposals?: boolean;
  canCreateFleetProposals?: boolean;
  canAttachFilesToFleetContent?: boolean;
  canExportCrewData?: boolean;
  crewTenureDays?: number;
  fleetTenureDays?: number;
  crewProposalDaysRemaining?: number;
  crewProposalContributionShortfall?: number;
  fleetProposalDaysRemaining?: number;
  fleetProposalContributionShortfall?: number;
}

export interface CreateCrewRequest {
  name: string;
  maxSize: number;
  privacy: CrewPrivacy;
  scope: CrewScope;
  zipCode?: string;
  radiusMiles?: number;
}

export interface SearchCrewsRequest {
  scope: CrewScope;
  zipCode?: string;
  radiusMiles?: number;
  page: number;
  pageSize: number;
}

export interface CrewSearchResult {
  success: boolean;
  message: string;
  items: Crew[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CrewOperationResult {
  success: boolean;
  message: string;
  crew?: Crew;
  proposalsSubmitted?: boolean;
  proposalsCreated?: number;
}

export interface UpdateCrewRequest {
  name: string;
  maxSize: number;
  privacy: CrewPrivacy;
  scope: CrewScope;
  zipCode?: string;
  radiusMiles?: number;
  allowSurvivalThresholds: boolean;
  allowCrossCrewGiving: boolean;
  requireApprovalForEdits: boolean;
  inNeedDefaultThreshold: number;
  libraryOfThingsEnabled: boolean;
  memberCycleCapMode: CycleCapMode;
  memberCycleCapFixedAmount: number;
  memberCycleCapMultiplier: number;
  nonMemberCycleCapMode: CycleCapMode;
  nonMemberCycleCapFixedAmount: number;
  nonMemberCycleCapMultiplier: number;
  allowCrewmateFileAttachments: boolean;
  minimumCrewmateTenureDaysForAttachments: number;
  minimumContributionForAttachments: number;
  minimumCrewmateTenureDaysForProposals: number;
  minimumContributionForProposals: number;
}

export interface PublicCrewRule {
  id: number;
  title: string;
  description: string;
}

export interface PublicCrewRulesResponse {
  success: boolean;
  message: string;
  crewId: number;
  crewName: string;
  items: PublicCrewRule[];
}

export interface SubmitJoinRequestBody {
  crewId?: number;
  joinCode?: string;
  invitationId?: number;
  acceptedRuleIds: number[];
}

export interface InviteCandidate {
  userId: number;
  username: string;
  isFriend: boolean;
}

export interface InviteCandidateListResponse {
  success: boolean;
  message: string;
  items: InviteCandidate[];
}

export interface CrewInvitation {
  id: number;
  crewId: number;
  crewName: string;
  inviterUserId: number;
  inviterUsername: string;
  status: string;
  createdAt: string;
}

export interface CrewInvitationListResponse {
  success: boolean;
  message: string;
  items: CrewInvitation[];
}

export interface CrewInvitationDetailResponse {
  success: boolean;
  message: string;
  invitation?: CrewInvitation | null;
}

export interface CrewInvitationOperationResponse {
  success: boolean;
  message: string;
  invitationId: number;
  crewId?: number | null;
}

export interface JoinRequestListItem {
  proposalId: number;
  crewId: number;
  crewName: string;
  status: string;
  approveCount: number;
  disapproveCount: number;
  approvalTimerEndsAt?: string | null;
  isKeyPrepared: boolean;
  createdAt: string;
}

export interface JoinRequestListResponse {
  success: boolean;
  message: string;
  items: JoinRequestListItem[];
}

export interface JoinRequestOperationResponse {
  success: boolean;
  message: string;
  proposalId?: number;
}
