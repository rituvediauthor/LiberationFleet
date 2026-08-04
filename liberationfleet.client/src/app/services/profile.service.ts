import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, shareReplay, tap } from 'rxjs';
import {
  CUSTOM_PLATFORM_OPTION_ID,
  PaymentPlatformAccount,
  ProfileOperationResult,
  UpdateProfileRequest,
  UserProfile
} from '../models/profile.model';

@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  private readonly apiUrl = '/api/profile';
  private nextTempPlatformId = -1;
  private profileRequest$: Observable<UserProfile> | null = null;

  constructor(private http: HttpClient) {}

  /**
   * Session-cached profile. Concurrent callers share one in-flight request.
   * Pass forceRefresh after profile edits (or call clearProfileCache).
   */
  getProfile(forceRefresh = false): Observable<UserProfile> {
    if (forceRefresh) {
      this.clearProfileCache();
    }
    if (!this.profileRequest$) {
      this.profileRequest$ = this.http.get<UserProfile>(this.apiUrl).pipe(
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }
    return this.profileRequest$;
  }

  clearProfileCache(): void {
    this.profileRequest$ = null;
  }

  clearSessionCache(): void {
    this.clearProfileCache();
  }

  /** Replace the session cache after a successful save (avoids an extra GET). */
  setCachedProfile(profile: UserProfile): void {
    this.profileRequest$ = of(profile).pipe(shareReplay({ bufferSize: 1, refCount: false }));
  }

  updateProfile(request: UpdateProfileRequest): Observable<ProfileOperationResult> {
    return this.http.put<ProfileOperationResult>(this.apiUrl, request).pipe(
      tap(result => {
        if (result.success && result.profile) {
          this.setCachedProfile(result.profile);
        } else if (result.success) {
          this.clearProfileCache();
        }
      })
    );
  }

  saveProfile(profile: UserProfile): Observable<ProfileOperationResult> {
    return this.updateProfile({
      username: profile.username,
      email: profile.email,
      avatarResourceId: profile.avatarResourceId,
      inNeedOfAid: profile.inNeedOfAid,
      emergencyLevel: profile.emergencyLevel,
      peopleRepresentedCount: profile.peopleRepresentedCount,
      disabilityLevel: profile.disabilityLevel,
      identityGroups: profile.identityGroups ?? [],
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
