export type CrewPrivacy = 'Public' | 'Private';
export type CrewScope = 'Local' | 'Online';

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
  requireApprovalForEdits?: boolean;
  inNeedDefaultThreshold?: number;
}

export interface CrewMembershipStatus {
  hasCrew: boolean;
  crewId?: number;
  crewName?: string;
  joinCode?: string;
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
  requireApprovalForEdits: boolean;
  inNeedDefaultThreshold: number;
}

export interface JoinCrewRequest {
  crewId?: number;
  joinCode?: string;
}
