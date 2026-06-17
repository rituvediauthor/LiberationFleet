export type GiftLogType = 'direct' | 'initiated' | 'completed';

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
  isSurvivalThreshold?: boolean;
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

export interface RecipientNeed {
  userId: number;
  username: string;
  amountNeeded: number;
  isSurvivalThreshold: boolean;
  receptionOrderPosition: number;
  commonPaymentPlatforms: number[];
  suggestedMiddlemanId?: number;
  suggestedMiddlemanName?: string;
  paymentNote: string;
}

export interface ReceptionOrderResponse {
  success: boolean;
  message: string;
  recipients: RecipientNeed[];
}
