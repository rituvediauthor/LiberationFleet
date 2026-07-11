import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { Subscription } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { AdultContentGateComponent } from '../../../components/adult-content-gate/adult-content-gate.component';
import { ChatService } from '../../../services/chat.service';
import { ChatHubService } from '../../../services/chat-hub.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatRoomListItem } from '../../../models/chat.model';
import { HiddenContentItem, MutedContentItem } from '../../../models/notification.model';
import { NotificationService } from '../../../services/notification.service';
import { AdultContentService } from '../../../services/adult-content.service';
import { ContentPreferenceService } from '../../../services/content-preference.service';
import { VoicePresenceService } from '../../../services/voice-presence.service';
import { VoiceParticipant, VoiceRoomPresence } from '../../../models/voice.model';

@Component({
  selector: 'app-chat-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, AdultContentGateComponent, ContentBadgeComponent],
  templateUrl: './chat-list.component.html',
  styleUrl: './chat-list.component.css'
})
export class ChatListComponent implements OnInit, OnDestroy {
  rooms: ChatRoomListItem[] = [];
  loading = true;
  errorMessage = '';
  crewId = 0;
  openMenuRoomId: number | null = null;
  mutedItems: MutedContentItem[] = [];
  hiddenItems: HiddenContentItem[] = [];
  showHiddenExpanded = false;
  showAdultGate = false;
  pendingRoom: ChatRoomListItem | null = null;
  voicePresenceByRoom: VoiceRoomPresence[] = [];
  resourceCounts: Record<string, number> = {};
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private chatService = inject(ChatService);
  private chatHub = inject(ChatHubService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);
  private notificationService = inject(NotificationService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private voicePresence = inject(VoicePresenceService);
  private subscriptions: Subscription[] = [];

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew']);
    this.notificationService.refreshBadges();
    this.subscriptions.push(
      this.notificationService.resourceCounts$.subscribe(counts => {
        this.resourceCounts = counts;
      })
    );

    this.createButton = {
      label: 'Create chat room',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/chats/create'])
    };

    this.subscriptions.push(
      this.chatHub.roomCreated$.subscribe(room => void this.onRoomCreated(room)),
      this.chatHub.roomActivityUpdated$.subscribe(update => this.onRoomActivityUpdated(update)),
      this.voicePresence.presence$.subscribe(rooms => {
        this.voicePresenceByRoom = rooms;
      })
    );

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        if (this.crewId > 0) {
          void this.chatHub.joinCrew(this.crewId);
          void this.voicePresence.ensureCrewSubscribed(this.crewId);
        }
        this.contentPreferenceService.ensureLoaded().subscribe();
        this.loadMutes();
        this.loadHidden();
        this.loadRooms();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew membership';
      }
    });
  }

  ngOnDestroy() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }

  @HostListener('document:click')
  closeMenus() {
    this.openMenuRoomId = null;
  }

  get visibleRooms(): ChatRoomListItem[] {
    return this.rooms.filter(room =>
      !this.isRoomHidden(room.id) && this.adultContentService.shouldShowEntry(room.isAdultContent)
    );
  }

  get hiddenRooms(): ChatRoomListItem[] {
    return this.rooms.filter(room =>
      this.isRoomHidden(room.id) && this.adultContentService.shouldShowEntry(room.isAdultContent)
    );
  }

  chatBadgeCount(roomId: number): number {
    return this.resourceCounts[`chat:${roomId}`] ?? 0;
  }

  formatActivity(date: string): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  roomTypeLabel(room: ChatRoomListItem): string {
    return room.roomType === 'Voice' ? 'Voice' : 'Text';
  }

  voiceOccupantsForRoom(roomId: number): VoiceParticipant[] {
    return this.voicePresenceByRoom.find(room => room.chatRoomId === roomId)?.participants ?? [];
  }

  hasVoiceActivity(roomId: number): boolean {
    return this.voiceOccupantsForRoom(roomId).length > 0;
  }

  openRoom(room: ChatRoomListItem) {
    const resourceKey = this.adultContentService.resourceKey('chat', room.id);
    if (this.adultContentService.needsAgeGate(room.isAdultContent, resourceKey)) {
      this.pendingRoom = room;
      this.showAdultGate = true;
      return;
    }

    this.navigateToRoom(room);
  }

  onAdultGateConfirmed() {
    if (!this.pendingRoom) {
      this.showAdultGate = false;
      return;
    }

    const resourceKey = this.adultContentService.resourceKey('chat', this.pendingRoom.id);
    this.adultContentService.grantConsent(resourceKey);
    const room = this.pendingRoom;
    this.pendingRoom = null;
    this.showAdultGate = false;
    this.navigateToRoom(room);
  }

  onAdultGateDeclined() {
    this.pendingRoom = null;
    this.showAdultGate = false;
  }

  private navigateToRoom(room: ChatRoomListItem) {
    if (room.roomType === 'Voice') {
      this.router.navigate(['/app/crew/chats', room.id, 'voice']);
      return;
    }
    this.router.navigate(['/app/crew/chats', room.id]);
  }

  toggleMenu(roomId: number, event: Event) {
    event.stopPropagation();
    this.openMenuRoomId = this.openMenuRoomId === roomId ? null : roomId;
  }

  editRoom(room: ChatRoomListItem, event: Event) {
    event.stopPropagation();
    this.openMenuRoomId = null;
    this.router.navigate(['/app/crew/chats', room.id, 'edit']);
  }

  isRoomMuted(roomId: number): boolean {
    return this.notificationService.isMuted(this.mutedItems, 'ChatRoom', roomId);
  }

  isRoomHidden(roomId: number): boolean {
    return this.notificationService.isHidden(this.hiddenItems, 'ChatRoom', roomId);
  }

  muteRoom(room: ChatRoomListItem, event: Event) {
    event.stopPropagation();
    this.openMenuRoomId = null;
    const muted = !this.isRoomMuted(room.id);
    this.notificationService.setMute('ChatRoom', room.id, muted).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update mute setting');
          return;
        }
        if (muted) {
          this.mutedItems = [...this.mutedItems, { contentType: 'ChatRoom', resourceId: room.id }];
          this.toastService.success('Chat room muted');
        } else {
          this.mutedItems = this.mutedItems.filter(
            item => !(item.contentType === 'ChatRoom' && item.resourceId === room.id)
          );
          this.toastService.success('Chat room unmuted');
        }
      },
      error: () => this.toastService.error('Failed to update mute setting')
    });
  }

  hideRoom(room: ChatRoomListItem, event: Event) {
    event.stopPropagation();
    this.openMenuRoomId = null;
    this.notificationService.setHidden('ChatRoom', room.id, true).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to hide chat room');
          return;
        }
        this.hiddenItems = [...this.hiddenItems, { contentType: 'ChatRoom', resourceId: room.id }];
        if (!this.isRoomMuted(room.id)) {
          this.mutedItems = [...this.mutedItems, { contentType: 'ChatRoom', resourceId: room.id }];
        }
        this.toastService.success('Chat room hidden');
      },
      error: () => this.toastService.error('Failed to hide chat room')
    });
  }

  unhideRoom(room: ChatRoomListItem, event: Event) {
    event.stopPropagation();
    this.openMenuRoomId = null;
    this.notificationService.setHidden('ChatRoom', room.id, false).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to unhide chat room');
          return;
        }
        this.hiddenItems = this.hiddenItems.filter(
          item => !(item.contentType === 'ChatRoom' && item.resourceId === room.id)
        );
        this.toastService.success('Chat room unhidden');
      },
      error: () => this.toastService.error('Failed to unhide chat room')
    });
  }

  toggleShowHidden() {
    this.showHiddenExpanded = !this.showHiddenExpanded;
  }

  private loadMutes() {
    this.notificationService.getMutes().subscribe({
      next: response => {
        if (response.success) {
          this.mutedItems = response.items ?? [];
        }
      }
    });
  }

  private loadHidden() {
    this.notificationService.getHidden().subscribe({
      next: response => {
        if (response.success) {
          this.hiddenItems = response.items ?? [];
        }
      }
    });
  }

  private loadRooms() {
    this.loading = true;
    this.errorMessage = '';
    this.chatService.getRooms().subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.errorMessage = response.message || 'Failed to load chat rooms';
            return;
          }
          const items = response.items ?? [];
          this.rooms = this.crewId > 0
            ? await this.chatCrypto.decryptRooms(items, this.crewId)
            : items;
        } finally {
          this.loading = false;
        }
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load chat rooms';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private async onRoomCreated(room: ChatRoomListItem) {
    if (!this.adultContentService.shouldShowEntry(room.isAdultContent)) {
      return;
    }

    if (this.rooms.some(existing => existing.id === room.id)) {
      return;
    }

    const decrypted = this.crewId > 0
      ? await this.chatCrypto.decryptRoom(room, this.crewId)
      : room;
    this.rooms = [decrypted, ...this.rooms];
  }

  private onRoomActivityUpdated(update: { roomId: number; lastActivityAt: string }) {
    const room = this.rooms.find(item => item.id === update.roomId);
    if (!room) {
      return;
    }

    room.lastActivityAt = update.lastActivityAt;
    this.rooms = [...this.rooms].sort(
      (a, b) => new Date(b.lastActivityAt).getTime() - new Date(a.lastActivityAt).getTime()
    );
  }
}
