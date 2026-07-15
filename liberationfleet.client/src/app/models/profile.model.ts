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

export interface UserProfile {
  id: number;
  username: string;
  email: string;
  paymentPlatforms: PaymentPlatformAccount[];
  roles: string[];
  inNeedOfAid: boolean;
  emergencyLevel: number;
  peopleRepresentedCount: number;
  disabilityLevel: number;
  needsSurvivalAid: boolean;
  isSurvivalThresholdRecipient: boolean;
  stats: UserProfileStats;
}

export interface UserProfileStats {
  sacrificeCountLastSeason: number;
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
  paymentPlatforms: PaymentPlatformAccount[];
  inNeedOfAid: boolean;
  emergencyLevel: number;
  peopleRepresentedCount: number;
  disabilityLevel: number;
  needsSurvivalAid: boolean;
}

export interface ProfileOperationResult {
  success: boolean;
  message: string;
  profile?: UserProfile;
}
