import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatRoomListItem } from '../../../models/chat.model';

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

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);
    this.loadRooms();
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

  private loadRooms() {
    this.loading = true;
    this.errorMessage = '';
    this.fleetService.getChats().subscribe({
      next: result => {
        this.loading = false;
        if (!result.success) {
          this.errorMessage = result.message || 'Failed to load fleet chats';
          this.rooms = [];
          return;
        }
        this.rooms = result.items ?? [];
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error.error?.message || 'Failed to load fleet chats';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
