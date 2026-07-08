import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import {
  HiddenContentItem,
  HiddenContentListResponse,
  MutedContentItem,
  MutedContentListResponse,
  MutedContentType,
  NotificationFilterCategory,
  NotificationItem,
  NotificationListResponse,
  NotificationOperationResponse,
  NotificationPreference,
  NotificationPreferencesResponse,
  NotificationPreferencesUpdateRequest
} from '../models/notification.model';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private readonly apiUrl = '/api/notifications';
  private unreadCountSubject = new BehaviorSubject<number>(0);
  readonly unreadCount$ = this.unreadCountSubject.asObservable();

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
        }
      })
    );
  }

  markAllRead(): Observable<NotificationOperationResponse> {
    return this.http.post<NotificationOperationResponse>(`${this.apiUrl}/read-all`, {}).pipe(
      tap(response => {
        if (response.success) {
          this.unreadCountSubject.next(response.unreadCount);
        }
      })
    );
  }

  setUnreadCount(count: number) {
    this.unreadCountSubject.next(count);
  }

  handleIncoming(notification: NotificationItem) {
    this.unreadCountSubject.next(this.unreadCountSubject.value + (notification.isRead ? 0 : 1));
  }

  getMutes(): Observable<MutedContentListResponse> {
    return this.http.get<MutedContentListResponse>(`${this.apiUrl}/mutes`);
  }

  setMute(contentType: MutedContentType, resourceId: number, muted: boolean): Observable<NotificationOperationResponse> {
    return this.http.put<NotificationOperationResponse>(`${this.apiUrl}/mutes`, {
      contentType,
      resourceId,
      muted
    });
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
    });
  }

  isHidden(items: HiddenContentItem[], contentType: MutedContentType, resourceId: number): boolean {
    return items.some(item => item.contentType === contentType && item.resourceId === resourceId);
  }
}
