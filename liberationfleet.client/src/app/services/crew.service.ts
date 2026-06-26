import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateCrewRequest,
  CrewMembershipStatus,
  CrewOperationResult,
  CrewSearchResult,
  JoinCrewRequest,
  SearchCrewsRequest,
  UpdateCrewRequest
} from '../models/crew.model';
import { PaymentPlatformOption } from '../models/gift.model';

@Injectable({
  providedIn: 'root'
})
export class CrewService {
  private readonly apiUrl = '/api/crews';

  constructor(private http: HttpClient) {}

  getMembership(): Observable<CrewMembershipStatus> {
    return this.http.get<CrewMembershipStatus>(`${this.apiUrl}/membership`);
  }

  getCurrentCrew(): Observable<CrewOperationResult> {
    return this.http.get<CrewOperationResult>(`${this.apiUrl}/current`);
  }

  updateCrew(request: UpdateCrewRequest): Observable<CrewOperationResult> {
    return this.http.put<CrewOperationResult>(`${this.apiUrl}/current`, request);
  }

  leaveCrew(): Observable<CrewOperationResult> {
    return this.http.post<CrewOperationResult>(`${this.apiUrl}/leave`, {});
  }

  getPaymentPlatforms(otherCrewmatesOnly = false): Observable<PaymentPlatformOption[]> {
    const params = otherCrewmatesOnly ? { otherCrewmatesOnly: 'true' } : undefined;
    return this.http.get<PaymentPlatformOption[]>(`${this.apiUrl}/payment-platforms`, { params });
  }

  create(request: CreateCrewRequest): Observable<CrewOperationResult> {
    return this.http.post<CrewOperationResult>(this.apiUrl, request);
  }

  search(request: SearchCrewsRequest): Observable<CrewSearchResult> {
    return this.http.post<CrewSearchResult>(`${this.apiUrl}/search`, request);
  }

  join(request: JoinCrewRequest): Observable<CrewOperationResult> {
    if (request.joinCode?.trim()) {
      return this.http.post<CrewOperationResult>(`${this.apiUrl}/join`, {
        joinCode: request.joinCode.trim().toUpperCase()
      });
    }

    return this.http.post<CrewOperationResult>(`${this.apiUrl}/join`, {
      crewId: request.crewId
    });
  }
}
