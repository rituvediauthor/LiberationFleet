import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ChatMessageListResponse,
  ChatOperationResponse,
  ChatRoomDetailResponse,
  ChatRoomListResponse,
  CreateChatRoomRequest,
  DeleteChatRoomRequest,
  UpdateChatRoomRequest
} from '../models/chat.model';

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private readonly apiUrl = '/api/chats';

  constructor(private http: HttpClient) {}

  getRooms(): Observable<ChatRoomListResponse> {
    return this.http.get<ChatRoomListResponse>(`${this.apiUrl}/rooms`);
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
      plaintextName: request.plaintextName
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
    payload: { nonce: string; ciphertext: string; keyVersion?: number }
  ): Observable<ChatOperationResponse> {
    return this.http.post<ChatOperationResponse>(`${this.apiUrl}/rooms/${roomId}/messages`, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1
    });
  }

  updateMessage(
    roomId: number,
    messageId: number,
    payload: { nonce: string; ciphertext: string; keyVersion?: number }
  ): Observable<ChatOperationResponse> {
    return this.http.put<ChatOperationResponse>(`${this.apiUrl}/rooms/${roomId}/messages/${messageId}`, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1
    });
  }

  toggleAnonymousMode(roomId: number, enabled: boolean): Observable<ChatOperationResponse> {
    return this.http.put<ChatOperationResponse>(`${this.apiUrl}/rooms/${roomId}/anonymous-mode`, { enabled });
  }
}
