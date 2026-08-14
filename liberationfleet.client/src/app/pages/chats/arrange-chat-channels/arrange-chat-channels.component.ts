import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { ChatService } from '../../../services/chat.service';
import { FleetService } from '../../../services/fleet.service';
import { CrewService } from '../../../services/crew.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatOperationResponse, ChatRoomListItem, ChatRoomListResponse } from '../../../models/chat.model';
import { CrewMembershipStatus } from '../../../models/crew.model';
import { FleetStatus } from '../../../models/fleet.model';

@Component({
  selector: 'app-arrange-chat-channels',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './arrange-chat-channels.component.html',
  styleUrl: './arrange-chat-channels.component.css'
})
export class ArrangeChatChannelsComponent implements OnInit, OnDestroy {
  rooms: ChatRoomListItem[] = [];
  loading = true;
  saving = false;
  errorMessage = '';
  scope: 'crew' | 'fleet' = 'crew';
  crewId = 0;
  fleetId = 0;
  backButton!: ActionBarButton;
  primaryButton!: ActionBarButton;
  secondaryButton!: ActionBarButton;

  draggingIndex: number | null = null;
  dropIndex: number | null = null;
  private dragPointerId: number | null = null;
  private dragMoved = false;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private chatService = inject(ChatService);
  private fleetService = inject(FleetService);
  private crewService = inject(CrewService);
  private chatCrypto = inject(ChatCryptoService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.scope = this.route.snapshot.data['scope'] === 'fleet' ? 'fleet' : 'crew';
    this.backButton = this.navigation.createBackButton([
      this.scope === 'fleet' ? '/app/fleet/chats' : '/app/crew/chats'
    ]);
    this.updateActionButtons();
    void this.loadRooms();
  }

  ngOnDestroy() {
    this.clearDrag();
  }

  @HostListener('window:pointerup', ['$event'])
  onWindowPointerUp(event: PointerEvent) {
    if (this.draggingIndex === null || this.dragPointerId !== event.pointerId) {
      return;
    }
    this.finishDrag();
  }

  @HostListener('window:pointercancel')
  onWindowPointerCancel() {
    this.clearDrag();
  }

  roomTypeIcon(room: ChatRoomListItem): string {
    return room.roomType === 'Voice' ? 'fa-solid fa-volume-high' : 'fa-solid fa-comments';
  }

  roomTypeLabel(room: ChatRoomListItem): string {
    return room.roomType === 'Voice' ? 'Voice' : 'Text';
  }

  onHandlePointerDown(index: number, event: PointerEvent) {
    if (event.button !== 0 || this.saving) {
      return;
    }
    event.preventDefault();
    this.draggingIndex = index;
    this.dropIndex = index;
    this.dragPointerId = event.pointerId;
    this.dragMoved = false;
    (event.target as HTMLElement).setPointerCapture?.(event.pointerId);
  }

  onRowPointerEnter(index: number) {
    if (this.draggingIndex === null) {
      return;
    }
    this.dragMoved = true;
    // Prefer inserting before this row when hovering the row body.
    this.dropIndex = index;
  }

  onDropZoneEnter(index: number) {
    if (this.draggingIndex === null) {
      return;
    }
    this.dragMoved = true;
    this.dropIndex = index;
  }

  isDropHighlight(index: number): boolean {
    return this.draggingIndex !== null && this.dropIndex === index;
  }

  private finishDrag() {
    if (this.draggingIndex === null || this.dropIndex === null) {
      this.clearDrag();
      return;
    }

    const from = this.draggingIndex;
    let to = this.dropIndex;
    // Adjacent drop zones around the dragged item are no-ops.
    if (!this.dragMoved || to === from || to === from + 1) {
      this.clearDrag();
      return;
    }

    const next = [...this.rooms];
    const [moved] = next.splice(from, 1);
    if (to > from) {
      to -= 1;
    }
    next.splice(to, 0, moved);
    this.rooms = next;
    this.clearDrag();
  }

