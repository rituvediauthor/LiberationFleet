import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { EncryptedContentSendPayload } from '../models/encrypted-send.model';
import {
  ChatMessageListResponse,
  ChatOperationResponse,
  ChatRoomDetailResponse,
  ChatRoomListResponse,
  CreateChatRoomRequest,
  DeleteChatRoomRequest,
  UpdateChatRoomRequest
} from '../models/chat.model';
import { ContentLiker, ContentLikersResponse } from '../models/gift.model';

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private readonly apiUrl = '/api/chats';

  constructor(private http: HttpClient) {}

  getRooms(): Observable<ChatRoomListResponse> {
    return this.http.get<ChatRoomListResponse>(`${this.apiUrl}/rooms`);
  }

  reorderRooms(request: {
    roomIds: number[];
    personal: boolean;
    scope?: 'crew' | 'fleet';
  }): Observable<ChatOperationResponse> {
    return this.http.put<ChatOperationResponse>(`${this.apiUrl}/rooms/order`, {
      roomIds: request.roomIds,
      personal: request.personal,
      scope: request.scope ?? 'crew'
    });
  }

  getRoom(roomId: number): Observable<ChatRoomDetailResponse> {
    return this.http.get<ChatRoomDetailResponse>(`${this.apiUrl}/rooms/${roomId}`);
  }

  createRoom(request: CreateChatRoomRequest): Observable<ChatOperationResponse> {
    return this.http.post<ChatOperationResponse>(`${this.apiUrl}/rooms`, {
      nonce: request.nonce,
      ciphertext: request.ciphertext,
      keyVersion: request.keyVersion ?? 1,
      roomType: request.roomType,
      purpose: request.purpose,
      plaintextName: request.plaintextName,
      isAdultContent: request.isAdultContent ?? false,
      scope: request.scope ?? 'crew'
    });
  }

  updateRoom(roomId: number, request: UpdateChatRoomRequest): Observable<ChatOperationResponse> {
    return this.http.put<ChatOperationResponse>(`${this.apiUrl}/rooms/${roomId}`, {
      nonce: request.nonce,
      ciphertext: request.ciphertext,
      keyVersion: request.keyVersion ?? 1,
      roomType: request.roomType,
      purpose: request.purpose,
      plaintextName: request.plaintextName,
      plaintextOldName: request.plaintextOldName,
      plaintextOldPurpose: request.plaintextOldPurpose
    });
  }

  deleteRoom(roomId: number, request: DeleteChatRoomRequest): Observable<ChatOperationResponse> {
    return this.http.delete<ChatOperationResponse>(`${this.apiUrl}/rooms/${roomId}`, { body: request });
  }

  getMessages(roomId: number, limit = 50, beforeMessageId?: number): Observable<ChatMessageListResponse> {
    let params = new HttpParams().set('limit', limit.toString());
    if (beforeMessageId != null) {
      params = params.set('beforeMessageId', beforeMessageId.toString());
    }
    return this.http.get<ChatMessageListResponse>(`${this.apiUrl}/rooms/${roomId}/messages`, { params });
  }

  sendMessage(
    roomId: number,
    payload: EncryptedContentSendPayload & { body?: string; isAnonymous?: boolean }
  ): Observable<ChatOperationResponse> {
    return this.http.post<ChatOperationResponse>(`${this.apiUrl}/rooms/${roomId}/messages`, {
      nonce: payload.nonce ?? '',
      ciphertext: payload.ciphertext ?? '',
      keyVersion: payload.keyVersion ?? 1,
      body: payload.body ?? payload.notificationPreview ?? null,
      mentionedUserIds: payload.mentionedUserIds ?? [],
      isAnonymous: !!payload.isAnonymous
    });
  }

  updateMessage(
    roomId: number,
    messageId: number,
    payload: EncryptedContentSendPayload & { body?: string }
  ): Observable<ChatOperationResponse> {
    return this.http.put<ChatOperationResponse>(`${this.apiUrl}/rooms/${roomId}/messages/${messageId}`, {
      nonce: payload.nonce ?? '',
      ciphertext: payload.ciphertext ?? '',
      keyVersion: payload.keyVersion ?? 1,
      body: payload.body ?? null,
      mentionedUserIds: payload.mentionedUserIds ?? []
    });
  }

  deleteMessage(roomId: number, messageId: number): Observable<ChatOperationResponse> {
    return this.http.delete<ChatOperationResponse>(`${this.apiUrl}/rooms/${roomId}/messages/${messageId}`);
  }

  toggleMessageLike(roomId: number, messageId: number): Observable<{
    success: boolean;
    message: string;
    liked?: boolean;
    likeCount?: number;
  }> {
    return this.http.post<{
      success: boolean;
      message: string;
      liked?: boolean;
      likeCount?: number;
    }>(`${this.apiUrl}/rooms/${roomId}/messages/${messageId}/like`, {});
  }

  getMessageLikers(roomId: number, messageId: number): Observable<ContentLiker[]> {
    return this.http.get<ContentLikersResponse>(
      `${this.apiUrl}/rooms/${roomId}/messages/${messageId}/likers`
    ).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load likers');
        }
        return response.items ?? [];
      })
    );
  }

  kickFromMessage(roomId: number, messageId: number, reason: string): Observable<ChatOperationResponse> {
    return this.http.post<ChatOperationResponse>(`${this.apiUrl}/rooms/${roomId}/messages/${messageId}/kick`, {
      reason
    });
  }
}
