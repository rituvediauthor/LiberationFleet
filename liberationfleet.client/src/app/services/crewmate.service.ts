import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import {
  AddPlaceholderCrewmateResponse,
  CrewmateKickResponse,
  CrewRoleChangeResponse,
  CrewRoleDefinitionsResponse,
  KickedCrewmateListResponse,
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

  unblockCrewmate(userId: number): Observable<CrewmateOperationResponse> {
    return this.http.delete<CrewmateOperationResponse>(`${this.apiUrl}/${userId}/block`)
      .pipe(map(response => this.mapOperation(response)));
  }

  kickCrewmate(userId: number, reason: string): Observable<CrewmateKickResponse> {
    return this.http.post<CrewmateKickResponse>(`${this.apiUrl}/${userId}/kick`, { reason });
  }

  getKickedCrewmates(): Observable<KickedCrewmateListResponse> {
    return this.http.get<KickedCrewmateListResponse>(`${this.apiUrl}/kicked`);
  }

  allowRejoin(userId: number): Observable<CrewmateKickResponse> {
    return this.http.post<CrewmateKickResponse>(`${this.apiUrl}/${userId}/allow-rejoin`, {});
  }

  getRoleDefinitions(): Observable<CrewRoleDefinitionsResponse> {
    return this.http.get<CrewRoleDefinitionsResponse>(`${this.apiUrl}/roles`);
  }

  nominateRoles(userId: number, roles: string[]): Observable<CrewRoleChangeResponse> {
    return this.http.post<CrewRoleChangeResponse>(`${this.apiUrl}/${userId}/nominate-roles`, { roles });
  }

  demoteRoles(userId: number, roles: string[]): Observable<CrewRoleChangeResponse> {
    return this.http.post<CrewRoleChangeResponse>(`${this.apiUrl}/${userId}/demote-roles`, { roles });
  }

  toggleCanAttachFiles(userId: number, canAttachFiles: boolean): Observable<CrewmateOperationResponse> {
    return this.http.put<CrewmateOperationResponse>(`${this.apiUrl}/${userId}/can-attach-files`, { canAttachFiles });
  }

  proposeAttachPermission(userId: number): Observable<CrewRoleChangeResponse> {
    return this.http.post<CrewRoleChangeResponse>(`${this.apiUrl}/${userId}/propose-attach-permission`, {});
  }

  proposeProposalPermission(userId: number): Observable<CrewRoleChangeResponse> {
    return this.http.post<CrewRoleChangeResponse>(`${this.apiUrl}/${userId}/propose-proposal-permission`, {});
  }

  exportCrewmateStates(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/export-states`, { responseType: 'blob' });
  }

  exportGiftLog(): Observable<Blob> {
    return this.http.get('/api/gifts/export', { responseType: 'blob' });
  }

  addPlaceholderCrewmate(
    name: string,
    paymentPlatforms: Array<{
      platformId: number;
      customPlatformName?: string;
      handle: string;
      isPreferred: boolean;
    }>
  ): Observable<AddPlaceholderCrewmateResponse> {
    return this.http.post<AddPlaceholderCrewmateResponse>(`${this.apiUrl}/placeholders`, {
      name,
      paymentPlatforms
    });
  }

  claimPlaceholderIdentity(userId: number): Observable<CrewmateKickResponse> {
    return this.http.post<CrewmateKickResponse>(`${this.apiUrl}/${userId}/claim-identity`, {});
  }

  private mapListItem(item: CrewmateListItem): CrewmateListItem {
    return {
      ...item,
      isSelf: !!item.isSelf,
      isPlaceholderMember: !!item.isPlaceholderMember,
      friendshipState: mapFriendshipState(item.friendshipState as unknown as number | string)
    };
  }

  private mapProfile(profile: CrewmateProfile): CrewmateProfile {
    return {
      ...profile,
      roles: profile.roles ?? [],
      electedRoles: profile.electedRoles ?? [],
      canAttachFiles: profile.canAttachFiles ?? false,
      canCreateProposals: profile.canCreateProposals ?? false,
      canAttachFilesToCrewContent: profile.canAttachFilesToCrewContent ?? false,
      canCreateCrewProposals: profile.canCreateCrewProposals ?? false,
      canProposeAttachFilesGrant: !!profile.canProposeAttachFilesGrant,
      canProposeCreateProposalsGrant: !!profile.canProposeCreateProposalsGrant,
      crewmateTenureDays: profile.crewmateTenureDays ?? 0,
      canToggleCanAttachFiles: !!profile.canToggleCanAttachFiles,
      canModerateAttachments: !!profile.canModerateAttachments,
      canExportCrewData: !!profile.canExportCrewData,
      isPlaceholderMember: !!profile.isPlaceholderMember,
      canClaimIdentity: !!profile.canClaimIdentity,
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
