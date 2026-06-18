export type GiftLogType = 'direct' | 'initiated' | 'completed';
export type ReceptionEntryType = 'survivalThreshold' | 'cycle';
export type GiftEntryStatus = 'pending' | 'completed';

export type PaymentPlatform = string;

export interface PaymentPlatformOption {
  id: number;
  name: string;
}

export interface CrewMember {
  id: number;
  username: string;
}

export interface GiftLogEntry {
  id: number;
  type: GiftLogType;
  giverId: number;
  giverName: string;
  recipientId: number;
  recipientName: string;
  middlemanId?: number;
  middlemanName?: string;
  amount: number;
  platform: PaymentPlatform;
  timestamp: Date;
  message: string;
  relatedUserIds: number[];
  canCompleteAsMiddleman?: boolean;
  status?: GiftEntryStatus;
}

export interface PendingMiddlemanGift {
  id: number;
  initiatorId: number;
  initiatorName: string;
  recipientId: number;
  recipientName: string;
  amount: number;
  platform?: PaymentPlatform;
}

export interface RecordGiftRequest {
  amount: number;
  recipientId?: number;
  middlemanId?: number;
  completingGiftId?: number;
  paymentPlatformId: number;
}

export interface GiftRecordItem {
  amount: number;
  paymentPlatformId: number;
  recipientId: number;
  middlemanId?: number;
  isCustom: boolean;
  entryType?: ReceptionEntryType;
}

export interface MiddlemanOption {
  userId: number;
  username: string;
}

export interface ReceptionOrderEntry {
  userId: number;
  username: string;
  amountNeeded: number;
  entryType: ReceptionEntryType;
  thresholdId?: number;
  cycleUserId?: number;
  middlemanOptions: MiddlemanOption[];
  defaultMiddlemanId?: number;
  noSuitableMiddleman: boolean;
  giverPlatformIds: number[];
  recipientPlatformIds: number[];
}

export interface SeasonStatus {
  seasonStarted: boolean;
  userInSeason: boolean;
  userSeasonReady: boolean;
  readyCount: number;
  canStartSeason: boolean;
  estimatedMonthlyContribution?: number;
}

export interface SeasonSetupSaveResult {
  success: boolean;
  message: string;
  status?: SeasonStatus;
}

export interface SeasonReadyResult {
  success: boolean;
  message: string;
  seasonStarted: boolean;
  status?: SeasonStatus;
}

export interface GiftLogResponse {
  success: boolean;
  message: string;
  items: GiftLogEntry[];
}

export interface GiftOperationResponse {
  success: boolean;
  message: string;
  entry?: GiftLogEntry;
}

export interface NextAidInfo {
  recipientName: string;
  amount: number;
}
