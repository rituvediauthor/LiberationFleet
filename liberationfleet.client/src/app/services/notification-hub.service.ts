import { Injectable, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { NotificationBadgeSummaryResponse, NotificationItem } from '../models/notification.model';
import { AuthService } from './auth.service';
import { ApiUrlService } from './api-url.service';
import { NotificationService } from './notification.service';

@Injectable({
  providedIn: 'root'
})
export class NotificationHubService implements OnDestroy {
  private connection: HubConnection | null = null;
  private startPromise: Promise<void> | null = null;

  readonly notificationReceived$ = new Subject<NotificationItem>();
  readonly unreadCountUpdated$ = new Subject<number>();
  readonly badgeSummaryUpdated$ = new Subject<NotificationBadgeSummaryResponse>();

  constructor(
    private authService: AuthService,
    private notificationService: NotificationService,
    private apiUrl: ApiUrlService
  ) {}

  ngOnDestroy() {
    void this.disconnect();
  }

  async connect(): Promise<void> {
    if (!this.authService.getToken()) {
      return;
    }

    await this.ensureConnected();
  }

  async disconnect(): Promise<void> {
    this.startPromise = null;
    if (!this.connection) {
      return;
    }

    await this.connection.stop();
    this.connection = null;
  }

  private async ensureConnected(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    if (!this.startPromise) {
      this.startPromise = this.startConnection();
    }

    await this.startPromise;
  }

  private async startConnection(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(this.apiUrl.resolveHub('/hubs/notifications'), {
        accessTokenFactory: () => this.authService.getToken() ?? ''
      })
      .withAutomaticReconnect()
      .build();

    this.connection.on('NotificationReceived', (notification: NotificationItem) => {
      this.notificationService.handleIncoming(notification);
      this.notificationReceived$.next(notification);
      this.showBrowserNotification(notification);
    });

    this.connection.on('UnreadCountUpdated', (count: number) => {
      this.notificationService.setUnreadCount(count);
      this.unreadCountUpdated$.next(count);
    });

    this.connection.on('BadgeSummaryUpdated', (summary: NotificationBadgeSummaryResponse) => {
      // SignalR may camelCase or leave PascalCase depending on server config.
      const normalized = this.normalizeBadgeSummary(summary);
      this.notificationService.applyBadgeSummary(normalized);
      this.badgeSummaryUpdated$.next(normalized);
    });

    await this.connection.start();
  }

  private normalizeBadgeSummary(summary: NotificationBadgeSummaryResponse | Record<string, unknown>): NotificationBadgeSummaryResponse {
    const raw = summary as Record<string, unknown>;
    const unreadCount = (raw['unreadCount'] ?? raw['UnreadCount'] ?? 0) as number;
    const areaCounts = (raw['areaCounts'] ?? raw['AreaCounts'] ?? {}) as Record<string, number>;
    const resourceCounts = (raw['resourceCounts'] ?? raw['ResourceCounts'] ?? {}) as Record<string, number>;
    const success = (raw['success'] ?? raw['Success'] ?? true) as boolean;
    const message = (raw['message'] ?? raw['Message'] ?? '') as string;

    return {
      success,
      message,
      unreadCount,
      areaCounts,
      resourceCounts
    };
  }

  private showBrowserNotification(notification: NotificationItem) {
    if (typeof Notification === 'undefined' || Notification.permission !== 'granted') {
      return;
    }

    if (!document.hidden) {
      return;
    }

    const browserNotification = new Notification(notification.title, {
      body: notification.body
    });

    browserNotification.onclick = () => {
      window.focus();
      window.location.href = notification.actionUrl;
      browserNotification.close();
    };
  }
}
