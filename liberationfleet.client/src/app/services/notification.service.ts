import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import {
  HiddenContentItem,
  HiddenContentListResponse,
  MarkNotificationsReadByContentRequest,
  MutedContentItem,
  MutedContentListResponse,
  MutedContentType,
  NotificationBadgeSummaryResponse,
  NotificationFilterCategory,
  NotificationItem,
  NotificationListResponse,
  NotificationOperationResponse,
  NotificationPreference,
  NotificationPreferencesResponse,
  NotificationPreferencesUpdateRequest
} from '../models/notification.model';
import {
  CrewNotificationArea,
  CrewNotificationAreaCounts,
  emptyAreaCounts
} from '../utils/notification-area.util';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private readonly apiUrl = '/api/notifications';
  private unreadCountSubject = new BehaviorSubject<number>(0);
  readonly unreadCount$ = this.unreadCountSubject.asObservable();
  private areaCountsSubject = new BehaviorSubject<CrewNotificationAreaCounts>(emptyAreaCounts());
  readonly areaCounts$ = this.areaCountsSubject.asObservable();
  private resourceCountsSubject = new BehaviorSubject<Record<string, number>>({});
  readonly resourceCounts$ = this.resourceCountsSubject.asObservable();
  private lastBadgeRefreshAt = 0;
  private badgeInFlight = false;
  private readonly badgeMinIntervalMs = 12_000;

  constructor(private http: HttpClient) {}

  getNotifications(category: NotificationFilterCategory = 'All', beforeId?: number): Observable<NotificationListResponse> {
    let params = new HttpParams().set('category', category).set('limit', '50');
    if (beforeId) {
      params = params.set('beforeId', String(beforeId));
    }

    return this.http.get<NotificationListResponse>(this.apiUrl, { params }).pipe(
      tap(response => {
        if (response.success) {
          this.unreadCountSubject.next(response.unreadCount);
        }
      })
    );
  }

  /**
   * Refresh badge counts. Soft-throttled for routine nav remounts;
   * pass force=true for hub pushes / mark-read / mute changes.
   */
  refreshBadges(force = false): void {
    const now = Date.now();
    if (!force && (this.badgeInFlight || now - this.lastBadgeRefreshAt < this.badgeMinIntervalMs)) {
      return;
    }

    this.badgeInFlight = true;
    this.http.get<NotificationBadgeSummaryResponse>(`${this.apiUrl}/badges`).subscribe({
      next: response => {
        this.badgeInFlight = false;
        this.lastBadgeRefreshAt = Date.now();
        if (!response.success) {
          return;
        }

        this.unreadCountSubject.next(response.unreadCount);
        this.areaCountsSubject.next(this.toAreaCounts(response.areaCounts));
        this.resourceCountsSubject.next(response.resourceCounts ?? {});
      },
      error: () => {
        this.badgeInFlight = false;
      }
    });
  }

  clearSessionCache(): void {
    this.lastBadgeRefreshAt = 0;
    this.badgeInFlight = false;
    this.unreadCountSubject.next(0);
    this.areaCountsSubject.next(emptyAreaCounts());
    this.resourceCountsSubject.next({});
  }

  /** @deprecated Use refreshBadges() */
  refreshAreaCounts(): void {
    this.refreshBadges();
  }

  getPreferences(): Observable<NotificationPreferencesResponse> {
    return this.http.get<NotificationPreferencesResponse>(`${this.apiUrl}/preferences`);
  }

  updatePreferences(
    preferences: NotificationPreference[],
    settingsPassword?: string
  ): Observable<NotificationOperationResponse> {
    const body: NotificationPreferencesUpdateRequest = { preferences };
    if (settingsPassword) {
      body.settingsPassword = settingsPassword;
    }

    return this.http.put<NotificationOperationResponse>(`${this.apiUrl}/preferences`, body);
  }

  markRead(notificationId: number): Observable<NotificationOperationResponse> {
    return this.http.post<NotificationOperationResponse>(`${this.apiUrl}/${notificationId}/read`, {}).pipe(
      tap(response => {
        if (response.success) {
          this.unreadCountSubject.next(response.unreadCount);
          this.refreshBadges(true);
        }
      })
    );
  }

  markReadForContent(request: MarkNotificationsReadByContentRequest): Observable<NotificationOperationResponse> {
    return this.http.post<NotificationOperationResponse>(`${this.apiUrl}/read-by-content`, request).pipe(
      tap(response => {
        if (response.success) {
          this.unreadCountSubject.next(response.unreadCount);
          this.refreshBadges(true);
        }
      })
    );
  }

  markAllRead(): Observable<NotificationOperationResponse> {
    return this.http.post<NotificationOperationResponse>(`${this.apiUrl}/read-all`, {}).pipe(
      tap(response => {
        if (response.success) {
          this.unreadCountSubject.next(response.unreadCount);
          this.areaCountsSubject.next(emptyAreaCounts());
          this.resourceCountsSubject.next({});
        }
      })
    );
  }

  setUnreadCount(count: number) {
    this.unreadCountSubject.next(count);
  }

  handleIncoming(notification: NotificationItem) {
    this.unreadCountSubject.next(this.unreadCountSubject.value + (notification.isRead ? 0 : 1));
    if (!notification.isRead) {
      this.refreshBadges(true);
    }
  }

  resourceCount(key: string): number {
    return this.resourceCountsSubject.value[key] ?? 0;
  }

  getMutes(): Observable<MutedContentListResponse> {
    return this.http.get<MutedContentListResponse>(`${this.apiUrl}/mutes`);
  }

  setMute(contentType: MutedContentType, resourceId: number, muted: boolean): Observable<NotificationOperationResponse> {
    return this.http.put<NotificationOperationResponse>(`${this.apiUrl}/mutes`, {
      contentType,
      resourceId,
      muted
    }).pipe(
      tap(response => {
        if (response.success) {
          this.refreshBadges(true);
        }
      })
    );
  }

  isMuted(items: MutedContentItem[], contentType: MutedContentType, resourceId: number): boolean {
    return items.some(item => item.contentType === contentType && item.resourceId === resourceId);
  }

  getHidden(): Observable<HiddenContentListResponse> {
    return this.http.get<HiddenContentListResponse>(`${this.apiUrl}/hidden`);
  }

  setHidden(contentType: MutedContentType, resourceId: number, hidden: boolean): Observable<NotificationOperationResponse> {
    return this.http.put<NotificationOperationResponse>(`${this.apiUrl}/hidden`, {
      contentType,
      resourceId,
      hidden
    }).pipe(
      tap(response => {
        if (response.success) {
          this.refreshBadges(true);
        }
      })
    );
  }

  isHidden(items: HiddenContentItem[], contentType: MutedContentType, resourceId: number): boolean {
    return items.some(item => item.contentType === contentType && item.resourceId === resourceId);
  }

  private toAreaCounts(counts: Record<string, number>): CrewNotificationAreaCounts {
    return {
      crewChats: counts['crewChats'] ?? 0,
      fleetChats: counts['fleetChats'] ?? 0,
      crewForums: counts['crewForums'] ?? 0,
      fleetForums: counts['fleetForums'] ?? 0,
      crewProposals: counts['crewProposals'] ?? 0,
      fleetProposals: counts['fleetProposals'] ?? 0,
      crewGiftLog: counts['crewGiftLog'] ?? 0,
      fleetGiftLog: counts['fleetGiftLog'] ?? 0,
      crewRules: counts['crewRules'] ?? 0,
      fleetRules: counts['fleetRules'] ?? 0,
      crewSettings: counts['crewSettings'] ?? 0,
      fleetSettings: counts['fleetSettings'] ?? 0,
      crewLibrary: counts['crewLibrary'] ?? 0,
      fleetLibrary: counts['fleetLibrary'] ?? 0,
      crewCrewmates: counts['crewCrewmates'] ?? 0,
      fleetCrewmates: counts['fleetCrewmates'] ?? 0,
      fleet: counts['fleet'] ?? 0
    };
  }
}
