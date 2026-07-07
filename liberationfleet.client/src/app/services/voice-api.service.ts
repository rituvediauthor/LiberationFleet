import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  VoiceJoinResponse,
  VoiceOperationResponse,
  VoicePresenceSnapshot
} from '../models/voice.model';

@Injectable({
  providedIn: 'root'
})
export class VoiceApiService {
  private readonly apiUrl = '/api/chats';

  constructor(private http: HttpClient) {}

  joinVoiceRoom(roomId: number): Observable<VoiceJoinResponse> {
    return this.http.post<VoiceJoinResponse>(`${this.apiUrl}/rooms/${roomId}/voice/join`, {});
  }

  leaveVoiceRoom(roomId: number): Observable<VoiceOperationResponse> {
    return this.http.post<VoiceOperationResponse>(`${this.apiUrl}/rooms/${roomId}/voice/leave`, {});
  }

  getVoicePresence(crewId: number): Observable<VoicePresenceSnapshot> {
    const params = new HttpParams().set('crewId', String(crewId));
    return this.http.get<VoicePresenceSnapshot>(`${this.apiUrl}/voice/presence`, { params });
  }

  disconnectParticipant(roomId: number, userId: number): Observable<VoiceOperationResponse> {
    return this.http.post<VoiceOperationResponse>(`${this.apiUrl}/rooms/${roomId}/voice/disconnect`, { userId });
  }

  serverMuteParticipant(roomId: number, userId: number, isServerMuted: boolean): Observable<VoiceOperationResponse> {
    return this.http.post<VoiceOperationResponse>(`${this.apiUrl}/rooms/${roomId}/voice/server-mute`, {
      userId,
      isServerMuted
    });
  }
}
