import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, map } from 'rxjs';
import { EncryptedContentSendPayload } from '../models/encrypted-send.model';
import { CUSTOM_PLATFORM_OPTION_ID } from '../models/profile.model';
import {
  ContentLiker,
  ContentLikersResponse,
  CrewMember,
  GiftComment,
  GiftCommentRepliesResponse,
  GiftDetail,
  GiftDetailResponse,
  GiftEngagementOperationResponse,
  GiftLikeToggleResponse,
  GiftLogEntry,
  GiftLogPage,
  GiftLogQueryOptions,
  GiftLogResponse,
  GiftHistoryDetailResponse,
  GiftHistoryRecipientListResponse,
  GiftOperationResponse,
  GiftRecordItem,
  GiftVerificationAction,
  NextAidInfo,
  PaymentPlatformOption,
  PendingMiddlemanGift,
  ReceptionOrderEntry,
  RecordGiftRequest,
  SeasonProfile,
  SeasonProfileResponse,
  SeasonReadyResult,
  SeasonSetupSaveResult,
  SeasonStatus,
  UpdateSeasonProfileRequest
} from '../models/gift.model';

@Injectable({
  providedIn: 'root'
})
export class GiftService {
  private readonly apiUrl = '/api/gifts';
  private readonly seasonUrl = '/api/season';
  private readonly paymentPlatformsUrl = '/api/payment-platforms';

  constructor(private http: HttpClient) {}

  getSeasonStatus(): Observable<SeasonStatus> {
    return this.http.get<SeasonStatus>(`${this.seasonUrl}/status`);
  }

  markSeasonReady(): Observable<SeasonReadyResult> {
    return this.http.post<SeasonReadyResult>(`${this.seasonUrl}/ready`, {});
  }

  saveSeasonSetup(estimatedMonthlyContribution: number): Observable<SeasonSetupSaveResult> {
    return this.http.post<SeasonSetupSaveResult>(`${this.seasonUrl}/setup`, { estimatedMonthlyContribution });
  }

  clearSeasonReady(): Observable<SeasonSetupSaveResult> {
    return this.http.post<SeasonSetupSaveResult>(`${this.seasonUrl}/clear-ready`, {});
  }

  /** Navigate immediately using known season state when available (avoids blocking on /api/season/status). */
  navigateToGiftLogEntry(router: Router, seasonStarted?: boolean | null): void {
    if (seasonStarted === false) {
      void router.navigate(['/app/crew/season-setup']);
      return;
    }
    if (seasonStarted === true) {
      void router.navigate(['/app/crew/gift-log']);
      return;
    }

    this.getSeasonStatus().subscribe({
      next: status => {
        if (!status.seasonStarted) {
          void router.navigate(['/app/crew/season-setup']);
        } else {
          void router.navigate(['/app/crew/gift-log']);
        }
      },
      error: () => void router.navigate(['/app/crew/gift-log'])
    });
  }

  /** From the Next Aid widget: record gift if in season, otherwise join season. */
  navigateToNextAidAction(
    router: Router,
    scope: 'crew' | 'fleet' = 'crew',
    known?: { seasonStarted?: boolean | null; userInSeason?: boolean | null }
  ): void {
    const go = (seasonStarted: boolean, userInSeason: boolean) => {
      if (!seasonStarted || !userInSeason) {
        void router.navigate(['/app/crew/join-season']);
        return;
      }
      void router.navigate([
        scope === 'fleet' ? '/app/fleet/gift-log/record' : '/app/crew/gift-log/record'
      ]);
    };

    if (known && known.seasonStarted != null && known.userInSeason != null) {
      go(!!known.seasonStarted, !!known.userInSeason);
      return;
    }

    this.getSeasonStatus().subscribe({
      next: status => go(!!status.seasonStarted, !!status.userInSeason),
      error: () => void router.navigate(['/app/crew/join-season'])
    });
  }

  getReceptionOrder(limit = 30): Observable<ReceptionOrderEntry[]> {
    return this.http.get<ReceptionOrderEntry[]>(`${this.apiUrl}/reception-order`, { params: { limit } });
  }

