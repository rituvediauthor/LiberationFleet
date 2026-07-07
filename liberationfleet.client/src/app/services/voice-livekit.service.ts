import { Injectable } from '@angular/core';
import {
  ConnectionState,
  LocalParticipant,
  RemoteParticipant,
  Room,
  RoomEvent,
  Track
} from 'livekit-client';
import { Subject } from 'rxjs';
import { VoiceDevicePreferences } from '../models/voice.model';

export interface VoiceConnectionState {
  connected: boolean;
  reconnecting: boolean;
  error?: string;
}

@Injectable({
  providedIn: 'root'
})
export class VoiceLiveKitService {
  private room: Room | null = null;
  private localMuted = false;
  private localDeafened = false;
  private devicePreferences: VoiceDevicePreferences = {
    inputDeviceId: '',
    outputDeviceId: ''
  };

  readonly connectionState$ = new Subject<VoiceConnectionState>();
  readonly activeSpeakers$ = new Subject<number[]>();
  readonly participantConnected$ = new Subject<RemoteParticipant>();
  readonly participantDisconnected$ = new Subject<RemoteParticipant>();

  get isMuted(): boolean {
    return this.localMuted;
  }

  get isDeafened(): boolean {
    return this.localDeafened;
  }

  get devicePrefs(): VoiceDevicePreferences {
    return this.devicePreferences;
  }

  loadDevicePreferences(): VoiceDevicePreferences {
    try {
      const raw = localStorage.getItem('voiceDevicePreferences');
      if (raw) {
        this.devicePreferences = JSON.parse(raw) as VoiceDevicePreferences;
      }
    } catch {
      // Ignore invalid stored preferences.
    }

    return this.devicePreferences;
  }

  saveDevicePreferences(preferences: VoiceDevicePreferences): void {
    this.devicePreferences = preferences;
    localStorage.setItem('voiceDevicePreferences', JSON.stringify(preferences));
  }

  async connect(wsUrl: string, token: string): Promise<void> {
    await this.disconnect();
    this.loadDevicePreferences();

    this.room = new Room({
      adaptiveStream: true,
      dynacast: true
    });

    this.room
      .on(RoomEvent.ConnectionStateChanged, state => {
        this.connectionState$.next({
          connected: state === ConnectionState.Connected,
          reconnecting: state === ConnectionState.Reconnecting,
          error: state === ConnectionState.Disconnected ? 'Disconnected' : undefined
        });
      })
      .on(RoomEvent.ActiveSpeakersChanged, speakers => {
        this.activeSpeakers$.next(speakers.map(speaker => Number(speaker.identity)).filter(id => !Number.isNaN(id)));
      })
      .on(RoomEvent.ParticipantConnected, participant => {
        this.participantConnected$.next(participant);
      })
      .on(RoomEvent.ParticipantDisconnected, participant => {
        this.participantDisconnected$.next(participant);
      })
      .on(RoomEvent.TrackSubscribed, (_track, _pub, participant) => {
        this.applyDeafenToParticipant(participant);
      });

    await this.room.connect(wsUrl, token);
    await this.room.localParticipant.setMicrophoneEnabled(true, this.buildAudioCaptureOptions());
    this.localMuted = false;
    this.localDeafened = false;
    this.connectionState$.next({ connected: true, reconnecting: false });
  }

  async disconnect(): Promise<void> {
    if (!this.room) {
      return;
    }

    this.room.removeAllListeners();
    await this.room.disconnect();
    this.room = null;
    this.localMuted = false;
    this.localDeafened = false;
    this.connectionState$.next({ connected: false, reconnecting: false });
  }

  async setMuted(muted: boolean): Promise<boolean> {
    if (!this.room) {
      return this.localMuted;
    }

    await this.room.localParticipant.setMicrophoneEnabled(!muted, this.buildAudioCaptureOptions());
    this.localMuted = muted;
    return this.localMuted;
  }

  async setDeafened(deafened: boolean): Promise<boolean> {
    this.localDeafened = deafened;
    if (deafened && !this.localMuted) {
      await this.setMuted(true);
    }

    this.room?.remoteParticipants.forEach(participant => this.applyDeafenToParticipant(participant));
    return this.localDeafened;
  }

  async applyDevicePreferences(preferences: VoiceDevicePreferences): Promise<void> {
    this.saveDevicePreferences(preferences);
    if (!this.room) {
      return;
    }

    if (preferences.outputDeviceId) {
      this.room.switchActiveDevice('audiooutput', preferences.outputDeviceId).catch(() => undefined);
    }

    if (!this.localMuted) {
      await this.room.localParticipant.setMicrophoneEnabled(true, this.buildAudioCaptureOptions());
    }
  }

  private buildAudioCaptureOptions() {
    if (!this.devicePreferences.inputDeviceId) {
      return undefined;
    }

    return {
      deviceId: this.devicePreferences.inputDeviceId
    };
  }

  private applyDeafenToParticipant(participant: RemoteParticipant | LocalParticipant): void {
    participant.audioTrackPublications.forEach(publication => {
      const track = publication.track;
      if (!track || track.kind !== Track.Kind.Audio) {
        return;
      }

      if (this.localDeafened) {
        track.detach();
      } else {
        track.attach();
      }
    });
  }
}
