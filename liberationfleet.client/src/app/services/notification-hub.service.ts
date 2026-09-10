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
      const normalized = this.normalizeNotification(notification);
      this.notificationService.handleIncoming(normalized);
      this.notificationReceived$.next(normalized);
      this.showBrowserNotification(normalized);
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
      59: 'LibraryTaskScheduleChanged'
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
