export type SecurityAlertType =
  | 'StrangeDeviceSignIn'
  | 'SettingsChanged'
  | 'SuspiciousPasswordFailures';

export interface SecuritySettingsDto {
  twoFactorEnabled: boolean;
  lockSettingsWithPassword: boolean;
  hasSettingsLockPassword: boolean;
}

export interface SecuritySettingsResponse {
  success: boolean;
  message: string;
  settings?: SecuritySettingsDto;
}

export interface UpdateSecuritySettingsRequest {
  twoFactorEnabled?: boolean;
  lockSettingsWithPassword?: boolean;
  newSettingsLockPassword?: string;
  currentSettingsLockPassword?: string;
  settingsPassword?: string;
}

export interface RegisteredDeviceDto {
  id: number;
  deviceId: string;
  displayName: string;
  userAgent: string;
  firstSeenAt: string;
  lastSeenAt: string;
  isTrusted: boolean;
  isBlocked: boolean;
  isCurrent: boolean;
}

export interface RegisteredDevicesResponse {
  success: boolean;
  message: string;
  devices: RegisteredDeviceDto[];
}

export interface SecurityAlertDto {
  id: number;
  alertType: SecurityAlertType;
  title: string;
  message: string;
  occurredAt: string;
  isRead: boolean;
  relatedDeviceId?: number | null;
  relatedDeviceName?: string | null;
  canManageDevice: boolean;
}

export interface SecurityAlertsResponse {
  success: boolean;
  message: string;
  alerts: SecurityAlertDto[];
}

export interface SecurityOperationResponse {
  success: boolean;
  message: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface VerifySettingsPasswordRequest {
  settingsPassword: string;
}

export interface VerifySettingsPasswordResponse {
  success: boolean;
  message: string;
}
