import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
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
  FleetLibraryStatus,
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
import {
  LibraryOfferingKind,
  LibraryUnitDetail,
  LibraryUnitDetailResponse,
  LibraryUnitListPage,
  LibraryUnitListResponse
} from '../models/library.model';
import {
  CreateFleetForumCommentRequest,
  CreateFleetForumRequest,
  FleetForumCommentRepliesResponse,
  FleetForumDetailResponse,
  FleetForumListResponse,
  FleetForumOperationResponse,
  UpdateFleetForumCommentRequest,
  UpdateFleetForumRequest
} from '../models/fleet-forum.model';

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

  getLibraryStatus(): Observable<FleetLibraryStatus> {
    return this.http.get<FleetLibraryStatus>(`${this.apiUrl}/current/library-status`);
  }

  getLibraryDurableUnits(options?: {
    search?: string;
    categoryIds?: number[];
    limit?: number;
    offset?: number;
  }): Observable<LibraryUnitListPage> {
    return this.getLibraryUnitListPage('durable-units', options);
  }

  getLibraryStockUnits(
    kind: Extract<LibraryOfferingKind, 'Consumable' | 'Service'>,
    options?: {
      search?: string;
      categoryIds?: number[];
      limit?: number;
      offset?: number;
    }
  ): Observable<LibraryUnitListPage> {
    return this.getLibraryUnitListPage('stock-units', options, kind);
  }

  getLibraryUnit(unitId: number): Observable<LibraryUnitDetail> {
    return this.http.get<LibraryUnitDetailResponse>(`${this.apiUrl}/current/library/units/${unitId}`).pipe(
      map(response => {
        if (!response.success || !response.item) {
          throw new Error(response.message || 'Failed to load item');
        }
        return response.item;
      })
    );
  }

  private getLibraryUnitListPage(
    path: string,
    options?: { search?: string; categoryIds?: number[]; limit?: number; offset?: number },
    kind?: Extract<LibraryOfferingKind, 'Consumable' | 'Service'>
  ): Observable<LibraryUnitListPage> {
    let params = new HttpParams();
    if (kind) {
      params = params.set('kind', kind);
    }
    if (options?.search?.trim()) {
      params = params.set('search', options.search.trim());
    }
    for (const categoryId of options?.categoryIds ?? []) {
      params = params.append('categoryIds', categoryId.toString());
    }
    if (options?.limit) {
      params = params.set('limit', options.limit.toString());
    }
    if (options?.offset) {
      params = params.set('offset', options.offset.toString());
    }

    return this.http.get<LibraryUnitListResponse>(`${this.apiUrl}/current/library/${path}`, { params }).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load offerings');
        }
        return { items: response.items, hasMore: response.hasMore };
      })
    );
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

  getForums(): Observable<FleetForumListResponse> {
    return this.http.get<FleetForumListResponse>(`${this.apiUrl}/current/forums`);
  }

  getForum(id: number): Observable<FleetForumDetailResponse> {
    return this.http.get<FleetForumDetailResponse>(`${this.apiUrl}/current/forums/${id}`);
  }

  getForumCommentReplies(postId: number, parentCommentId: number): Observable<FleetForumCommentRepliesResponse> {
    return this.http.get<FleetForumCommentRepliesResponse>(
      `${this.apiUrl}/current/forums/${postId}/comments/${parentCommentId}/replies`
    );
  }

  createForum(body: CreateFleetForumRequest): Observable<FleetForumOperationResponse> {
    return this.http.post<FleetForumOperationResponse>(`${this.apiUrl}/current/forums`, body);
  }

  updateForum(id: number, body: UpdateFleetForumRequest): Observable<FleetForumOperationResponse> {
    return this.http.put<FleetForumOperationResponse>(`${this.apiUrl}/current/forums/${id}`, body);
  }

  deleteForum(id: number): Observable<FleetForumOperationResponse> {
    return this.http.delete<FleetForumOperationResponse>(`${this.apiUrl}/current/forums/${id}`);
  }

  createForumComment(id: number, body: CreateFleetForumCommentRequest): Observable<FleetForumOperationResponse> {
    return this.http.post<FleetForumOperationResponse>(`${this.apiUrl}/current/forums/${id}/comments`, body);
  }

  updateForumComment(
    postId: number,
    commentId: number,
    body: UpdateFleetForumCommentRequest
  ): Observable<FleetForumOperationResponse> {
    return this.http.put<FleetForumOperationResponse>(
      `${this.apiUrl}/current/forums/${postId}/comments/${commentId}`,
      body
    );
  }
}
