import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  CrewMember,
  GiftLogEntry,
  GiftLogResponse,
  GiftOperationResponse,
  NextAidInfo,
  PaymentPlatformOption,
  PendingMiddlemanGift,
  RecordGiftRequest
} from '../models/gift.model';

@Injectable({
  providedIn: 'root'
})
export class GiftService {
  private readonly apiUrl = '/api/gifts';
  private readonly paymentPlatformsUrl = '/api/payment-platforms';

  constructor(private http: HttpClient) {}

  getNextAidInfo(): NextAidInfo {
    return { recipientName: 'Ritu', amount: 20 };
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
