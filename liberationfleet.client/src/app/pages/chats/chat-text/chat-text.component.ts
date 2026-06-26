import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  QueryList,
  ViewChild,
  ViewChildren,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ProposalAttachmentDisplayComponent } from '../../../components/proposal-attachment-display/proposal-attachment-display.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatService } from '../../../services/chat.service';
import { ChatHubService } from '../../../services/chat-hub.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { AuthService } from '../../../services/auth.service';
import { ChatMessage } from '../../../models/chat.model';
import { PendingAttachment } from '../../../models/proposal.model';
import { getUserIdFromToken } from '../../../utils/jwt.util';

@Component({
  selector: 'app-chat-text',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent
  ],
  templateUrl: './chat-text.component.html',
  styleUrl: './chat-text.component.css'
})
export class ChatTextComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('messageScroll') messageScroll?: ElementRef<HTMLDivElement>;
  @ViewChildren('messageItem') messageItems?: QueryList<ElementRef<HTMLElement>>;

  roomId = 0;
  roomName = 'Chat';
  messages: ChatMessage[] = [];
  crewId = 0;
  currentUserId: number | null = null;
  authorDisplayName = '';
  messageText = '';
  messageAttachments: PendingAttachment[] = [];
  composerFocused = false;
  pickingFile = false;
  loading = true;
  loadingOlder = false;
  hasMore = false;
  sending = false;
  loadError = '';

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private chatService = inject(ChatService);
  private chatHub = inject(ChatHubService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private encryptionContent = inject(EncryptionContentService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private intersectionObserver?: IntersectionObserver;
  private hubSubscription?: Subscription;

  ngOnInit() {
    this.roomId = Number(this.route.snapshot.paramMap.get('id'));
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;

    this.hubSubscription = this.chatHub.messageReceived$.subscribe(message => {
      void this.onMessageReceived(message);
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        if (this.crewId > 0) {
          void this.chatHub.joinCrew(this.crewId);
        }
        void this.chatHub.joinRoom(this.roomId);
        this.loadRoomName();
        this.loadLatestMessages(true);
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Failed to load crew membership';
      }
    });
  }

  ngAfterViewInit() {
    this.setupLazyLoadObserver();
    this.messageItems?.changes.subscribe(() => this.setupLazyLoadObserver());
  }

  ngOnDestroy() {
    this.intersectionObserver?.disconnect();
    this.hubSubscription?.unsubscribe();
    void this.chatHub.leaveRoom();
  }

  goBack() {
    this.router.navigate(['/app/crew/chats']);
  }

  onComposerFocus() {
    this.composerFocused = true;
  }

  onComposerBlur() {
    setTimeout(() => {
      if (this.pickingFile) {
        return;
      }
      if (!this.messageText.trim() && this.messageAttachments.length === 0) {
        this.composerFocused = false;
      }
    }, 150);
  }

  onFileDialogOpenChange(open: boolean) {
    this.pickingFile = open;
    if (open) {
      this.composerFocused = true;
      return;
    }
    if (!this.messageText.trim() && this.messageAttachments.length === 0) {
      this.composerFocused = false;
    }
  }

  get composerExpanded(): boolean {
    return this.composerFocused || this.pickingFile || this.messageAttachments.length > 0;
  }

  canSend(): boolean {
    return Boolean(this.messageText.trim() || this.messageAttachments.length > 0);
  }

  async sendMessage() {
    if (!this.canSend() || this.sending || this.crewId <= 0) {
      return;
    }

    this.sending = true;
    try {
      const encrypted = await this.chatCrypto.encryptMessagePayload(
        this.crewId,
        this.messageText.trim(),
        this.authorDisplayName,
        this.messageAttachments
      );

      this.chatService.sendMessage(this.roomId, encrypted).subscribe({
        next: response => {
          this.sending = false;
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to send message');
            return;
          }
          this.messageText = '';
          this.messageAttachments = [];
          this.composerFocused = false;
        },
        error: () => {
          this.sending = false;
          this.toastService.error('Failed to send message');
        }
      });
    } catch {
      this.sending = false;
      this.toastService.error('Failed to encrypt message');
    }
  }

  isOwnMessage(message: ChatMessage): boolean {
    return this.currentUserId != null && message.authorUserId === this.currentUserId;
  }

  private loadRoomName() {
    this.chatService.getRooms().subscribe({
      next: async response => {
        const room = response.items?.find(item => item.id === this.roomId);
        if (!room) {
          return;
        }

        const decrypted = this.crewId > 0
          ? await this.chatCrypto.decryptRoom(room, this.crewId)
          : room;
        this.roomName = decrypted.name || 'Chat';
      }
    });
  }

  private async onMessageReceived(message: ChatMessage) {
    if (message.id <= 0 || this.messages.some(existing => existing.id === message.id)) {
      return;
    }

    const scrollEl = this.messageScroll?.nativeElement;
    const shouldStickToBottom = scrollEl
      ? scrollEl.scrollHeight - scrollEl.scrollTop - scrollEl.clientHeight < 80
      : true;

    const decrypted = this.crewId > 0
      ? await this.chatCrypto.decryptSingleMessage(message, this.crewId)
      : message;
    this.messages = [...this.messages, decrypted];

    if (shouldStickToBottom) {
      setTimeout(() => this.scrollToBottom(), 0);
    }
  }

  private loadLatestMessages(scrollToBottom: boolean) {
    this.loading = true;
    this.loadError = '';
    this.chatService.getMessages(this.roomId, 50).subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.loadError = response.message || 'Failed to load messages';
            return;
          }
          this.hasMore = response.hasMore;
          this.messages = this.crewId > 0
            ? await this.chatCrypto.decryptMessages(response.items ?? [], this.crewId)
            : response.items ?? [];
          if (scrollToBottom) {
            setTimeout(() => this.scrollToBottom(), 0);
          }
        } catch (error: unknown) {
          this.loadError = error instanceof Error ? error.message : 'Failed to decrypt messages';
        } finally {
          this.loading = false;
        }
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Failed to load messages';
      }
    });
  }

  private loadOlderMessages() {
    if (this.loadingOlder || !this.hasMore || this.messages.length === 0) {
      return;
    }

    const oldestId = this.messages[0].id;
    const scrollEl = this.messageScroll?.nativeElement;
    const previousHeight = scrollEl?.scrollHeight ?? 0;

    this.loadingOlder = true;
    this.chatService.getMessages(this.roomId, 50, oldestId).subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to load older messages');
            return;
          }
          this.hasMore = response.hasMore;
          const older = this.crewId > 0
            ? await this.chatCrypto.decryptMessages(response.items ?? [], this.crewId)
            : response.items ?? [];
          this.messages = [...older, ...this.messages];
          setTimeout(() => {
            if (scrollEl) {
              scrollEl.scrollTop = scrollEl.scrollHeight - previousHeight;
            }
          }, 0);
        } finally {
          this.loadingOlder = false;
        }
      },
      error: () => {
        this.loadingOlder = false;
        this.toastService.error('Failed to load older messages');
      }
    });
  }

  private setupLazyLoadObserver() {
    this.intersectionObserver?.disconnect();
    if (!this.messageItems || this.messageItems.length === 0) {
      return;
    }

    const triggerIndex = Math.min(39, this.messages.length - 1);
    const triggerElement = this.messageItems.get(triggerIndex)?.nativeElement;
    if (!triggerElement) {
      return;
    }

    this.intersectionObserver = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) {
        this.loadOlderMessages();
      }
    }, {
      root: this.messageScroll?.nativeElement,
      threshold: 0.1
    });

    this.intersectionObserver.observe(triggerElement);
  }

  private scrollToBottom() {
    const scrollEl = this.messageScroll?.nativeElement;
    if (!scrollEl) {
      return;
    }
    scrollEl.scrollTop = scrollEl.scrollHeight;
  }
}