  getNextAidInfo(): Observable<NextAidInfo | null> {
    return this.http.get<NextAidInfo | null>(`${this.apiUrl}/next-aid`);
  }

  getCrewMembers(activeUserId: number): Observable<CrewMember[]> {
    return this.http.get<CrewMember[]>(`${this.apiUrl}/members`).pipe(
      map(members => members.filter(m => m.id !== activeUserId))
    );
  }

  getPaymentPlatforms(): Observable<PaymentPlatformOption[]> {
    return this.http.get<PaymentPlatformOption[]>(this.paymentPlatformsUrl);
  }

  getPendingMiddlemanGifts(): Observable<PendingMiddlemanGift[]> {
    return this.http.get<PendingMiddlemanGift[]>(`${this.apiUrl}/pending-middleman`);
  }

  getMyGiftHistory(): Observable<GiftHistoryRecipientListResponse> {
    return this.http.get<GiftHistoryRecipientListResponse>(`${this.apiUrl}/my-history`);
  }

  getMyGiftHistoryForRecipient(recipientUserId: number): Observable<GiftHistoryDetailResponse> {
    return this.http.get<GiftHistoryDetailResponse>(`${this.apiUrl}/my-history/${recipientUserId}`);
  }

  getLogs(options?: GiftLogQueryOptions): Observable<GiftLogPage> {
    let params = new HttpParams().set('limit', (options?.limit ?? 50).toString());
    if (options?.beforeCreatedAt) {
      params = params.set('beforeCreatedAt', options.beforeCreatedAt);
    }
    if (options?.beforeId != null) {
      params = params.set('beforeId', options.beforeId.toString());
    }

    return this.http.get<GiftLogResponse>(`${this.apiUrl}/log`, { params }).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load gift log');
        }
        return {
          hasMore: response.hasMore,
          items: response.items.map(entry => ({
            ...entry,
            timestamp: new Date(entry.timestamp)
          }))
        };
      })
    );
  }

  isUserRelated(entry: GiftLogEntry, userId: number): boolean {
    return entry.relatedUserIds.includes(userId);
  }

  recordGifts(gifts: GiftRecordItem[]): Observable<GiftOperationResponse> {
    return this.http.post<GiftOperationResponse>(`${this.apiUrl}/batch`, { gifts });
  }

  completeMiddlemanGift(giftId: number, paymentPlatformId: number): Observable<GiftOperationResponse> {
    return this.http.post<GiftOperationResponse>(`${this.apiUrl}/${giftId}/complete`, { paymentPlatformId });
  }

  verifyGift(
    giftId: number,
    action: GiftVerificationAction,
    paymentPlatformId?: number
  ): Observable<GiftOperationResponse> {
    return this.http.post<GiftOperationResponse>(`${this.apiUrl}/${giftId}/verify`, {
      action,
      paymentPlatformId: paymentPlatformId ?? null
    });
  }

  recordGift(request: RecordGiftRequest): Observable<GiftOperationResponse> {
    const body: {
      amount: number;
      paymentPlatformId: number;
      recipientId: number | null;
      middlemanId: number | null;
      completingGiftId: number | null;
    } = {
      amount: request.amount,
      paymentPlatformId: request.paymentPlatformId,
      recipientId: null,
      middlemanId: null,
      completingGiftId: null
    };

    if (request.completingGiftId) {
      body.completingGiftId = request.completingGiftId;
    } else {
      body.recipientId = request.recipientId ?? null;
      body.middlemanId = request.middlemanId ?? null;
    }

    return this.http.post<GiftOperationResponse>(this.apiUrl, body);
  }

  getGiftDetail(giftId: number): Observable<GiftDetail> {
    return this.http.get<GiftDetailResponse>(`${this.apiUrl}/log/${giftId}`).pipe(
      map(response => {
        if (!response.success || !response.entry) {
          throw new Error(response.message || 'Failed to load gift');
        }
        return this.mapGiftDetail(response.entry);
      })
    );
  }

  getGiftCommentReplies(giftId: number, parentCommentId: number): Observable<GiftComment[]> {
    return this.http.get<GiftCommentRepliesResponse>(
      `${this.apiUrl}/log/${giftId}/comments/${parentCommentId}/replies`
    ).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load replies');
        }
        return response.items.map(comment => ({
          ...comment,
          createdAt: new Date(comment.createdAt)
        }));
      })
    );
  }

  createGiftComment(
    giftId: number,
    payload: EncryptedContentSendPayload & { parentCommentId?: number | null }
  ): Observable<GiftEngagementOperationResponse> {
    return this.http.post<GiftEngagementOperationResponse>(`${this.apiUrl}/log/${giftId}/comments`, {
      parentCommentId: payload.parentCommentId ?? null,
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? [],
      notificationPreview: payload.notificationPreview ?? payload.body ?? null
    });
  }

  toggleGiftLike(giftId: number): Observable<GiftLikeToggleResponse> {
    return this.http.post<GiftLikeToggleResponse>(`${this.apiUrl}/log/${giftId}/like`, {});
  }

  toggleGiftCommentLike(giftId: number, commentId: number): Observable<GiftLikeToggleResponse> {
    return this.http.post<GiftLikeToggleResponse>(
      `${this.apiUrl}/log/${giftId}/comments/${commentId}/like`,
      {}
    );
  }

  getGiftLikers(giftId: number): Observable<ContentLiker[]> {
    return this.http.get<ContentLikersResponse>(`${this.apiUrl}/log/${giftId}/likers`).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load likers');
        }
        return response.items ?? [];
      })
    );
  }

  getGiftCommentLikers(giftId: number, commentId: number): Observable<ContentLiker[]> {
    return this.http.get<ContentLikersResponse>(
      `${this.apiUrl}/log/${giftId}/comments/${commentId}/likers`
    ).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load likers');
        }
        return response.items ?? [];
      })
    );
  }

  getSeasonProfile(): Observable<SeasonProfile> {
    return this.http.get<SeasonProfileResponse>(`${this.apiUrl}/season-profile`).pipe(
      map(response => {
        if (!response.success || !response.profile) {
          throw new Error(response.message || 'Failed to load season profile');
        }
        return response.profile;
      })
    );
  }

  updateSeasonProfile(profile: UpdateSeasonProfileRequest): Observable<SeasonProfileResponse> {
    return this.http.put<SeasonProfileResponse>(`${this.apiUrl}/season-profile`, profile);
  }

  buildSeasonProfileRequest(
    profile: SeasonProfile,
    overrides?: Partial<UpdateSeasonProfileRequest>
  ): UpdateSeasonProfileRequest {
    return {
      paymentPlatforms: profile.paymentPlatforms
        .filter(p => p.handle.trim() && (p.platformId > 0 || p.customPlatformName?.trim()))
        .map(p => ({
          id: p.id > 0 ? p.id : 0,
          platformId: p.platformId === CUSTOM_PLATFORM_OPTION_ID ? 0 : p.platformId,
          customPlatformName: p.platformId === CUSTOM_PLATFORM_OPTION_ID ? p.customPlatformName?.trim() : undefined,
          platform: p.platform,
          handle: p.handle.trim(),
          isPreferred: !!p.isPreferred
        })),
      inNeedOfAid: profile.inNeedOfAid,
      emergencyLevel: profile.emergencyLevel,
      peopleRepresentedCount: profile.peopleRepresentedCount,
      disabilityLevel: profile.disabilityLevel,
      identityGroups: profile.identityGroups ?? [],
      needsSurvivalAid: profile.needsSurvivalAid,
      estimatedMonthlyContribution: profile.estimatedMonthlyContribution,
      ...overrides
    };
  }

  private mapGiftDetail(entry: GiftDetail): GiftDetail {
    return {
      ...entry,
      timestamp: entry.timestamp instanceof Date ? entry.timestamp : new Date(entry.timestamp),
      comments: (entry.comments ?? []).map(comment => ({
        ...comment,
        createdAt: comment.createdAt instanceof Date ? comment.createdAt : new Date(comment.createdAt)
      }))
    };
  }
}
