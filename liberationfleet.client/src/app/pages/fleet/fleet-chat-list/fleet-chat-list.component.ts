import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { firstValueFrom, Subscription } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { AdultContentGateComponent } from '../../../components/adult-content-gate/adult-content-gate.component';
import { NavigationService } from '../../../services/navigation.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatRoomListItem } from '../../../models/chat.model';
import { HiddenContentItem, MutedContentItem } from '../../../models/notification.model';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { ChatHubService } from '../../../services/chat-hub.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { NotificationService } from '../../../services/notification.service';
import { AdultContentService } from '../../../services/adult-content.service';
import { ContentPreferenceService } from '../../../services/content-preference.service';
import { CrewService } from '../../../services/crew.service';
import { CONNECTIVITY_ERROR_MESSAGE, describeLoadError, isConnectivityError, isRetryableLoadError } from '../../../utils/http-error.util';

@Component({
  selector: 'app-fleet-chat-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, AdultContentGateComponent, ContentBadgeComponent],
  templateUrl: './fleet-chat-list.component.html',
  styleUrl: './fleet-chat-list.component.css'
})
export class FleetChatListComponent implements OnInit, OnDestroy {
  rooms: ChatRoomListItem[] = [];
  loading = true;
  errorMessage = '';
  pageMenuOpen = false;
  openMenuRoomId: number | null = null;
  mutedItems: MutedContentItem[] = [];
  hiddenItems: HiddenContentItem[] = [];
  showHiddenExpanded = false;
  showAdultGate = false;
  pendingRoom: ChatRoomListItem | null = null;
  resourceCounts: Record<string, number> = {};
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  fleetId = 0;
  crewId = 0;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private chatCrypto = inject(ChatCryptoService);
  private chatHub = inject(ChatHubService);
  private encryptionContent = inject(EncryptionContentService);
  private notificationService = inject(NotificationService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private subscriptions: Subscription[] = [];

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
    this.createButton = {
      label: 'Propose chat',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/fleet/chats/create'])
    };

    this.notificationService.refreshBadges();
    this.subscriptions.push(
      this.notificationService.resourceCounts$.subscribe(counts => {
        this.resourceCounts = counts;
      }),
      this.chatHub.roomCreated$.subscribe(room => void this.onRoomCreated(room)),
      this.chatHub.roomActivityUpdated$.subscribe(update => this.onRoomActivityUpdated(update))
    );

    this.contentPreferenceService.ensureLoaded().subscribe();
    this.loadMutes();
    this.loadHidden();
    void this.loadRooms();
  }

  ngOnDestroy() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }

  @HostListener('document:click')
  closeMenus() {
    this.openMenuRoomId = null;
    this.pageMenuOpen = false;
  }

  togglePageMenu(event: Event) {
    event.stopPropagation();
    this.openMenuRoomId = null;
    this.pageMenuOpen = !this.pageMenuOpen;
  }

  goArrangeChannels(event: Event) {
    event.stopPropagation();
    this.pageMenuOpen = false;
    void this.router.navigate(['/app/fleet/chats/arrange']);
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
    void this.router.navigate(['/app/fleet/chats', room.id]);
  }

  toggleMenu(roomId: number, event: Event) {
    event.stopPropagation();
    this.pageMenuOpen = false;
    this.openMenuRoomId = this.openMenuRoomId === roomId ? null : roomId;
  }

  editRoom(room: ChatRoomListItem, event: Event) {
    event.stopPropagation();
    this.openMenuRoomId = null;
    void this.router.navigate(['/app/fleet/chats', room.id, 'edit']);
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
      error: (error: unknown) => this.toastService.error(describeLoadError(error, 'Failed to update mute setting'))
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
      error: (error: unknown) => this.toastService.error(describeLoadError(error, 'Failed to hide chat room'))
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
      error: (error: unknown) => this.toastService.error(describeLoadError(error, 'Failed to unhide chat room'))
    });
  }

  toggleShowHidden() {
    this.showHiddenExpanded = !this.showHiddenExpanded;
  }

  retryLoadRooms() {
    void this.loadRooms();
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

  private async loadRooms(attempt = 0): Promise<void> {
    this.loading = true;
    this.errorMessage = '';
    try {
      const [, status, membership] = await Promise.all([
        this.encryptionContent.whenReady(),
        firstValueFrom(this.fleetService.getStatus()),
        firstValueFrom(this.crewService.getMembership())
      ]);
      this.fleetId = status.fleetId ?? 0;
      this.crewId = membership.crewId ?? 0;

      if (this.fleetId > 0) {
        void this.chatHub.joinFleet(this.fleetId);
      }
      if (this.crewId > 0) {
        void this.chatHub.joinCrew(this.crewId);
      }

      const result = await firstValueFrom(this.fleetService.getChats());
      if (!result.success) {
        this.errorMessage = result.message || 'Failed to load fleet chats';
        this.rooms = [];
        return;
      }

      const items = result.items ?? [];
      try {
        this.rooms = this.fleetId > 0
          ? await this.chatCrypto.decryptRooms(items, { fleetId: this.fleetId })
          : items;
      } catch {
        this.rooms = items;
      }
    } catch (error: unknown) {
      if (attempt < 1 && isRetryableLoadError(error)) {
        await new Promise(resolve => setTimeout(resolve, 400));
        return this.loadRooms(attempt + 1);
      }
      this.rooms = [];
      this.errorMessage = describeLoadError(error, 'Failed to load fleet chats');
      this.toastService.error(
        isConnectivityError(error) ? CONNECTIVITY_ERROR_MESSAGE : this.errorMessage
      );
    } finally {
      this.loading = false;
    }
  }

  private async onRoomCreated(room: ChatRoomListItem) {
    if (!this.adultContentService.shouldShowEntry(room.isAdultContent)) {
      return;
    }

    if (this.rooms.some(existing => existing.id === room.id)) {
      return;
    }

    const decrypted = this.fleetId > 0
      ? await this.chatCrypto.decryptRoom(room, { fleetId: this.fleetId })
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
