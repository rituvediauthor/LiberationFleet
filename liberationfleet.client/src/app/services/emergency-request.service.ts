import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  EmergencyRequestDetailResponse,
  EmergencyRequestListResponse,
  EmergencyRequestOperationResponse
} from '../models/emergency-request.model';

@Injectable({
  providedIn: 'root'
})
export class EmergencyRequestService {
  private readonly apiUrl = '/api/emergency-requests';

  constructor(private http: HttpClient) {}

  getList(): Observable<EmergencyRequestListResponse> {
    return this.http.get<EmergencyRequestListResponse>(this.apiUrl);
  }

  getDetail(id: number): Observable<EmergencyRequestDetailResponse> {
    return this.http.get<EmergencyRequestDetailResponse>(`${this.apiUrl}/${id}`);
  }

  create(purpose: string, amountNeeded: number): Observable<EmergencyRequestOperationResponse> {
    return this.http.post<EmergencyRequestOperationResponse>(this.apiUrl, { purpose, amountNeeded });
  }

  recordGift(
    id: number,
    amount: number,
    paymentPlatformId: number,
    middlemanId?: number
  ): Observable<EmergencyRequestOperationResponse> {
    return this.http.post<EmergencyRequestOperationResponse>(`${this.apiUrl}/${id}/record-gift`, {
      amount,
      paymentPlatformId,
      middlemanId: middlemanId ?? null
    });
  }

  markAlreadyLogged(id: number, amount: number): Observable<EmergencyRequestOperationResponse> {
    return this.http.post<EmergencyRequestOperationResponse>(`${this.apiUrl}/${id}/already-logged`, { amount });
  }

  splitCycle(id: number, amount: number): Observable<EmergencyRequestOperationResponse> {
    return this.http.post<EmergencyRequestOperationResponse>(`${this.apiUrl}/${id}/split-cycle`, { amount });
  }
}
