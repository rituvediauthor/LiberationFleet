import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ChangePasswordRequest,
  DeleteAccountRequest,
  BeginEmailMfaRequest,
  ConfirmEmailMfaRequest,
  EmailMfaChallengeResponse,
  RegisteredDevicesResponse,
  ResendEmailMfaRequest,
  SecurityAlertsResponse,
  SecurityOperationResponse,
  SecuritySettingsResponse,
  UpdateSecuritySettingsRequest,
  VerifySettingsPasswordRequest,
  VerifySettingsPasswordResponse
} from '../models/security.model';

@Injectable({
  providedIn: 'root'
})
export class SecurityService {
  private readonly apiUrl = '/api/security';

  constructor(private http: HttpClient) {}

  getSettings(): Observable<SecuritySettingsResponse> {
    return this.http.get<SecuritySettingsResponse>(`${this.apiUrl}/settings`);
  }

  updateSettings(request: UpdateSecuritySettingsRequest): Observable<SecuritySettingsResponse> {
    return this.http.put<SecuritySettingsResponse>(`${this.apiUrl}/settings`, request);
  }

  beginEnableEmailMfa(request: BeginEmailMfaRequest = {}): Observable<EmailMfaChallengeResponse> {
    return this.http.post<EmailMfaChallengeResponse>(`${this.apiUrl}/mfa/email/begin-enable`, request);
  }

  confirmEnableEmailMfa(request: ConfirmEmailMfaRequest): Observable<EmailMfaChallengeResponse> {
    return this.http.post<EmailMfaChallengeResponse>(`${this.apiUrl}/mfa/email/confirm-enable`, request);
  }

  beginDisableEmailMfa(request: BeginEmailMfaRequest = {}): Observable<EmailMfaChallengeResponse> {
    return this.http.post<EmailMfaChallengeResponse>(`${this.apiUrl}/mfa/email/begin-disable`, request);
  }

  confirmDisableEmailMfa(request: ConfirmEmailMfaRequest): Observable<EmailMfaChallengeResponse> {
    return this.http.post<EmailMfaChallengeResponse>(`${this.apiUrl}/mfa/email/confirm-disable`, request);
  }

  resendEmailMfa(request: ResendEmailMfaRequest): Observable<EmailMfaChallengeResponse> {
    return this.http.post<EmailMfaChallengeResponse>(`${this.apiUrl}/mfa/email/resend`, request);
  }

  getAlerts(): Observable<SecurityAlertsResponse> {
    return this.http.get<SecurityAlertsResponse>(`${this.apiUrl}/alerts`);
  }

  markAlertRead(alertId: number): Observable<SecurityOperationResponse> {
    return this.http.post<SecurityOperationResponse>(`${this.apiUrl}/alerts/${alertId}/read`, {});
  }

  getDevices(currentDeviceId?: string): Observable<RegisteredDevicesResponse> {
    let params = new HttpParams();
    if (currentDeviceId) {
      params = params.set('currentDeviceId', currentDeviceId);
    }

    return this.http.get<RegisteredDevicesResponse>(`${this.apiUrl}/devices`, { params });
  }

  trustDevice(deviceId: number): Observable<SecurityOperationResponse> {
    return this.http.post<SecurityOperationResponse>(`${this.apiUrl}/devices/${deviceId}/trust`, {});
  }

  blockDevice(deviceId: number): Observable<SecurityOperationResponse> {
    return this.http.post<SecurityOperationResponse>(`${this.apiUrl}/devices/${deviceId}/block`, {});
  }

  unblockDevice(deviceId: number): Observable<SecurityOperationResponse> {
    return this.http.post<SecurityOperationResponse>(`${this.apiUrl}/devices/${deviceId}/unblock`, {});
  }

  changePassword(request: ChangePasswordRequest): Observable<SecurityOperationResponse> {
    return this.http.post<SecurityOperationResponse>(`${this.apiUrl}/change-password`, request);
  }

  deleteAccount(request: DeleteAccountRequest): Observable<SecurityOperationResponse> {
    return this.http.post<SecurityOperationResponse>(`${this.apiUrl}/delete-account`, request);
  }

  verifySettingsPassword(request: VerifySettingsPasswordRequest): Observable<VerifySettingsPasswordResponse> {
    return this.http.post<VerifySettingsPasswordResponse>(`${this.apiUrl}/verify-settings-password`, request);
  }
}
