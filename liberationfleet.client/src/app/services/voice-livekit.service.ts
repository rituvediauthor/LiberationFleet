import { Injectable } from '@angular/core';
import {
  ConnectionState,
  LocalParticipant,
  RemoteParticipant,
  Room,
  RoomEvent,
  Track
} from 'livekit-client';
import { BehaviorSubject, Subject } from 'rxjs';
import { VoiceDevicePreferences } from '../models/voice.model';

export interface VoiceConnectionState {
  connected: boolean;
  reconnecting: boolean;
  error?: string;
}

export interface VoiceConnectResult {
  connected: boolean;
  microphoneEnabled: boolean;
  microphoneError?: string;
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
  readonly audioPlaybackBlocked$ = new BehaviorSubject<boolean>(false);

  get isMuted(): boolean {
    return this.localMuted;
  }

  get isDeafened(): boolean {
    return this.localDeafened;
  }

  get devicePrefs(): VoiceDevicePreferences {
    return this.devicePreferences;
  }

  get audioPlaybackBlocked(): boolean {
    return this.audioPlaybackBlocked$.value;
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

  /**
   * Request mic permission during a user gesture (e.g. tapping a voice room).
   * Mobile browsers (especially iOS/Android PWAs) often reject getUserMedia after
   * async navigation/HTTP, even when the user intended to join voice.
   */
  async primeMicrophonePermission(): Promise<void> {
    if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) {
      return;
    }

    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        }
      });
      stream.getTracks().forEach(track => track.stop());
    } catch {
      // Permission may still be granted later via LiveKit unmute; don't block navigation.
    }
  }

  async startAudio(): Promise<void> {
    if (!this.room) {
      return;
    }

    await this.room.startAudio();
    this.audioPlaybackBlocked$.next(!this.room.canPlaybackAudio);
  }

  async connect(wsUrl: string, token: string): Promise<VoiceConnectResult> {
    await this.disconnect();
    this.loadDevicePreferences();
    this.assertPlayableWsUrl(wsUrl);

    this.room = new Room({
      adaptiveStream: true,
      dynacast: true,
      audioCaptureDefaults: {
        autoGainControl: true,
        echoCancellation: true,
        noiseSuppression: true
      }
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
      })
      .on(RoomEvent.AudioPlaybackStatusChanged, () => {
        this.audioPlaybackBlocked$.next(!(this.room?.canPlaybackAudio ?? true));
      });

    try {
      await this.room.connect(wsUrl, token);
    } catch (error) {
      await this.disconnect();
      throw this.normalizeConnectError(error);
    }

    try {
      await this.room.startAudio();
    } catch {
      // iOS/PWA may require a later tap; UI shows "Tap to enable audio".
    }
    this.audioPlaybackBlocked$.next(!this.room.canPlaybackAudio);

    let microphoneEnabled = false;
    let microphoneError: string | undefined;
    try {
      await this.enableMicrophoneWithFallback();
      microphoneEnabled = true;
      this.localMuted = false;
    } catch (error) {
      this.localMuted = true;
      microphoneError = this.describeMicrophoneError(error);
    }

    this.localDeafened = false;
    this.connectionState$.next({
      connected: true,
      reconnecting: false,
      error: microphoneError
    });

    return {
      connected: true,
      microphoneEnabled,
      microphoneError
    };
  }

  async disconnect(): Promise<void> {
    if (!this.room) {
      this.audioPlaybackBlocked$.next(false);
      return;
    }

    this.room.removeAllListeners();
    await this.room.disconnect();
    this.room = null;
    this.localMuted = false;
    this.localDeafened = false;
    this.audioPlaybackBlocked$.next(false);
    this.connectionState$.next({ connected: false, reconnecting: false });
  }

  async setMuted(muted: boolean): Promise<boolean> {
    if (!this.room) {
      return this.localMuted;
    }

    try {
      if (muted) {
        await this.room.localParticipant.setMicrophoneEnabled(false);
      } else {
        await this.enableMicrophoneWithFallback();
      }
      this.localMuted = muted;
      return this.localMuted;
    } catch (error) {
      const message = this.describeMicrophoneError(error);
      this.connectionState$.next({
        connected: this.room.state === ConnectionState.Connected,
        reconnecting: false,
        error: message
      });
      throw new Error(message);
    }
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
      await this.enableMicrophoneWithFallback();
    }
  }

  private async enableMicrophoneWithFallback(): Promise<void> {
    if (!this.room) {
      throw new Error('Not connected to voice.');
    }

    try {
      await this.room.localParticipant.setMicrophoneEnabled(true, this.buildAudioCaptureOptions());
      return;
    } catch (error) {
      // Stored device IDs from desktop/emulator are often invalid on a phone PWA.
      if (!this.devicePreferences.inputDeviceId) {
        throw error;
      }

      this.devicePreferences = {
        ...this.devicePreferences,
        inputDeviceId: ''
      };
      this.saveDevicePreferences(this.devicePreferences);
      await this.room.localParticipant.setMicrophoneEnabled(true);
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

  private assertPlayableWsUrl(wsUrl: string): void {
    const trimmed = (wsUrl || '').trim();
    if (!trimmed) {
      throw new Error('Voice server URL is missing. LiveKit is not configured on the API.');
    }

    const pageHost = typeof window !== 'undefined' ? window.location.hostname : '';
    const pointsAtLoopback = /localhost|127\.0\.0\.1/i.test(trimmed);
    if (pointsAtLoopback && pageHost && pageHost !== 'localhost' && pageHost !== '127.0.0.1') {
      throw new Error(
        'Voice server points at localhost, which phones cannot reach. Set LiveKit__Host to your LiveKit Cloud wss:// URL on the API.'
      );
    }

    if (
      typeof window !== 'undefined'
      && window.isSecureContext
      && trimmed.startsWith('ws://')
      && !pointsAtLoopback
    ) {
      throw new Error('Voice server must use wss:// when the app is served over HTTPS.');
    }
  }

  private normalizeConnectError(error: unknown): Error {
    const detail = error instanceof Error ? error.message : String(error ?? 'Unknown error');
    const lower = detail.toLowerCase();
    if (lower.includes('permission') || lower.includes('notallowed')) {
      return new Error('Microphone permission was denied. Allow the mic for this site and try again.');
    }
    if (lower.includes('failed to fetch') || lower.includes('websocket') || lower.includes('network')) {
      return new Error(`Could not reach the voice server. ${detail}`);
    }
    return new Error(detail || 'Failed to connect to voice (WebRTC).');
  }

  private describeMicrophoneError(error: unknown): string {
    const detail = error instanceof Error ? error.message : String(error ?? '');
    const lower = detail.toLowerCase();
    if (lower.includes('permission') || lower.includes('notallowed') || lower.includes('denied')) {
      return 'Microphone permission denied. Allow the mic for this site (and in phone Settings if prompted).';
    }
    if (lower.includes('overconstrained') || lower.includes('device')) {
      return 'Could not open the selected microphone. Default mic will be used next time — try Unmute again.';
    }
    return detail || 'Could not enable the microphone.';
  }
}
