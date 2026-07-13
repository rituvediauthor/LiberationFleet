import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateFleetRequest,
  CrewLookupResponse,
  FleetCrewDetailResponse,
  FleetCrewListResponse,
  FleetCrewOperationResponse,
  FleetEmergencyListResponse,
  FleetGiftLogResponse,
  FleetJoinRequestListResponse,
  FleetJoinRequestOperationResponse,
  FleetNextAidResponse,
  FleetOperationResult,
  FleetReceptionOrderResponse,
  FleetRecordGiftsRequest,
  FleetRecordGiftsResponse,
  FleetRuleDetailResponse,
  FleetRuleListResponse,
  FleetRuleOperationResponse,
  FleetSearchResult,
  FleetStatus,
  InviteCrewToFleetResponse,
  PublicFleetRulesResponse,
  SearchFleetsRequest,
  SubmitFleetJoinRequestBody,
  UpdateFleetRequest,
  WriteFleetRuleBody
} from '../models/fleet.model';
import { ChatRoomListResponse } from '../models/chat.model';
import { GiftLogQueryOptions } from '../models/gift.model';

@Injectable({
  providedIn: 'root'
})
export class FleetService {
  private readonly apiUrl = '/api/fleets';

  constructor(private http: HttpClient) {}

  getStatus(): Observable<FleetStatus> {
    return this.http.get<FleetStatus>(`${this.apiUrl}/status`);
  }

  acceptRules(acceptedRuleIds: number[]): Observable<FleetOperationResult> {
    return this.http.post<FleetOperationResult>(`${this.apiUrl}/accept-rules`, { acceptedRuleIds });
  }

  lookupCrewByJoinCode(joinCode: string): Observable<CrewLookupResponse> {
    return this.http.get<CrewLookupResponse>(`${this.apiUrl}/lookup-crew`, {
      params: { joinCode: joinCode.trim().toUpperCase() }
    });
  }

  inviteCrew(joinCode: string): Observable<InviteCrewToFleetResponse> {
    return this.http.post<InviteCrewToFleetResponse>(`${this.apiUrl}/invite-crew`, {
      joinCode: joinCode.trim().toUpperCase()
    });
  }

  create(request: CreateFleetRequest): Observable<FleetOperationResult> {
    return this.http.post<FleetOperationResult>(this.apiUrl, request);
  }

  search(request: SearchFleetsRequest): Observable<FleetSearchResult> {
    return this.http.post<FleetSearchResult>(`${this.apiUrl}/search`, request);
  }

  getPublicRules(fleetId: number): Observable<PublicFleetRulesResponse> {
    return this.http.get<PublicFleetRulesResponse>(`${this.apiUrl}/${fleetId}/public-rules`);
  }

  getPublicRulesByJoinCode(joinCode: string): Observable<PublicFleetRulesResponse> {
    return this.http.get<PublicFleetRulesResponse>(`${this.apiUrl}/public-rules`, {
      params: { joinCode: joinCode.trim().toUpperCase() }
    });
  }

  submitJoinRequest(body: SubmitFleetJoinRequestBody): Observable<FleetJoinRequestOperationResponse> {
    return this.http.post<FleetJoinRequestOperationResponse>(`${this.apiUrl}/join-request`, body);
  }

  getMyJoinRequests(): Observable<FleetJoinRequestListResponse> {
    return this.http.get<FleetJoinRequestListResponse>(`${this.apiUrl}/join-requests/mine`);
  }

  getCurrent(): Observable<FleetOperationResult> {
    return this.http.get<FleetOperationResult>(`${this.apiUrl}/current`);
  }

  updateCurrent(request: UpdateFleetRequest): Observable<FleetOperationResult> {
    return this.http.put<FleetOperationResult>(`${this.apiUrl}/current`, request);
  }

  getCrews(): Observable<FleetCrewListResponse> {
    return this.http.get<FleetCrewListResponse>(`${this.apiUrl}/current/crews`);
  }

  getCrewDetail(crewId: number): Observable<FleetCrewDetailResponse> {
    return this.http.get<FleetCrewDetailResponse>(`${this.apiUrl}/current/crews/${crewId}`);
  }

  kickCrew(crewId: number, reason: string): Observable<FleetCrewOperationResponse> {
    return this.http.post<FleetCrewOperationResponse>(`${this.apiUrl}/current/crews/${crewId}/kick`, { reason });
  }

  joinCrew(crewId: number): Observable<FleetCrewOperationResponse> {
    return this.http.post<FleetCrewOperationResponse>(`${this.apiUrl}/current/crews/${crewId}/join`, {});
  }

  getGiftLog(options?: GiftLogQueryOptions): Observable<FleetGiftLogResponse> {
    const params: Record<string, string> = {};
    if (options?.limit != null) {
      params['limit'] = String(options.limit);
    }
    if (options?.beforeCreatedAt) {
      params['beforeCreatedAt'] = options.beforeCreatedAt;
    }
    if (options?.beforeId != null) {
      params['beforeId'] = String(options.beforeId);
    }
    return this.http.get<FleetGiftLogResponse>(`${this.apiUrl}/current/gift-log`, { params });
  }

  getReceptionOrder(): Observable<FleetReceptionOrderResponse> {
    return this.http.get<FleetReceptionOrderResponse>(`${this.apiUrl}/current/reception-order`);
  }

  recordGifts(request: FleetRecordGiftsRequest): Observable<FleetRecordGiftsResponse> {
    return this.http.post<FleetRecordGiftsResponse>(`${this.apiUrl}/current/gifts`, request);
  }

  getNextAid(): Observable<FleetNextAidResponse> {
    return this.http.get<FleetNextAidResponse>(`${this.apiUrl}/current/next-aid`);
  }

  getEmergencies(): Observable<FleetEmergencyListResponse> {
    return this.http.get<FleetEmergencyListResponse>(`${this.apiUrl}/current/emergencies`);
  }

  getChats(): Observable<ChatRoomListResponse> {
    return this.http.get<ChatRoomListResponse>(`${this.apiUrl}/current/chats`);
  }

  getRules(): Observable<FleetRuleListResponse> {
    return this.http.get<FleetRuleListResponse>(`${this.apiUrl}/current/rules`);
  }

  getRule(id: number): Observable<FleetRuleDetailResponse> {
    return this.http.get<FleetRuleDetailResponse>(`${this.apiUrl}/current/rules/${id}`);
  }

  createRule(body: WriteFleetRuleBody): Observable<FleetRuleOperationResponse> {
    return this.http.post<FleetRuleOperationResponse>(`${this.apiUrl}/current/rules`, body);
  }

  updateRule(id: number, body: WriteFleetRuleBody): Observable<FleetRuleOperationResponse> {
    return this.http.put<FleetRuleOperationResponse>(`${this.apiUrl}/current/rules/${id}`, body);
  }

  deleteRule(id: number): Observable<FleetRuleOperationResponse> {
    return this.http.delete<FleetRuleOperationResponse>(`${this.apiUrl}/current/rules/${id}`);
  }
}
