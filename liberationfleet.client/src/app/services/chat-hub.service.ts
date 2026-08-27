import { Injectable, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
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

@Injectable({
  providedIn: 'root'
})
export class ChatHubService implements OnDestroy {
  private connection: HubConnection | null = null;
  private startPromise: Promise<void> | null = null;
  private joinedCrewId: number | null = null;
  private joinedFleetId: number | null = null;
  private joinedRoomId: number | null = null;

  readonly messageReceived$ = new Subject<ChatMessage>();
  readonly messageUpdated$ = new Subject<ChatMessage>();
  readonly messageDeleted$ = new Subject<{ roomId: number; messageId: number }>();
  readonly roomCreated$ = new Subject<ChatRoomListItem>();
  readonly roomActivityUpdated$ = new Subject<ChatRoomActivityUpdate>();
  readonly directMessageReceived$ = new Subject<DirectMessageReceivedEvent>();
  readonly directMessageUpdated$ = new Subject<DirectMessageReceivedEvent>();

  constructor(
    private authService: AuthService,
    private apiUrl: ApiUrlService
  ) {}

  ngOnDestroy() {
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

  private async ensureConnectedInternal(): Promise<HubConnection> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return this.connection;
    }

    if (!this.startPromise) {
      this.startPromise = this.startConnection();
    }

    await this.startPromise;
    return this.connection!;
  }

  private async startConnection(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(this.apiUrl.resolveHub('/hubs/chat'), {
        accessTokenFactory: () => this.authService.getToken() ?? ''
      })
      .withAutomaticReconnect()
      .build();

    this.connection.on('MessageReceived', (message: ChatMessage) => {
      this.messageReceived$.next(message);
    });

    this.connection.on('MessageUpdated', (message: ChatMessage) => {
      this.messageUpdated$.next(message);
    });

    this.connection.on('MessageDeleted', (event: { roomId: number; messageId: number }) => {
      this.messageDeleted$.next(event);
    });

    this.connection.on('RoomCreated', (room: ChatRoomListItem) => {
      this.roomCreated$.next(room);
    });

    this.connection.on('RoomActivityUpdated', (update: ChatRoomActivityUpdate) => {
      this.roomActivityUpdated$.next(update);
    });

    this.connection.on('DirectMessageReceived', (event: DirectMessageReceivedEvent) => {
      this.directMessageReceived$.next(event);
    });

    this.connection.on('DirectMessageUpdated', (event: DirectMessageReceivedEvent) => {
      this.directMessageUpdated$.next(event);
    });

    await this.connection.start();
  }
}
