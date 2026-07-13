import { CrewPrivacy, CrewScope } from './crew.model';
import {
  GiftLogEntry,
  GiftRecordItem,
  NextAidInfo,
  ReceptionOrderEntry
} from './gift.model';

export type FleetPrivacy = CrewPrivacy;
export type FleetScope = CrewScope;

export interface FleetStatus {
  hasFleet: boolean;
  fleetId?: number;
  fleetName?: string;
  allowCrossCrewGiving?: boolean;
  joinCode?: string;
  libraryOfThingsEnabled?: boolean;
  needsRuleAcceptance?: boolean;
}

export interface Fleet {
  id: number;
  name: string;
  privacy: FleetPrivacy;
  scope: FleetScope;
  zipCode?: string;
  radiusMiles?: number;
  joinCode: string;
  distanceMiles?: number;
  crewCount?: number;
  requireApprovalForEdits?: boolean;
  libraryOfThingsEnabled?: boolean;
  allowCrewmateFileAttachments?: boolean;
  minimumCrewmateTenureDaysForAttachments?: number;
  minimumContributionForAttachments?: number;
  minimumCrewmateTenureDaysForProposals?: number;
  minimumContributionForProposals?: number;
}

export interface CreateFleetRequest {
  name: string;
  privacy: FleetPrivacy;
  scope: FleetScope;
  zipCode?: string;
  radiusMiles?: number;
}

export interface SearchFleetsRequest {
  scope: FleetScope;
  zipCode?: string;
  radiusMiles?: number;
  page: number;
  pageSize: number;
}

export interface FleetSearchResult {
  success: boolean;
  message: string;
  items: Fleet[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface FleetOperationResult {
  success: boolean;
  message: string;
  fleet?: Fleet;
  proposalsSubmitted?: boolean;
  proposalsCreated?: number;
}

export interface UpdateFleetRequest {
  name: string;
  privacy: FleetPrivacy;
  scope: FleetScope;
  zipCode?: string;
  radiusMiles?: number;
  requireApprovalForEdits: boolean;
  libraryOfThingsEnabled: boolean;
  allowCrewmateFileAttachments: boolean;
  minimumCrewmateTenureDaysForAttachments: number;
  minimumContributionForAttachments: number;
  minimumCrewmateTenureDaysForProposals: number;
  minimumContributionForProposals: number;
}

export interface PublicFleetRule {
  id: number;
  title: string;
  description: string;
}

export interface PublicFleetRulesResponse {
  success: boolean;
  message: string;
  fleetId: number;
  fleetName: string;
  items: PublicFleetRule[];
}

export interface FleetRule {
  id: number;
  createdByUserId: number;
  createdByUsername: string;
  createdAt: string;
  updatedAt: string;
  isPublic: boolean;
  title: string;
  description: string;
}

export interface FleetRuleListResponse {
  success: boolean;
  message: string;
  items: FleetRule[];
}

export interface FleetRuleDetailResponse {
  success: boolean;
  message: string;
  rule?: FleetRule;
}

export interface FleetRuleOperationResponse {
  success: boolean;
  message: string;
  ruleId?: number;
  proposalsSubmitted?: boolean;
  proposalId?: number;
}

export interface WriteFleetRuleBody {
  isPublic: boolean;
  title: string;
  description: string;
}

export interface SubmitFleetJoinRequestBody {
  fleetId?: number;
  joinCode?: string;
  acceptedRuleIds: number[];
}

export interface FleetJoinRequestListItem {
  proposalId: number;
  fleetId: number;
  fleetName: string;
  status: string;
  approveCount: number;
  disapproveCount: number;
  approvalTimerEndsAt?: string | null;
  createdAt: string;
}

export interface FleetJoinRequestListResponse {
  success: boolean;
  message: string;
  items: FleetJoinRequestListItem[];
}

export interface FleetJoinRequestOperationResponse {
  success: boolean;
  message: string;
  proposalId?: number;
}

export interface CrewLookupDto {
  crewId: number;
  crewName: string;
  memberCount: number;
  alreadyInFleet: boolean;
  isOwnCrew: boolean;
}

export interface CrewLookupResponse {
  success: boolean;
  message: string;
  crew?: CrewLookupDto | null;
}

export interface InviteCrewToFleetResponse {
  success: boolean;
  message: string;
  proposalId?: number;
  crewId?: number;
  crewName?: string;
}

export interface FleetCrewListItem {
  crewId: number;
  crewName: string;
  memberCount: number;
  maxSize?: number;
  isOwnCrew?: boolean;
}

export interface FleetCrewListResponse {
  success: boolean;
  message: string;
  items: FleetCrewListItem[];
}

export interface FleetCrewmateSummary {
  userId: number;
  username: string;
  isSelf?: boolean;
}

export interface FleetCrewDetail {
  crewId: number;
  crewName: string;
  memberCount: number;
  maxSize?: number;
  isOwnCrew?: boolean;
  canKick?: boolean;
  canJoin?: boolean;
  crewmates: FleetCrewmateSummary[];
}

export interface FleetCrewDetailResponse {
  success: boolean;
  message: string;
  crew?: FleetCrewDetail;
}

export interface FleetCrewOperationResponse {
  success: boolean;
  message: string;
}

export interface FleetGiftLogResponse {
  success: boolean;
  message: string;
  items: GiftLogEntry[];
  hasMore: boolean;
}

export interface FleetReceptionOrderResponse {
  success: boolean;
  message: string;
  items: ReceptionOrderEntry[];
}

export interface FleetRecordGiftsRequest {
  gifts: GiftRecordItem[];
}

export interface FleetRecordGiftsResponse {
  success: boolean;
  message: string;
}

export interface FleetNextAidResponse {
  success: boolean;
  message: string;
  nextAid?: NextAidInfo | null;
}

export interface FleetEmergencyListItem {
  id: number;
  requesterUserId: number;
  requesterUsername: string;
  purposePreview: string;
  amountNeeded: number;
  amountFulfilled: number;
  amountRemaining: number;
  createdAt: string;
}

export interface FleetEmergencyListResponse {
  success: boolean;
  message: string;
  items: FleetEmergencyListItem[];
}
