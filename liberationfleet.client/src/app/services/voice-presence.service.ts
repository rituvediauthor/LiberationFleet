import { Injectable, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { VoiceParticipant, VoicePresenceSnapshot, VoiceRoomPresence } from '../models/voice.model';
import { AuthService } from './auth.service';
import { VoiceApiService } from './voice-api.service';

@Injectable({
  providedIn: 'root'
})
export class VoicePresenceService implements OnDestroy {
  private connection: HubConnection | null = null;
  private startPromise: Promise<void> | null = null;
  private joinedCrewId: number | null = null;
  private readonly presenceSubject = new BehaviorSubject<VoiceRoomPresence[]>([]);

  readonly presence$ = this.presenceSubject.asObservable();
  readonly stateUpdated$ = new Subject<VoiceParticipant>();

  constructor(
    private authService: AuthService,
    private voiceApi: VoiceApiService
  ) {}

  ngOnDestroy() {
    void this.disconnect();
  }

  get snapshot(): VoiceRoomPresence[] {
    return this.presenceSubject.value;
  }

  getParticipantsForRoom(roomId: number): VoiceParticipant[] {
    return this.snapshot.find(room => room.chatRoomId === roomId)?.participants ?? [];
  }

  async ensureCrewSubscribed(crewId: number): Promise<void> {
    const connection = await this.ensureConnected();
    if (this.joinedCrewId === crewId) {
      return;
    }

    if (this.joinedCrewId != null) {
      await connection.invoke('LeaveCrew', this.joinedCrewId);
    }

    await connection.invoke('JoinCrew', crewId);
    this.joinedCrewId = crewId;
    await this.refreshPresence(crewId);
  }

  async refreshPresence(crewId: number): Promise<void> {
    return new Promise(resolve => {
      this.voiceApi.getVoicePresence(crewId).subscribe({
        next: response => {
          if (response.success) {
            this.presenceSubject.next(response.rooms ?? []);
          }
          resolve();
        },
        error: () => resolve()
      });
    });
  }

  async registerVoicePresence(roomId: number): Promise<void> {
    const connection = await this.ensureConnected();
    await connection.invoke('JoinVoice', roomId);
  }

  async leaveVoicePresence(roomId: number): Promise<void> {
    if (this.connection?.state !== HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke('LeaveVoice', roomId);
  }

  async updateVoiceState(roomId: number, isMuted: boolean, isDeafened: boolean, isSpeaking: boolean): Promise<void> {
    if (this.connection?.state !== HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke('UpdateVoiceState', roomId, isMuted, isDeafened, isSpeaking);
  }

  async disconnect(): Promise<void> {
    this.joinedCrewId = null;
    this.startPromise = null;
    this.presenceSubject.next([]);

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
      .withUrl('/hubs/voice', {
        accessTokenFactory: () => this.authService.getToken() ?? ''
      })
      .withAutomaticReconnect()
      .build();

    this.connection.on('VoicePresenceUpdated', (snapshot: VoicePresenceSnapshot) => {
      this.presenceSubject.next(snapshot.rooms ?? []);
    });

    this.connection.on('VoiceStateUpdated', (participant: VoiceParticipant) => {
      this.stateUpdated$.next(participant);
      const rooms = this.snapshot.map(room => {
        if (room.chatRoomId !== participant.chatRoomId) {
          return room;
        }

        const participants = room.participants.some(item => item.userId === participant.userId)
          ? room.participants.map(item => item.userId === participant.userId ? participant : item)
          : [...room.participants, participant];

        return { ...room, participants };
      });

      this.presenceSubject.next(rooms);
    });

    await this.connection.start();
  }
}
