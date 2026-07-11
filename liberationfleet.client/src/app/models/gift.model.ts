import { EncryptedPayload } from './crypto.model';

export type GiftLogType = 'direct' | 'initiated' | 'completed';
export type ReceptionEntryType = 'survivalThreshold' | 'cycle';
export type GiftEntryStatus = 'pending' | 'completed' | 'cantComplete';
export type GiftDisplayFlag = 'notComplete' | 'cantComplete';
export type GiftVerificationAction =
  | 'confirmReceived'
  | 'confirmNotReceived'
  | 'completeTransfer'
  | 'cantComplete';

export type PaymentPlatform = string;

export interface PaymentPlatformOption {
  id: number;
  name: string;
}

export interface CrewMember {
  id: number;
  username: string;
  platformIds: number[];
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
  status?: GiftEntryStatus;
  verificationStatus?: string;
  displayFlag?: GiftDisplayFlag | null;
  availableActions?: GiftVerificationAction[];
  completionPlatformOptions?: PaymentPlatformOption[];
  hasEncryptedContent?: boolean;
  encryptedPayload?: EncryptedPayload | null;
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

export interface PlatformAccount {
  platformId: number;
  name: string;
  handle: string;
}

export interface MiddlemanOption {
  userId: number;
  username: string;
  commonPlatformIds: number[];
  platformAccounts: PlatformAccount[];
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
  commonPlatformIds: number[];
  recipientPreferredPlatformName?: string;
  recipientPreferredPlatformHandle?: string;
  recipientPlatformAccounts: PlatformAccount[];
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
  hasMore: boolean;
}

export interface GiftLogPage {
  items: GiftLogEntry[];
  hasMore: boolean;
}

export interface GiftLogQueryOptions {
  limit?: number;
  beforeCreatedAt?: string;
  beforeId?: number;
}

export interface GiftOperationResponse {
  success: boolean;
  message: string;
  entry?: GiftLogEntry;
}

export interface NextAidInfo {
  recipientName: string;
  amount: number;
  isCurrentUserRecipient?: boolean;
  platformDisplayKind?: 'none' | 'preferred' | 'common' | 'middlemanNeeded' | 'unavailable';
  platformName?: string;
  platformHandle?: string;
}

export interface GiftHistoryRecipientSummary {
  recipientUserId: number;
  recipientUsername: string;
  totalAmount: number;
  giftCount: number;
  lastGiftAt: string;
}

export interface GiftHistoryRecipientListResponse {
  success: boolean;
  message: string;
  items: GiftHistoryRecipientSummary[];
}

export interface GiftHistoryEntry {
  id: number;
  amount: number;
  timestamp: string;
  giftType: string;
  platform: string;
  middlemanUsername?: string | null;
  statusLabel: string;
}

export interface GiftHistoryDetailResponse {
  success: boolean;
  message: string;
  recipientUserId: number;
  recipientUsername: string;
  totalAmount: number;
  items: GiftHistoryEntry[];
}