  private clearDrag() {
    this.draggingIndex = null;
    this.dropIndex = null;
    this.dragPointerId = null;
    this.dragMoved = false;
  }

  private updateActionButtons() {
    this.primaryButton = {
      label: 'Submit proposal',
      type: 'primary',
      disabled: this.saving || this.loading || this.rooms.length === 0,
      onClick: () => void this.saveOrder(false)
    };
    this.secondaryButton = {
      label: 'Set personal',
      type: 'secondary',
      disabled: this.saving || this.loading || this.rooms.length === 0,
      onClick: () => void this.saveOrder(true)
    };
  }

  private async loadRooms() {
    this.loading = true;
    this.errorMessage = '';
    this.updateActionButtons();
    try {
      await this.encryptionContent.whenReady();
      if (this.scope === 'fleet') {
        const status = await new Promise<FleetStatus>((resolve, reject) => {
          this.fleetService.getStatus().subscribe({ next: resolve, error: reject });
        });
        this.fleetId = status.fleetId ?? 0;
        this.fleetService.getChats().subscribe({
          next: async (result: ChatRoomListResponse) => {
            if (!result.success) {
              this.loading = false;
              this.errorMessage = result.message || 'Failed to load channels';
              this.updateActionButtons();
              return;
            }
            const items = result.items ?? [];
            this.rooms = this.fleetId > 0
              ? await this.chatCrypto.decryptRooms(items, { fleetId: this.fleetId })
              : items;
            this.loading = false;
            this.updateActionButtons();
          },
          error: (err: { error?: { message?: string } }) => {
            this.loading = false;
            this.errorMessage = err?.error?.message || 'Failed to load channels';
            this.updateActionButtons();
          }
        });
        return;
      }

      const membership = await new Promise<CrewMembershipStatus>((resolve, reject) => {
        this.crewService.getMembership().subscribe({ next: resolve, error: reject });
      });
      this.crewId = membership.crewId ?? 0;
      this.chatService.getRooms().subscribe({
        next: async (result: ChatRoomListResponse) => {
          if (!result.success) {
            this.loading = false;
            this.errorMessage = result.message || 'Failed to load channels';
            this.updateActionButtons();
            return;
          }
          const items = result.items ?? [];
          this.rooms = this.crewId > 0
            ? await this.chatCrypto.decryptRooms(items, { crewId: this.crewId })
            : items;
          this.loading = false;
          this.updateActionButtons();
        },
        error: (err: { error?: { message?: string } }) => {
          this.loading = false;
          this.errorMessage = err?.error?.message || 'Failed to load channels';
          this.updateActionButtons();
        }
      });
    } catch {
      this.loading = false;
      this.errorMessage = 'Failed to load channels';
      this.updateActionButtons();
    }
  }

  private async saveOrder(personal: boolean) {
    if (this.saving || this.rooms.length === 0) {
      return;
    }
    this.saving = true;
    this.updateActionButtons();
    this.chatService.reorderRooms({
      roomIds: this.rooms.map(room => room.id),
      personal,
      scope: this.scope
    }).subscribe({
      next: (result: ChatOperationResponse) => {
        this.saving = false;
        this.updateActionButtons();
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to save channel order');
          return;
        }
        this.toastService.success(result.message || (personal ? 'Personal order saved' : 'Channel order updated'));
        if (result.proposalsSubmitted) {
          void this.router.navigate([
            this.scope === 'fleet' ? '/app/fleet/proposals' : '/app/crew/proposals/list/pending'
          ]);
          return;
        }
        void this.router.navigate([
          this.scope === 'fleet' ? '/app/fleet/chats' : '/app/crew/chats'
        ]);
      },
      error: (err: { error?: { message?: string } }) => {
        this.saving = false;
        this.updateActionButtons();
        this.toastService.error(err?.error?.message || 'Failed to save channel order');
      }
    });
  }
}
