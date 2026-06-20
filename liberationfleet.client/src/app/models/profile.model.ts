import { PaymentPlatform } from './gift.model';

export const CUSTOM_PLATFORM_OPTION_ID = 0;

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
  inNeedOfAid: boolean;
  emergencyLevel: number;
  needsSurvivalAid: boolean;
  stats: UserProfileStats;
}

export interface UserProfileStats {
  sacrificeCount: number;
  averageMonthlyContributions: number;
  membershipStatus: boolean;
  lifetimeContributions: number;
  receptionLastYear: number;
  percentBoost: number;
  priorityScore: number;
}

export interface UpdateProfileRequest {
  username: string;
  email: string;
  paymentPlatforms: PaymentPlatformAccount[];
  inNeedOfAid: boolean;
  emergencyLevel: number;
  needsSurvivalAid: boolean;
}

export interface ProfileOperationResult {
  success: boolean;
  message: string;
  profile?: UserProfile;
}
