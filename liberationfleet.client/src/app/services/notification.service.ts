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

  refreshBadges(): void {
    this.http.get<NotificationBadgeSummaryResponse>(`${this.apiUrl}/badges`).subscribe({
      next: response => {
        if (!response.success) {
          return;
        }

        this.unreadCountSubject.next(response.unreadCount);
        this.areaCountsSubject.next(this.toAreaCounts(response.areaCounts));
        this.resourceCountsSubject.next(response.resourceCounts ?? {});
      }
    });
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
          this.refreshBadges();
        }
      })
    );
  }

  markReadForContent(request: MarkNotificationsReadByContentRequest): Observable<NotificationOperationResponse> {
    return this.http.post<NotificationOperationResponse>(`${this.apiUrl}/read-by-content`, request).pipe(
      tap(response => {
        if (response.success) {
          this.unreadCountSubject.next(response.unreadCount);
          this.refreshBadges();
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
      this.refreshBadges();
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
          this.refreshBadges();
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
          this.refreshBadges();
        }
      })
    );
  }

  isHidden(items: HiddenContentItem[], contentType: MutedContentType, resourceId: number): boolean {
    return items.some(item => item.contentType === contentType && item.resourceId === resourceId);
  }

  private toAreaCounts(counts: Record<string, number>): CrewNotificationAreaCounts {
    return {
      chats: counts['chats'] ?? 0,
      forums: counts['forums'] ?? 0,
      proposals: counts['proposals'] ?? 0,
      giftLog: counts['giftLog'] ?? 0,
      rules: counts['rules'] ?? 0,
      settings: counts['settings'] ?? 0,
      library: counts['library'] ?? 0,
      crewmates: counts['crewmates'] ?? 0,
      fleet: counts['fleet'] ?? 0
    };
  }
}
