import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ChatService } from '../../../services/chat.service';
import { ChatHubService } from '../../../services/chat-hub.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatRoomListItem } from '../../../models/chat.model';
import { MutedContentItem } from '../../../models/notification.model';
import { NotificationService } from '../../../services/notification.service';

@Component({
  selector: 'app-chat-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
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
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private router = inject(Router);
  private chatService = inject(ChatService);
  private chatHub = inject(ChatHubService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);
  private notificationService = inject(NotificationService);
  private subscriptions: Subscription[] = [];

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.createButton = {
      label: 'Create chat room',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/chats/create'])
    };

    this.subscriptions.push(
      this.chatHub.roomCreated$.subscribe(room => void this.onRoomCreated(room)),
      this.chatHub.roomActivityUpdated$.subscribe(update => this.onRoomActivityUpdated(update))
    );

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        if (this.crewId > 0) {
          void this.chatHub.joinCrew(this.crewId);
        }
        this.loadMutes();
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

  openRoom(room: ChatRoomListItem) {
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

  hideRoom(event: Event) {
    event.stopPropagation();
    this.openMenuRoomId = null;
    this.toastService.success('Hide is coming soon');
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
