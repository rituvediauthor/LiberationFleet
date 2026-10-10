import { Injectable, NgZone, OnDestroy } from '@angular/core';
import {
  HttpTransportType,
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { ChatMessage, ChatRoomListItem } from '../models/chat.model';
import { AuthService } from './auth.service';
import { ApiUrlService } from './api-url.service';

export interface ChatRoomActivityUpdate {
  roomId: number;
  lastActivityAt: string;
}

export interface DirectMessageReceivedEvent {
  friendUserId: number;
  message: ChatMessage;
}

export type TypingScope = 'room' | 'direct' | 'libraryRequest';

export interface TypingEvent {
  scope: TypingScope;
  roomId?: number;
  friendUserId?: number;
  requestId?: number;
  userId?: number | null;
  displayName: string;
  isAnonymous?: boolean;
  isTyping: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ChatHubService implements OnDestroy {
  private connection: HubConnection | null = null;
  private startPromise: Promise<void> | null = null;
  private joinedCrewId: number | null = null;
  private joinedFleetId: number | null = null;
  private joinedRoomId: number | null = null;
  private readonly onVisibilityChange = () => {
    if (typeof document !== 'undefined' && document.visibilityState === 'visible') {
      void this.recoverAfterResume();
    }
  };

  readonly messageReceived$ = new Subject<ChatMessage>();
  readonly messageUpdated$ = new Subject<ChatMessage>();
  readonly messageDeleted$ = new Subject<{ roomId: number; messageId: number }>();
  readonly roomCreated$ = new Subject<ChatRoomListItem>();
  readonly roomActivityUpdated$ = new Subject<ChatRoomActivityUpdate>();
  readonly directMessageReceived$ = new Subject<DirectMessageReceivedEvent>();
  readonly directMessageUpdated$ = new Subject<DirectMessageReceivedEvent>();
  readonly typing$ = new Subject<TypingEvent>();

  constructor(
    private authService: AuthService,
    private apiUrl: ApiUrlService,
    private ngZone: NgZone
  ) {
    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', this.onVisibilityChange);
    }
  }

  ngOnDestroy() {
    if (typeof document !== 'undefined') {
      document.removeEventListener('visibilitychange', this.onVisibilityChange);
    }
    void this.disconnect();
  }

  async joinCrew(crewId: number): Promise<void> {
    const connection = await this.ensureConnected();
    if (this.joinedCrewId === crewId) {
      return;
    }

    if (this.joinedCrewId != null) {
      await connection.invoke('LeaveCrew', this.joinedCrewId);
    }

    await connection.invoke('JoinCrew', crewId);
    this.joinedCrewId = crewId;
  }

  async joinFleet(fleetId: number): Promise<void> {
    const connection = await this.ensureConnected();
    if (this.joinedFleetId === fleetId) {
      return;
    }

    if (this.joinedFleetId != null) {
      await connection.invoke('LeaveFleet', this.joinedFleetId);
    }

    await connection.invoke('JoinFleet', fleetId);
    this.joinedFleetId = fleetId;
  }

  /** Connect to the chat hub (user group) without joining a crew room — used for DMs. */
  async ensureConnected(): Promise<HubConnection> {
    return this.ensureConnectedInternal();
  }

  async joinRoom(roomId: number): Promise<void> {
    const connection = await this.ensureConnectedInternal();
    if (this.joinedRoomId === roomId) {
      return;
    }

    if (this.joinedRoomId != null) {
      await connection.invoke('LeaveRoom', this.joinedRoomId);
    }

    await connection.invoke('JoinRoom', roomId);
    this.joinedRoomId = roomId;
  }

  async leaveRoom(): Promise<void> {
    if (this.connection?.state !== HubConnectionState.Connected || this.joinedRoomId == null) {
      this.joinedRoomId = null;
      return;
    }

    await this.connection.invoke('LeaveRoom', this.joinedRoomId);
    this.joinedRoomId = null;
  }

  async sendRoomTyping(roomId: number, isTyping: boolean, isAnonymous: boolean): Promise<void> {
    if (roomId <= 0) {
      return;
    }
    try {
      const connection = await this.ensureConnectedInternal();
      await connection.invoke('SendRoomTyping', roomId, isTyping, isAnonymous);
    } catch {
      // Ephemeral — ignore transient hub failures.
    }
  }

  async sendDirectTyping(friendUserId: number, isTyping: boolean): Promise<void> {
    if (friendUserId <= 0) {
      return;
    }
    try {
      const connection = await this.ensureConnectedInternal();
      await connection.invoke('SendDirectTyping', friendUserId, isTyping);
    } catch {
      // Ephemeral — ignore transient hub failures.
    }
  }

  async sendLibraryRequestTyping(requestId: number, isTyping: boolean): Promise<void> {
    if (requestId <= 0) {
      return;
    }
    try {
      const connection = await this.ensureConnectedInternal();
      await connection.invoke('SendLibraryRequestTyping', requestId, isTyping);
    } catch {
      // Ephemeral — ignore transient hub failures.
    }
  }

  async disconnect(): Promise<void> {
    this.joinedCrewId = null;
    this.joinedFleetId = null;
    this.joinedRoomId = null;
    this.startPromise = null;

    if (!this.connection) {
      return;
    }

    await this.connection.stop();
    this.connection = null;
  }

  private async recoverAfterResume(): Promise<void> {
    if (!this.authService.getToken()) {
      return;
    }

    const wantsHub =
      this.connection != null
      || this.joinedCrewId != null
      || this.joinedFleetId != null
      || this.joinedRoomId != null;
    if (!wantsHub) {
      return;
    }

    try {
      await this.ensureConnectedInternal();
      await this.rejoinDesiredGroups();
    } catch {
      // Resume is best-effort; next explicit join/send will retry.
    }
  }

  private async ensureConnectedInternal(): Promise<HubConnection> {
    const connected = this.getConnectedOrNull();
    if (connected) {
      return connected;
    }

    // Automatic reconnect in progress — wait instead of tearing the socket down.
    if (this.connection?.state === HubConnectionState.Reconnecting) {
      await this.waitForConnected(15_000);
      const afterReconnect = this.getConnectedOrNull();
      if (afterReconnect) {
        return afterReconnect;
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

    const afterStart = this.getConnectedOrNull();
    if (afterStart) {
      return afterStart;
    }

    // Start resolved but socket later dropped — begin a fresh connection.
    this.startPromise = this.startConnection().catch(error => {
      this.startPromise = null;
      this.connection = null;
      throw error;
    });
    await this.startPromise;

    const retry = this.getConnectedOrNull();
    if (!retry) {
      throw new Error('Chat hub failed to connect.');
    }
    return retry;
  }

  private getConnectedOrNull(): HubConnection | null {
    return this.connection?.state === HubConnectionState.Connected ? this.connection : null;
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

  private async rejoinDesiredGroups(): Promise<void> {
    const connection = this.connection;
    if (!connection || connection.state !== HubConnectionState.Connected) {
      return;
    }

    const crewId = this.joinedCrewId;
    const fleetId = this.joinedFleetId;
    const roomId = this.joinedRoomId;

    // Hub groups are not preserved across reconnect — force re-join.
    this.joinedCrewId = null;
    this.joinedFleetId = null;
    this.joinedRoomId = null;

    if (crewId != null) {
      await connection.invoke('JoinCrew', crewId);
      this.joinedCrewId = crewId;
    }
    if (fleetId != null) {
      await connection.invoke('JoinFleet', fleetId);
      this.joinedFleetId = fleetId;
    }
    if (roomId != null) {
      await connection.invoke('JoinRoom', roomId);
      this.joinedRoomId = roomId;
    }
  }

  private emit<T>(subject: Subject<T>, value: T): void {
    this.ngZone.run(() => subject.next(value));
  }

  private async startConnection(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
    }

    // Skip Server-Sent Events: Android WebViews often fail SSE after WebSockets drop,
    // which blocks the fallback to LongPolling and kills realtime chat/typing.
    this.connection = new HubConnectionBuilder()
      .withUrl(this.apiUrl.resolveHub('/hubs/chat'), {
        accessTokenFactory: () => this.authService.getToken() ?? '',
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling
      })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .build();

    this.connection.on('MessageReceived', (message: ChatMessage) => {
      this.emit(this.messageReceived$, message);
    });

    this.connection.on('MessageUpdated', (message: ChatMessage) => {
      this.emit(this.messageUpdated$, message);
    });

    this.connection.on('MessageDeleted', (event: { roomId: number; messageId: number }) => {
      this.emit(this.messageDeleted$, event);
    });

    this.connection.on('RoomCreated', (room: ChatRoomListItem) => {
      this.emit(this.roomCreated$, room);
    });

    this.connection.on('RoomActivityUpdated', (update: ChatRoomActivityUpdate) => {
      this.emit(this.roomActivityUpdated$, update);
    });

    this.connection.on('DirectMessageReceived', (event: DirectMessageReceivedEvent) => {
      this.emit(this.directMessageReceived$, event);
    });

    this.connection.on('DirectMessageUpdated', (event: DirectMessageReceivedEvent) => {
      this.emit(this.directMessageUpdated$, event);
    });

    this.connection.on('Typing', (event: TypingEvent) => {
      if (!event || !event.scope) {
        return;
      }
      this.emit(this.typing$, {
        ...event,
        displayName: event.displayName || (event.isAnonymous ? 'Anonymous' : 'Someone'),
        isTyping: !!event.isTyping
      });
    });

    this.connection.onreconnected(() => {
      void this.rejoinDesiredGroups();
    });

    this.connection.onclose(() => {
      // Reconnect gave up — allow the next ensureConnected() to start a fresh socket.
      this.startPromise = null;
    });

    await this.connection.start();
  }
}
