import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CUSTOM_PLATFORM_OPTION_ID, PaymentPlatformAccount, ProfileOperationResult, UpdateProfileRequest, UserProfile } from '../models/profile.model';

@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  private readonly apiUrl = '/api/profile';
  private nextTempPlatformId = -1;

  constructor(private http: HttpClient) {}

  getProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(this.apiUrl);
  }

  updateProfile(request: UpdateProfileRequest): Observable<ProfileOperationResult> {
    return this.http.put<ProfileOperationResult>(this.apiUrl, request);
  }

  saveProfile(profile: UserProfile): Observable<ProfileOperationResult> {
    return this.updateProfile({
      username: profile.username,
      email: profile.email,
      inNeedOfAid: profile.inNeedOfAid,
      emergencyLevel: profile.emergencyLevel,
      needsSurvivalAid: profile.needsSurvivalAid,
      paymentPlatforms: profile.paymentPlatforms
        .filter(p => p.handle.trim() && (p.platformId > 0 || p.customPlatformName?.trim()))
        .map(p => ({
          id: p.id > 0 ? p.id : 0,
          platformId: p.platformId === CUSTOM_PLATFORM_OPTION_ID ? 0 : p.platformId,
          customPlatformName: p.platformId === CUSTOM_PLATFORM_OPTION_ID ? p.customPlatformName?.trim() : undefined,
          platform: p.platform,
          handle: p.handle.trim(),
          isPreferred: !!p.isPreferred
        }))
    });
  }

  createPaymentPlatformAccount(): PaymentPlatformAccount {
    return {
      id: this.nextTempPlatformId--,
      platformId: CUSTOM_PLATFORM_OPTION_ID,
      platform: '',
      handle: '',
      customPlatformName: '',
      isPreferred: false
    };
  }

  addPaymentPlatform(profile: UserProfile): PaymentPlatformAccount {
    const account = this.createPaymentPlatformAccount();
    profile.paymentPlatforms = [...profile.paymentPlatforms, account];
    return account;
  }

  removePaymentPlatform(profile: UserProfile, accountId: number): void {
    profile.paymentPlatforms = profile.paymentPlatforms.filter(a => a.id !== accountId);
  }

  setPreferredPlatform(profile: UserProfile, accountId: number): void {
    profile.paymentPlatforms = profile.paymentPlatforms.map(account => ({
      ...account,
      isPreferred: account.id === accountId
    }));
  }

  isCustomPlatform(account: PaymentPlatformAccount): boolean {
    return account.platformId === CUSTOM_PLATFORM_OPTION_ID;
  }
}
