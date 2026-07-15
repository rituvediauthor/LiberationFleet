import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { AdultContentGateComponent } from '../../../components/adult-content-gate/adult-content-gate.component';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatService } from '../../../services/chat.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { AdultContentService } from '../../../services/adult-content.service';
import { ContentPreferenceService } from '../../../services/content-preference.service';
import { VoiceApiService } from '../../../services/voice-api.service';
import { VoiceLiveKitService } from '../../../services/voice-livekit.service';
import { VoicePresenceService } from '../../../services/voice-presence.service';
import { CrewmateService } from '../../../services/crewmate.service';
import { AuthService } from '../../../services/auth.service';
import { NavigationService } from '../../../services/navigation.service';
import { NotificationContentService } from '../../../services/notification-content.service';
import { VoiceDevicePreferences, VoiceParticipant } from '../../../models/voice.model';
import { getUserIdFromToken } from '../../../utils/jwt.util';

@Component({
  selector: 'app-chat-voice',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent, AdultContentGateComponent],
  templateUrl: './chat-voice.component.html',
  styleUrl: './chat-voice.component.css'
})
export class ChatVoiceComponent implements OnInit, OnDestroy {
  roomId = 0;
  crewId = 0;
  roomName = 'Voice chat';
  roomIsAdultContent = false;
  participants: VoiceParticipant[] = [];
  activeSpeakerIds = new Set<number>();
  loading = true;
  connecting = false;
  connected = false;
  reconnecting = false;
  errorMessage = '';
  showAdultGate = false;
  contentRevealed = false;
  isMuted = false;
  isDeafened = false;
  isServerMuted = false;
  canModerate = false;
  currentUserId: number | null = null;
  showDeviceSettings = false;
  inputDevices: MediaDeviceInfo[] = [];
  outputDevices: MediaDeviceInfo[] = [];
  devicePreferences: VoiceDevicePreferences = { inputDeviceId: '', outputDeviceId: '' };
  backButton!: ActionBarButton;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private notificationContent = inject(NotificationContentService);
  private chatService = inject(ChatService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private voiceApi = inject(VoiceApiService);
  private voiceLiveKit = inject(VoiceLiveKitService);
  private voicePresence = inject(VoicePresenceService);
  private crewmateService = inject(CrewmateService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private subscriptions: Subscription[] = [];
  private lastReconnecting = false;

  ngOnInit() {
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;
    this.devicePreferences = this.voiceLiveKit.loadDevicePreferences();

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => void this.disconnectAndLeave()
    };

    this.subscriptions.push(
      this.route.paramMap.subscribe(params => {
        const nextRoomId = Number(params.get('id'));
        if (!nextRoomId || Number.isNaN(nextRoomId)) {
          return;
        }

        if (this.roomId && nextRoomId !== this.roomId) {
          void this.switchRoom(nextRoomId);
          return;
        }

        this.roomId = nextRoomId;
        this.notificationContent.markVisited(`/app/crew/chats/${this.roomId}`, this.roomId);
      }),
      this.voicePresence.presence$.subscribe(rooms => {
        this.participants = rooms.find(room => room.chatRoomId === this.roomId)?.participants ?? [];
      }),
      this.voicePresence.stateUpdated$.subscribe(participant => {
        if (participant.userId !== this.currentUserId || participant.chatRoomId !== this.roomId) {
          return;
        }

        void this.applyServerMuteState(participant.isServerMuted);
      }),
      this.voiceLiveKit.activeSpeakers$.subscribe(ids => {
        this.activeSpeakerIds = new Set(ids);
        if (this.connected) {
          void this.syncVoiceState();
        }
      }),
      this.voiceLiveKit.connectionState$.subscribe(state => {
        this.connected = state.connected;
        this.reconnecting = state.reconnecting;
        if (state.reconnecting && !this.lastReconnecting) {
          this.toastService.info('Reconnecting to voice...');
        }
        if (!state.reconnecting && this.lastReconnecting && state.connected) {
          this.toastService.success('Voice reconnected');
        }
        this.lastReconnecting = state.reconnecting;
        if (state.error) {
          this.errorMessage = state.error;
        }
      })
    );

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        this.contentPreferenceService.ensureLoaded().subscribe({
          next: () => void this.loadRoom()
        });
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew membership';
      }
    });

    if (this.currentUserId) {
      this.crewmateService.getCrewmateProfile(this.currentUserId).subscribe({
        next: response => {
          if (response.success && response.profile) {
            this.canModerate = response.profile.canModerateAttachments;
          }
        }
      });
    }
  }

  ngOnDestroy() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    void this.cleanupVoiceSession();
  }

  isSpeaking(participant: VoiceParticipant): boolean {
    return this.activeSpeakerIds.has(participant.userId) && !participant.isMuted;
  }

  async toggleMute() {
    if (!this.connected) {
      return;
    }

    if (this.isServerMuted) {
      this.toastService.error('You have been server muted by a moderator');
      return;
    }

    if (this.isDeafened) {
      this.isDeafened = false;
      await this.voiceLiveKit.setDeafened(false);
    }

    this.isMuted = await this.voiceLiveKit.setMuted(!this.isMuted);
    await this.syncVoiceState();
  }

  async toggleDeafen() {
    if (!this.connected) {
      return;
    }

    this.isDeafened = await this.voiceLiveKit.setDeafened(!this.isDeafened);
    this.isMuted = this.voiceLiveKit.isMuted;
    await this.syncVoiceState();
  }

  async disconnectVoice() {
    await this.disconnectAndLeave();
  }

  onAdultGateConfirmed() {
    const resourceKey = this.adultContentService.resourceKey('chat', this.roomId);
    this.adultContentService.grantConsent(resourceKey);
    this.showAdultGate = false;
    this.contentRevealed = true;
    void this.connectVoice();
  }

  onAdultGateDeclined() {
    this.showAdultGate = false;
    this.navigation.back(['/app/crew/chats']);
  }

  async openDeviceSettings() {
    this.showDeviceSettings = true;
    await this.loadDevices();
  }

  closeDeviceSettings() {
    this.showDeviceSettings = false;
  }

  async saveDeviceSettings() {
    await this.voiceLiveKit.applyDevicePreferences(this.devicePreferences);
    this.showDeviceSettings = false;
    this.toastService.success('Voice device preferences saved');
  }

  serverMuteParticipant(participant: VoiceParticipant) {
    this.voiceApi.serverMuteParticipant(this.roomId, participant.userId, !participant.isServerMuted).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update server mute');
        }
      },
      error: () => this.toastService.error('Failed to update server mute')
    });
  }

  disconnectParticipant(participant: VoiceParticipant) {
    this.voiceApi.disconnectParticipant(this.roomId, participant.userId).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to disconnect participant');
        }
      },
      error: () => this.toastService.error('Failed to disconnect participant')
    });
  }

  private async switchRoom(nextRoomId: number) {
    await this.cleanupVoiceSession();
    this.roomId = nextRoomId;
    this.loading = true;
    this.connecting = false;
    this.connected = false;
    this.reconnecting = false;
    this.errorMessage = '';
    this.participants = [];
    this.isMuted = false;
    this.isDeafened = false;
    this.isServerMuted = false;
    this.showAdultGate = false;
    this.contentRevealed = false;
    await this.loadRoom();
  }

  private async applyServerMuteState(serverMuted: boolean) {
    this.isServerMuted = serverMuted;
    if (!this.connected) {
      return;
    }

    if (serverMuted) {
      this.isMuted = await this.voiceLiveKit.setMuted(true);
      this.toastService.info('You have been server muted');
    }

    await this.syncVoiceState();
  }

  private async loadRoom() {
    this.chatService.getRoom(this.roomId).subscribe({
      next: async response => {
        const room = response.room;
        if (!response.success || !room) {
          this.loading = false;
          this.errorMessage = response.message || 'Voice channel not found';
          return;
        }

        if (room.roomType !== 'Voice') {
          this.loading = false;
          this.errorMessage = 'This room is not a voice channel';
          return;
        }

        this.roomIsAdultContent = !!room.isAdultContent;
        const decrypted = this.crewId > 0
          ? await this.chatCrypto.decryptRoom(room, { crewId: this.crewId })
          : room;
        this.roomName = decrypted.name || 'Voice chat';

        const resourceKey = this.adultContentService.resourceKey('chat', this.roomId);
        if (this.adultContentService.needsAgeGate(this.roomIsAdultContent, resourceKey)) {
          this.showAdultGate = true;
          this.contentRevealed = false;
          this.loading = false;
          return;
        }

        this.contentRevealed = true;
        await this.connectVoice();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Voice channel not found';
      }
    });
  }

  private async connectVoice() {
    if (this.connecting || this.connected) {
      return;
    }

    this.connecting = true;
    this.errorMessage = '';

    await this.voicePresence.ensureCrewSubscribed(this.crewId);

    this.voiceApi.joinVoiceRoom(this.roomId).subscribe({
      next: async response => {
        if (!response.success || !response.token || !response.wsUrl) {
          this.connecting = false;
          this.loading = false;
          this.errorMessage = response.message || 'Failed to join voice channel';
          this.toastService.error(this.errorMessage);
          return;
        }

        try {
          if (response.previousChatRoomId) {
            await this.voicePresence.leaveVoicePresence(response.previousChatRoomId).catch(() => undefined);
          }

          await this.voiceLiveKit.connect(response.wsUrl, response.token);
          await this.voicePresence.registerVoicePresence(this.roomId);
          this.isMuted = this.voiceLiveKit.isMuted;
          this.isDeafened = this.voiceLiveKit.isDeafened;
          await this.syncVoiceState();
          this.connecting = false;
          this.loading = false;
          this.connected = true;
        } catch {
          this.connecting = false;
          this.loading = false;
          this.errorMessage = 'Failed to connect microphone. Check browser permissions.';
          this.toastService.error(this.errorMessage);
        }
      },
      error: () => {
        this.connecting = false;
        this.loading = false;
        this.errorMessage = 'Failed to join voice channel';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private async syncVoiceState() {
    const speaking = this.currentUserId != null && this.activeSpeakerIds.has(this.currentUserId);
    await this.voicePresence.updateVoiceState(this.roomId, this.isMuted, this.isDeafened, speaking);
  }

  private async disconnectAndLeave() {
    await this.cleanupVoiceSession();
    this.navigation.back(['/app/crew/chats']);
  }

  private async cleanupVoiceSession() {
    await this.voiceLiveKit.disconnect();
    await this.voicePresence.leaveVoicePresence(this.roomId).catch(() => undefined);
    await new Promise<void>(resolve => {
      this.voiceApi.leaveVoiceRoom(this.roomId).subscribe({
        next: () => resolve(),
        error: () => resolve()
      });
    });
  }

  private async loadDevices() {
    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      this.inputDevices = devices.filter(device => device.kind === 'audioinput');
      this.outputDevices = devices.filter(device => device.kind === 'audiooutput');
    } catch {
      this.inputDevices = [];
      this.outputDevices = [];
    }
  }
}
