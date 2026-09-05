import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, of, shareReplay, tap, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  CreateCrewRequest,
  CrewInvitationDetailResponse,
  CrewInvitationListResponse,
  CrewInvitationOperationResponse,
  CrewMembershipStatus,
  CrewOperationResult,
  CrewSearchResult,
  InviteCandidateListResponse,
  JoinRequestListResponse,
  JoinRequestOperationResponse,
  PublicCrewRulesResponse,
  SearchCrewsRequest,
  SubmitJoinRequestBody,
  UpdateCrewRequest
} from '../models/crew.model';
import { PaymentPlatformOption } from '../models/gift.model';
import { ProfileService } from './profile.service';

@Injectable({
  providedIn: 'root'
})
export class CrewService {
  private readonly apiUrl = '/api/crews';
  private membershipRequest$: Observable<CrewMembershipStatus> | null = null;
  private readonly membershipChangedSubject = new Subject<void>();
  /** Emits when the session membership cache is cleared or replaced. */
  readonly membershipChanged$ = this.membershipChangedSubject.asObservable();

  private readonly http = inject(HttpClient);
  private readonly profileService = inject(ProfileService);

  /**
   * Session-cached membership. Concurrent callers share one in-flight request.
   * Pass forceRefresh after join/leave/create/settings changes.
   * Errors are not sticky-cached — a later call can retry.
   */
  getMembership(forceRefresh = false): Observable<CrewMembershipStatus> {
    if (forceRefresh) {
      this.clearMembershipCache();
    }
    if (!this.membershipRequest$) {
      this.membershipRequest$ = this.http.get<CrewMembershipStatus>(`${this.apiUrl}/membership`).pipe(
        catchError(err => {
          this.membershipRequest$ = null;
          return throwError(() => err);
        }),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }
    return this.membershipRequest$;
  }

  clearMembershipCache(): void {
    this.membershipRequest$ = null;
    // Stats (lifetime, priority, roles, etc.) are crew-scoped — drop stale profile cache.
    this.profileService.clearProfileCache();
    this.membershipChangedSubject.next();
  }

  /** Replace the session cache after a successful save (avoids an extra GET). */
  setCachedMembership(status: CrewMembershipStatus): void {
    this.membershipRequest$ = of(status).pipe(shareReplay({ bufferSize: 1, refCount: false }));
    this.membershipChangedSubject.next();
  }

  clearSessionCache(): void {
    this.membershipRequest$ = null;
  }

  getCurrentCrew(): Observable<CrewOperationResult> {
    return this.http.get<CrewOperationResult>(`${this.apiUrl}/current`);
  }

  updateCrew(request: UpdateCrewRequest): Observable<CrewOperationResult> {
    return this.http.put<CrewOperationResult>(`${this.apiUrl}/current`, request).pipe(
      tap(result => {
        if (!result.success) {
          return;
        }
        if (result.crew && !result.proposalsSubmitted) {
          // Silent clear — caller refreshes via setCachedMembership after image invalidation.
          this.membershipRequest$ = null;
          return;
        }
        this.clearMembershipCache();
      })
    );
  }

  leaveCrew(): Observable<CrewOperationResult> {
    return this.http.post<CrewOperationResult>(`${this.apiUrl}/leave`, {}).pipe(
      tap(result => {
        if (result.success) {
          this.clearMembershipCache();
        }
      })
    );
  }

  getPaymentPlatforms(otherCrewmatesOnly = false): Observable<PaymentPlatformOption[]> {
    const params = otherCrewmatesOnly ? { otherCrewmatesOnly: 'true' } : undefined;
    return this.http.get<PaymentPlatformOption[]>(`${this.apiUrl}/payment-platforms`, { params });
  }

  create(request: CreateCrewRequest): Observable<CrewOperationResult> {
    return this.http.post<CrewOperationResult>(this.apiUrl, request).pipe(
      tap(result => {
        if (result.success) {
          this.clearMembershipCache();
        }
      })
    );
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

  getInviteCandidates(username?: string, friendsOnly = false): Observable<InviteCandidateListResponse> {
    const params: Record<string, string> = {};
    if (username?.trim()) {
      params['username'] = username.trim();
    }
    if (friendsOnly) {
      params['friendsOnly'] = 'true';
    }
    return this.http.get<InviteCandidateListResponse>(`${this.apiUrl}/invite-candidates`, { params });
  }

  inviteCrewmate(userId: number): Observable<CrewInvitationOperationResponse> {
    return this.http.post<CrewInvitationOperationResponse>(`${this.apiUrl}/invitations`, { userId });
  }

  getMyInvitations(): Observable<CrewInvitationListResponse> {
    return this.http.get<CrewInvitationListResponse>(`${this.apiUrl}/invitations/mine`);
  }

  getInvitation(invitationId: number): Observable<CrewInvitationDetailResponse> {
    return this.http.get<CrewInvitationDetailResponse>(`${this.apiUrl}/invitations/${invitationId}`);
  }

  declineInvitation(invitationId: number): Observable<CrewInvitationOperationResponse> {
    return this.http.post<CrewInvitationOperationResponse>(
      `${this.apiUrl}/invitations/${invitationId}/decline`,
      {}
    );
  }
}
