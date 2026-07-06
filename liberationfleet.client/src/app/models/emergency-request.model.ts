export interface EmergencyRequestListItem {
  id: number;
  requesterUserId: number;
  requesterUsername: string;
  purposePreview: string;
  amountNeeded: number;
  amountFulfilled: number;
  amountRemaining: number;
  createdAt: string;
}

export interface EmergencyRequestListResponse {
  success: boolean;
  message: string;
  items: EmergencyRequestListItem[];
}

export interface EmergencyPlatform {
  platformId: number;
  platformName: string;
  handle: string;
  isPreferred: boolean;
  isSharedWithViewer: boolean;
}

export interface EmergencyMiddlemanOption {
  userId: number;
  username: string;
  commonPlatformIds: number[];
}

export interface EmergencyRequestDetail {
  id: number;
  requesterUserId: number;
  requesterUsername: string;
  purpose: string;
  amountNeeded: number;
  amountFulfilled: number;
  amountRemaining: number;
  status: string;
  createdAt: string;
  commonPlatforms: EmergencyPlatform[];
  middlemanOptions: EmergencyMiddlemanOption[];
  isSelfRequest: boolean;
}

export interface EmergencyRequestDetailResponse {
  success: boolean;
  message: string;
  request: EmergencyRequestDetail | null;
}

export interface EmergencyRequestOperationResponse {
  success: boolean;
  message: string;
  requestId: number;
}
