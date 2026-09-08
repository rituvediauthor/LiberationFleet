import { PaymentPlatform } from './gift.model';

export const CUSTOM_PLATFORM_OPTION_ID = 0;

export interface PaymentPlatformSnapshot {
  id: number;
  platformId: number;
  customPlatformName: string;
  handle: string;
  isPreferred: boolean;
}

export interface PaymentPlatformAccount {
  id: number;
  platformId: number;
  platform: PaymentPlatform | string;
  handle: string;
  isPreferred?: boolean;
  customPlatformName?: string;
}

export interface PriorityScoreBreakdown {
  score: number;
  crewLifetimeContributions: number;
  emergencyLevel: number;
  membershipBonus: number;
  userLifetimeContributions: number;
  survivalThresholdAmount: number;
  baseScore: number;
  peopleRepresentedCount: number;
  disabilityLevel: number;
  priorityMultiplier: number;
  percentBoost: number;
  sacrificeBonusFactor: number;
  isFinancialMember: boolean;
  statusReason?: string | null;
}

export interface UserProfile {
  id: number;
  username: string;
  email: string;
  avatarResourceId?: string | null;
  paymentPlatforms: PaymentPlatformAccount[];
  roles: string[];
  inNeedOfAid: boolean;
  emergencyLevel: number;
  peopleRepresentedCount: number;
  disabilityLevel: number;
  identityGroups: string[];
  needsSurvivalAid: boolean;
  isSurvivalThresholdRecipient: boolean;
  canToggleInNeedOff: boolean;
  inNeedToggleThreshold: number;
  stats: UserProfileStats;
  givingSeasonPriority?: PriorityScoreBreakdown | null;
  libraryOfThingsPriority?: PriorityScoreBreakdown | null;
}

export interface UserProfileStats {
  sacrificeCountLastSeason: number;
  sacrificeCountThisSeason: number;
  averageMonthlyContributions: number;
  membershipStatus: boolean;
  lifetimeContributions: number;
  receptionThisYear: number;
  percentBoost: number;
  priorityScore: number;
  donationsPreviousTaxYearUsd?: number;
  donationsCurrentTaxYearUsd?: number;
  currentTaxYear?: number;
  previousTaxYear?: number;
}

export interface UpdateProfileRequest {
  username: string;
  email: string;
  avatarResourceId?: string | null;
  paymentPlatforms: PaymentPlatformAccount[];
  inNeedOfAid: boolean;
  emergencyLevel: number;
  peopleRepresentedCount: number;
  disabilityLevel: number;
  identityGroups: string[];
  needsSurvivalAid: boolean;
}

export interface ProfileOperationResult {
  success: boolean;
  message: string;
  profile?: UserProfile;
}
