import { Injectable, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { ChatMessage, ChatRoomListItem } from '../models/chat.model';
import { AuthService } from './auth.service';

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
  private joinedRoomId: number | null = null;

  readonly messageReceived$ = new Subject<ChatMessage>();
  readonly messageUpdated$ = new Subject<ChatMessage>();
  readonly roomCreated$ = new Subject<ChatRoomListItem>();
  readonly roomActivityUpdated$ = new Subject<ChatRoomActivityUpdate>();
  readonly directMessageReceived$ = new Subject<DirectMessageReceivedEvent>();
  readonly directMessageUpdated$ = new Subject<DirectMessageReceivedEvent>();

  constructor(private authService: AuthService) {}

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

  async joinRoom(roomId: number): Promise<void> {
    const connection = await this.ensureConnected();
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
    this.joinedRoomId = null;
    this.startPromise = null;

    if (!this.connection) {
      return;
    }

    await this.connection.stop();
    this.connection = null;
  }

  private async ensureConnected(): Promise<HubConnection> {
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
      .withUrl('/hubs/chat', {
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
