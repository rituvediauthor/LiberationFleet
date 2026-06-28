import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import {
  BlockedUserListResponse,
  DirectMessageListResponse,
  DirectMessageOperationResponse,
  FriendListResponse,
  FriendRequestListResponse,
  mapFriendRequestDirection,
  mapUserSearchResult,
  SendDirectMessageRequest,
  UserSearchResponse
} from '../models/friend.model';

@Injectable({
  providedIn: 'root'
})
export class FriendService {
  private readonly apiUrl = '/api/friends';

  constructor(private http: HttpClient) {}

  getFriends(search?: string): Observable<FriendListResponse> {
    let params = new HttpParams();
    if (search?.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http.get<FriendListResponse>(this.apiUrl, { params });
  }

  getRequests(): Observable<FriendRequestListResponse> {
    return this.http.get<FriendRequestListResponse>(`${this.apiUrl}/requests`).pipe(
      map(response => ({
        ...response,
        items: (response.items ?? []).map(item => ({
          ...item,
          direction: mapFriendRequestDirection(item.direction as unknown as number | string)
        }))
      }))
    );
  }

  getBlocked(): Observable<BlockedUserListResponse> {
    return this.http.get<BlockedUserListResponse>(`${this.apiUrl}/blocked`);
  }

  searchUsers(username: string): Observable<UserSearchResponse> {
    const params = new HttpParams().set('username', username);
    return this.http.get<UserSearchResponse>(`${this.apiUrl}/search`, { params }).pipe(
      map(response => ({
        ...response,
        items: (response.items ?? []).map(item => mapUserSearchResult(item))
      }))
    );
  }

  getMessages(friendUserId: number, limit = 50, beforeMessageId?: number): Observable<DirectMessageListResponse> {
    let params = new HttpParams().set('limit', limit);
    if (beforeMessageId) {
      params = params.set('beforeMessageId', beforeMessageId);
    }
    return this.http.get<DirectMessageListResponse>(`${this.apiUrl}/messages/${friendUserId}`, { params });
  }

  sendMessage(friendUserId: number, payload: SendDirectMessageRequest): Observable<DirectMessageOperationResponse> {
    return this.http.post<DirectMessageOperationResponse>(`${this.apiUrl}/messages/${friendUserId}`, payload);
  }

  updateMessage(
    friendUserId: number,
    messageId: number,
    payload: SendDirectMessageRequest
  ): Observable<DirectMessageOperationResponse> {
    return this.http.put<DirectMessageOperationResponse>(`${this.apiUrl}/messages/${friendUserId}/${messageId}`, payload);
  }
}
