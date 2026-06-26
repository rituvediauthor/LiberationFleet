import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import {
  CrewmateListItem,
  CrewmateListResponse,
  CrewmateOperationResponse,
  CrewmateProfile,
  CrewmateProfileResponse,
  mapFriendshipState
} from '../models/crewmate.model';

@Injectable({
  providedIn: 'root'
})
export class CrewmateService {
  private readonly apiUrl = '/api/crewmates';

  constructor(private http: HttpClient) {}

  getCrewmates(): Observable<CrewmateListResponse> {
    return this.http.get<CrewmateListResponse>(this.apiUrl).pipe(
      map(response => ({
        ...response,
        items: (response.items ?? []).map(item => this.mapListItem(item))
      }))
    );
  }

  getCrewmateProfile(userId: number): Observable<CrewmateProfileResponse> {
    return this.http.get<CrewmateProfileResponse>(`${this.apiUrl}/${userId}`).pipe(
      map(response => ({
        ...response,
        profile: response.profile ? this.mapProfile(response.profile) : null
      }))
    );
  }

  requestFriendship(userId: number): Observable<CrewmateOperationResponse> {
    return this.http.post<CrewmateOperationResponse>(`${this.apiUrl}/${userId}/friend-request`, {})
      .pipe(map(response => this.mapOperation(response)));
  }

  cancelFriendshipRequest(userId: number): Observable<CrewmateOperationResponse> {
    return this.http.delete<CrewmateOperationResponse>(`${this.apiUrl}/${userId}/friend-request`)
      .pipe(map(response => this.mapOperation(response)));
  }

  acceptFriendship(userId: number): Observable<CrewmateOperationResponse> {
    return this.http.post<CrewmateOperationResponse>(`${this.apiUrl}/${userId}/friend-request/accept`, {})
      .pipe(map(response => this.mapOperation(response)));
  }

  rejectFriendship(userId: number): Observable<CrewmateOperationResponse> {
    return this.http.post<CrewmateOperationResponse>(`${this.apiUrl}/${userId}/friend-request/reject`, {})
      .pipe(map(response => this.mapOperation(response)));
  }

  unfriend(userId: number): Observable<CrewmateOperationResponse> {
    return this.http.delete<CrewmateOperationResponse>(`${this.apiUrl}/${userId}/friendship`)
      .pipe(map(response => this.mapOperation(response)));
  }

  blockCrewmate(userId: number): Observable<CrewmateOperationResponse> {
    return this.http.post<CrewmateOperationResponse>(`${this.apiUrl}/${userId}/block`, {})
      .pipe(map(response => this.mapOperation(response)));
  }

  private mapListItem(item: CrewmateListItem): CrewmateListItem {
    return {
      ...item,
      isSelf: !!item.isSelf,
      friendshipState: mapFriendshipState(item.friendshipState as unknown as number | string)
    };
  }

  private mapProfile(profile: CrewmateProfile): CrewmateProfile {
    return {
      ...profile,
      roles: profile.roles ?? [],
      friendshipState: mapFriendshipState(profile.friendshipState as unknown as number | string)
    };
  }

  private mapOperation(response: CrewmateOperationResponse): CrewmateOperationResponse {
    return {
      ...response,
      friendshipState: mapFriendshipState(response.friendshipState as unknown as number | string)
    };
  }
}
