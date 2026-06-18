import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, map } from 'rxjs';
import {
  CrewMember,
  GiftLogEntry,
  GiftLogResponse,
  GiftOperationResponse,
  GiftRecordItem,
  NextAidInfo,
  PaymentPlatformOption,
  PendingMiddlemanGift,
  ReceptionOrderEntry,
  RecordGiftRequest,
  SeasonReadyResult,
  SeasonSetupSaveResult,
  SeasonStatus
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

  navigateToGiftLogEntry(router: Router): void {
    this.getSeasonStatus().subscribe({
      next: status => {
        if (!status.seasonStarted) {
          router.navigate(['/app/crew/season-setup']);
        } else if (!status.userInSeason) {
          router.navigate(['/app/crew/join-season']);
        } else {
          router.navigate(['/app/crew/gift-log']);
        }
      },
      error: () => router.navigate(['/app/crew/gift-log'])
    });
  }

  getReceptionOrder(limit = 30): Observable<ReceptionOrderEntry[]> {
    return this.http.get<ReceptionOrderEntry[]>(`${this.apiUrl}/reception-order`, { params: { limit } });
  }

  getNextAidInfo(): Observable<NextAidInfo | null> {
    return this.getReceptionOrder(1).pipe(
      map(entries => {
        if (entries.length === 0) {
          return null;
        }
        return { recipientName: entries[0].username, amount: entries[0].amountNeeded };
      })
    );
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

  getLogs(): Observable<GiftLogEntry[]> {
    return this.http.get<GiftLogResponse>(`${this.apiUrl}/log`).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load gift log');
        }
        return response.items.map(entry => ({
          ...entry,
          timestamp: new Date(entry.timestamp)
        }));
      })
    );
  }

  isUserRelated(entry: GiftLogEntry, userId: number): boolean {
    return entry.relatedUserIds.includes(userId);
  }

  recordGifts(gifts: GiftRecordItem[]): Observable<GiftOperationResponse> {
    return this.http.post<GiftOperationResponse>(`${this.apiUrl}/batch`, { gifts });
  }

  completeMiddlemanGift(giftId: number): Observable<GiftOperationResponse> {
    return this.http.post<GiftOperationResponse>(`${this.apiUrl}/${giftId}/complete`, {});
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
}
