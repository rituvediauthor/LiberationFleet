import { Component, HostListener, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatRoomListItem } from '../../../models/chat.model';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { CONNECTIVITY_ERROR_MESSAGE, describeLoadError, isConnectivityError, isRetryableLoadError } from '../../../utils/http-error.util';

@Component({
  selector: 'app-fleet-chat-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './fleet-chat-list.component.html',
  styleUrl: './fleet-chat-list.component.css'
})
export class FleetChatListComponent implements OnInit {
  rooms: ChatRoomListItem[] = [];
  loading = true;
  errorMessage = '';
  pageMenuOpen = false;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  fleetId = 0;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);
  private chatCrypto = inject(ChatCryptoService);
  private encryptionContent = inject(EncryptionContentService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
    this.createButton = {
      label: 'Propose chat',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/fleet/chats/create'])
    };
    void this.loadRooms();
  }

  @HostListener('document:click')
  closeMenus() {
    this.pageMenuOpen = false;
  }

  togglePageMenu(event: Event) {
    event.stopPropagation();
    this.pageMenuOpen = !this.pageMenuOpen;
  }

  goArrangeChannels(event: Event) {
    event.stopPropagation();
    this.pageMenuOpen = false;
    void this.router.navigate(['/app/fleet/chats/arrange']);
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
      void this.router.navigate(['/app/fleet/chats', room.id, 'voice']);
      return;
    }
    void this.router.navigate(['/app/fleet/chats', room.id]);
  }

  retryLoadRooms() {
    void this.loadRooms();
  }

  private async loadRooms(attempt = 0): Promise<void> {
    this.loading = true;
    this.errorMessage = '';
    try {
      const [, status] = await Promise.all([
        this.encryptionContent.whenReady(),
        firstValueFrom(this.fleetService.getStatus())
      ]);
      this.fleetId = status.fleetId ?? 0;

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
}
