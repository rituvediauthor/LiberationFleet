import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatRoomListItem } from '../../../models/chat.model';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';

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

  private async loadRooms() {
    this.loading = true;
    this.errorMessage = '';
    try {
      await this.encryptionContent.whenReady();
      const status = await new Promise<{ fleetId?: number }>((resolve, reject) => {
        this.fleetService.getStatus().subscribe({
          next: value => resolve(value),
          error: err => reject(err)
        });
      });
      this.fleetId = status.fleetId ?? 0;

      this.fleetService.getChats().subscribe({
        next: async result => {
          this.loading = false;
          if (!result.success) {
            this.errorMessage = result.message || 'Failed to load fleet chats';
            this.rooms = [];
            return;
          }
          const items = result.items ?? [];
          this.rooms = this.fleetId > 0
            ? await this.chatCrypto.decryptRooms(items, { fleetId: this.fleetId })
            : items;
        },
        error: error => {
          this.loading = false;
          this.errorMessage = error.error?.message || 'Failed to load fleet chats';
          this.toastService.error(this.errorMessage);
        }
      });
    } catch {
      this.loading = false;
      this.errorMessage = 'Failed to load fleet chats';
    }
  }
}
