import { Injectable, NgZone, OnDestroy } from '@angular/core';
import {
  HttpTransportType,
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState
} from '@microsoft/signalr';
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
  /** True while the authenticated app wants a live notification socket. */
  private wantsConnection = false;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private healthTimer: ReturnType<typeof setInterval> | null = null;
  private readonly healthIntervalMs = 30_000;

  readonly notificationReceived$ = new Subject<NotificationItem>();
  readonly unreadCountUpdated$ = new Subject<number>();
  readonly badgeSummaryUpdated$ = new Subject<NotificationBadgeSummaryResponse>();
  /** Fires after a fresh connect or automatic reconnect — list UIs can refetch. */
  readonly connectionRestored$ = new Subject<void>();

  private readonly onVisibilityChange = () => {
    if (typeof document !== 'undefined' && document.visibilityState === 'visible') {
      void this.recoverAfterResume();
    }
  };

  private readonly onPageShow = (event: PageTransitionEvent) => {
    // bfcache restore often leaves sockets dead without a visibility flip.
    if (event.persisted) {
      void this.recoverAfterResume();
    }
  };

  constructor(
    private authService: AuthService,
    private notificationService: NotificationService,
    private apiUrl: ApiUrlService,
    private ngZone: NgZone
  ) {
    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', this.onVisibilityChange);
    }
    if (typeof window !== 'undefined') {
      window.addEventListener('pageshow', this.onPageShow);
    }
  }

  ngOnDestroy() {
    if (typeof document !== 'undefined') {
      document.removeEventListener('visibilitychange', this.onVisibilityChange);
    }
    if (typeof window !== 'undefined') {
      window.removeEventListener('pageshow', this.onPageShow);
    }
    this.clearRetryTimer();
    this.stopHealthTimer();
    void this.disconnect();
  }

  async connect(): Promise<void> {
    if (!this.authService.getToken()) {
      return;
    }

    this.wantsConnection = true;
    this.startHealthTimer();
    await this.ensureConnected();
  }

  async disconnect(): Promise<void> {
    this.wantsConnection = false;
    this.clearRetryTimer();
    this.stopHealthTimer();
    this.startPromise = null;
    if (!this.connection) {
      return;
    }

    await this.connection.stop();
    this.connection = null;
  }

  private async recoverAfterResume(): Promise<void> {
    if (!this.wantsConnection || !this.authService.getToken()) {
      return;
    }

    try {
      await this.ensureConnected();
      // Missed SignalR pushes while suspended — sync badges from HTTP.
      this.ngZone.run(() => this.notificationService.refreshBadges(true));
      this.ngZone.run(() => this.connectionRestored$.next());
    } catch {
      // Resume is best-effort; health timer / next connect will retry.
      this.scheduleReconnectRetry();
    }
  }

  private async ensureConnected(): Promise<void> {
    if (this.isConnected()) {
      return;
    }

    // Automatic reconnect in progress — wait instead of tearing the socket down.
    if (this.connection?.state === HubConnectionState.Reconnecting) {
      await this.waitForConnected(15_000);
      if (this.isConnected()) {
        return;
      }
    }

    if (!this.startPromise) {
      this.startPromise = this.startConnection().catch(error => {
        this.startPromise = null;
        this.connection = null;
        throw error;
      });
    }

    await this.startPromise;

    if (this.isConnected()) {
      return;
    }

    this.startPromise = this.startConnection().catch(error => {
      this.startPromise = null;
      this.connection = null;
      throw error;
    });
    await this.startPromise;
  }

  private isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  private waitForConnected(timeoutMs: number): Promise<void> {
    return new Promise(resolve => {
      const started = Date.now();
      const tick = () => {
        const state = this.connection?.state;
        if (state === HubConnectionState.Connected
          || state === HubConnectionState.Disconnected
          || Date.now() - started >= timeoutMs) {
          resolve();
          return;
        }
        setTimeout(tick, 100);
      };
      tick();
    });
  }

  private async startConnection(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
    }

    // Skip SSE — Android WebViews often stall there after WebSockets fail.
    this.connection = new HubConnectionBuilder()
      .withUrl(this.apiUrl.resolveHub('/hubs/notifications'), {
        accessTokenFactory: () => this.authService.getToken() ?? '',
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling
      })
      // Keep trying longer than the previous short burst; onclose also schedules retries.
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10_000, 15_000, 30_000, 60_000])
      .build();

    this.connection.on('NotificationReceived', (notification: NotificationItem) => {
      this.ngZone.run(() => {
        const normalized = this.normalizeNotification(notification);
        this.notificationService.handleIncoming(normalized);
        this.notificationReceived$.next(normalized);
        this.showBrowserNotification(normalized);
      });
    });

    this.connection.on('UnreadCountUpdated', (count: number) => {
      this.ngZone.run(() => {
        this.notificationService.setUnreadCount(count);
        this.unreadCountUpdated$.next(count);
      });
    });

    this.connection.on('BadgeSummaryUpdated', (summary: NotificationBadgeSummaryResponse) => {
      this.ngZone.run(() => {
        // SignalR may camelCase or leave PascalCase depending on server config.
        const normalized = this.normalizeBadgeSummary(summary);
        this.notificationService.applyBadgeSummary(normalized);
        this.badgeSummaryUpdated$.next(normalized);
      });
    });

    this.connection.onreconnected(() => {
      this.ngZone.run(() => {
        this.notificationService.refreshBadges(true);
        this.connectionRestored$.next();
      });
    });

    this.connection.onclose(() => {
      // Reconnect gave up — allow the next ensureConnected() to start a fresh socket.
      this.startPromise = null;
      if (this.wantsConnection) {
        this.scheduleReconnectRetry();
      }
    });

    await this.connection.start();
    this.clearRetryTimer();
    this.ngZone.run(() => {
      this.notificationService.refreshBadges(true);
      this.connectionRestored$.next();
    });
  }

  private scheduleReconnectRetry(): void {
    if (this.retryTimer != null || !this.wantsConnection) {
      return;
    }

    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      if (!this.wantsConnection || this.isConnected() || !this.authService.getToken()) {
        return;
      }
      void this.ensureConnected().catch(() => this.scheduleReconnectRetry());
    }, 5_000);
  }

  private clearRetryTimer(): void {
    if (this.retryTimer != null) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
  }

  /**
   * While the app is open, periodically ensure the hub is alive and resync badges.
   * Mobile PWAs often exhaust SignalR reconnect and then sit silent until navigation.
   */
  private startHealthTimer(): void {
    if (this.healthTimer != null || typeof document === 'undefined') {
      return;
    }

    this.healthTimer = setInterval(() => {
      if (!this.wantsConnection || !this.authService.getToken()) {
        return;
      }
      if (typeof document !== 'undefined' && document.visibilityState === 'hidden') {
        return;
      }

      void this.ensureConnected()
        .then(() => {
          this.ngZone.run(() => this.notificationService.refreshBadges(false));
        })
        .catch(() => {
          this.scheduleReconnectRetry();
        });
    }, this.healthIntervalMs);
  }

  private stopHealthTimer(): void {
    if (this.healthTimer != null) {
      clearInterval(this.healthTimer);
      this.healthTimer = null;
    }
  }

  /** SignalR may use PascalCase and/or numeric enums depending on server protocol config. */
  private normalizeNotification(notification: NotificationItem | Record<string, unknown>): NotificationItem {
    const raw = notification as Record<string, unknown>;
    const kindRaw = raw['kind'] ?? raw['Kind'];
    const kind = typeof kindRaw === 'number'
      ? this.kindFromNumber(kindRaw)
      : String(kindRaw ?? '');

    return {
      id: Number(raw['id'] ?? raw['Id'] ?? 0),
      crewId: (raw['crewId'] ?? raw['CrewId'] ?? null) as number | null,
      kind: kind as NotificationItem['kind'],
      title: String(raw['title'] ?? raw['Title'] ?? ''),
      body: String(raw['body'] ?? raw['Body'] ?? ''),
      actionUrl: String(raw['actionUrl'] ?? raw['ActionUrl'] ?? ''),
      relatedEntityId: (raw['relatedEntityId'] ?? raw['RelatedEntityId'] ?? null) as number | null,
      secondaryEntityId: (raw['secondaryEntityId'] ?? raw['SecondaryEntityId'] ?? null) as number | null,
      actorUserId: (raw['actorUserId'] ?? raw['ActorUserId'] ?? null) as number | null,
      actorAvatarResourceId: (raw['actorAvatarResourceId'] ?? raw['ActorAvatarResourceId'] ?? null) as string | null,
      isRead: Boolean(raw['isRead'] ?? raw['IsRead'] ?? false),
      createdAt: String(raw['createdAt'] ?? raw['CreatedAt'] ?? ''),
      isTargetAvailable: (raw['isTargetAvailable'] ?? raw['IsTargetAvailable'] ?? null) as boolean | null
    };
  }

  private kindFromNumber(value: number): string {
    // Keep in sync with LiberationFleet.Server Domain.Enums.NotificationKind.
    const map: Record<number, string> = {
      1: 'NewProposal',
      2: 'ProposalRejected',
      3: 'ProposalAccepted',
      4: 'NewGifts',
      5: 'NewCycle',
      6: 'NewSeason',
      7: 'NewChatMessage',
      8: 'NewReply',
      9: 'NewForumPost',
      11: 'NewForumComment',
      13: 'NewCrewmate',
      14: 'JoinRequestFromPerson',
      15: 'JoinRequestFromCrew',
      16: 'NewRule',
      17: 'RuleDeleted',
      18: 'RuleEdited',
      19: 'CrewSettingChanged',
      20: 'CrewmateKicked',
      21: 'Mention',
      22: 'CrewmateRejoinAllowed',
      23: 'NewLibraryRequest',
      24: 'LibraryRequestDenied',
      25: 'LibraryRequestCompleted',
      26: 'NewLibraryRequestMessage',
      27: 'LibraryUnitBrokenReported',
      28: 'LibraryUnitBrokenConfirmed',
      29: 'LibraryUnitReportedFixed',
      30: 'SurvivalThresholdsRefreshed',
      31: 'NewFleetGifts',
      32: 'NewFleetProposal',
      33: 'FleetSettingChanged',
      34: 'NewFleetChatMessage',
      35: 'NewFleetForumPost',
      36: 'NewFleetForumComment',
      37: 'NewEmergencyRequest',
      38: 'ForumPostLiked',
      39: 'ForumCommentLiked',
      40: 'NewFleetRule',
      41: 'FleetRuleDeleted',
      42: 'FleetRuleEdited',
      43: 'FleetProposalAccepted',
      44: 'FleetProposalRejected',
      45: 'NewFleetReply',
      46: 'FleetMention',
      47: 'FleetForumPostLiked',
      48: 'FleetForumCommentLiked',
      49: 'NewGiftComment',
      50: 'GiftEntryLiked',
      51: 'GiftCommentLiked',
      52: 'NewGiftReply',
      53: 'NewProposalReply',
      54: 'NewFleetProposalReply',
      55: 'FriendRequest',
      56: 'FriendRequestAccepted',
      57: 'NewDirectMessage',
      58: 'ChatMessageLiked',
      59: 'LibraryTaskScheduleChanged',
      60: 'CrewJoinSwitchOffer'
    };
    return map[value] ?? String(value);
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
