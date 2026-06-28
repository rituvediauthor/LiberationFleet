import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateCrewRequest,
  CrewMembershipStatus,
  CrewOperationResult,
  CrewSearchResult,
  JoinRequestListResponse,
  JoinRequestOperationResponse,
  PublicCrewRulesResponse,
  SearchCrewsRequest,
  SubmitJoinRequestBody,
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

  getPublicRules(crewId: number): Observable<PublicCrewRulesResponse> {
    return this.http.get<PublicCrewRulesResponse>(`${this.apiUrl}/${crewId}/public-rules`);
  }

  getPublicRulesByJoinCode(joinCode: string): Observable<PublicCrewRulesResponse> {
    return this.http.get<PublicCrewRulesResponse>(`${this.apiUrl}/public-rules`, {
      params: { joinCode: joinCode.trim().toUpperCase() }
    });
  }

  submitJoinRequest(body: SubmitJoinRequestBody): Observable<JoinRequestOperationResponse> {
    return this.http.post<JoinRequestOperationResponse>(`${this.apiUrl}/join-request`, body);
  }

  getMyJoinRequests(): Observable<JoinRequestListResponse> {
    return this.http.get<JoinRequestListResponse>(`${this.apiUrl}/join-requests/mine`);
  }
}
