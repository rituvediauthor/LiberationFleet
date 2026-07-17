import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
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
import { FallibleFooterComponent } from '../../../components/fallible-footer/fallible-footer.component';
import { AdultContentGateComponent } from '../../../components/adult-content-gate/adult-content-gate.component';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatService } from '../../../services/chat.service';
import { ChatHubService } from '../../../services/chat-hub.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { FleetService } from '../../../services/fleet.service';
import { ProfileService } from '../../../services/profile.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { AuthService } from '../../../services/auth.service';
import { CrewmateService } from '../../../services/crewmate.service';
import { CryptoApiService } from '../../../services/crypto/crypto-api.service';
import { ChatMessage } from '../../../models/chat.model';
import { PendingAttachment, ProposalAttachment } from '../../../models/proposal.model';
import { getUserIdFromToken } from '../../../utils/jwt.util';
import { AdultContentService } from '../../../services/adult-content.service';
import { NavigationService } from '../../../services/navigation.service';
import { NotificationContentService } from '../../../services/notification-content.service';
import { ContentPreferenceService } from '../../../services/content-preference.service';
import { MentionAutocompleteDirective } from '../../../directives/mention-autocomplete.directive';
import { MentionTextComponent } from '../../../components/mention-text/mention-text.component';
import { ReportContentDialogComponent } from '../../../components/report-content-dialog/report-content-dialog.component';
import { UserAvatarComponent } from '../../../components/user-avatar/user-avatar.component';
import { truncateNotificationPreview } from '../../../utils/notification-preview.util';

@Component({
  selector: 'app-chat-text',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent,
    FallibleFooterComponent,
    AdultContentGateComponent,
    MentionAutocompleteDirective,
    MentionTextComponent,
    ReportContentDialogComponent,
    UserAvatarComponent
  ],
  templateUrl: './chat-text.component.html',
  styleUrl: './chat-text.component.css'
})
export class ChatTextComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('messageScroll') messageScroll?: ElementRef<HTMLDivElement>;
  @ViewChildren('messageItem') messageItems?: QueryList<ElementRef<HTMLElement>>;

  roomId = 0;
  roomName = 'Chat';
  anonymousModeEnabled = false;
  canToggleAnonymousMode = false;
  canModerateAttachments = false;
  canAttachFiles = false;
  messages: ChatMessage[] = [];
  crewId = 0;
  fleetId = 0;
  currentUserId: number | null = null;
  authorDisplayName = '';
  messageText = '';
  mentionedUserIds: number[] = [];
  messageAttachments: PendingAttachment[] = [];
  keptEditAttachments: ProposalAttachment[] = [];
  editingMessageId: number | null = null;
  openMessageMenuId: number | null = null;
  showReportDialog = false;
  reportTarget: ChatMessage | null = null;
  composerFocused = false;
  pickingFile = false;
  loading = true;
  loadingOlder = false;
  hasMore = false;
  sending = false;
  loadError = '';
  showAdultGate = false;
  contentRevealed = true;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private notificationContent = inject(NotificationContentService);
  private chatService = inject(ChatService);
  private chatHub = inject(ChatHubService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
  private profileService = inject(ProfileService);
  private encryptionContent = inject(EncryptionContentService);
  private authService = inject(AuthService);
  private crewmateService = inject(CrewmateService);
  private cryptoApi = inject(CryptoApiService);
  private toastService = inject(ToastService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private intersectionObserver?: IntersectionObserver;
  private hubSubscription?: Subscription;
  private hubUpdateSubscription?: Subscription;

  @HostListener('document:click')
  closeMenus() {
    this.openMessageMenuId = null;
  }

  ngOnInit() {
    this.roomId = Number(this.route.snapshot.paramMap.get('id'));
    const isFleetScope = this.route.snapshot.data['scope'] === 'fleet'
      || this.router.url.startsWith('/app/fleet/chats');
    if (this.roomId) {
      const prefix = isFleetScope ? '/app/fleet/chats' : '/app/crew/chats';
      this.notificationContent.markVisited(`${prefix}/${this.roomId}`, this.roomId);
    }
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;

    this.hubSubscription = this.chatHub.messageReceived$.subscribe(message => {
      void this.onMessageReceived(message);
    });

    this.hubUpdateSubscription = this.chatHub.messageUpdated$.subscribe(message => {
      void this.onMessageUpdated(message);
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
        if (this.currentUserId) {
          this.crewmateService.getCrewmateProfile(this.currentUserId).subscribe({
            next: response => {
              if (response.success && response.profile) {
                this.canModerateAttachments = response.profile.canModerateAttachments;
              }
            }
          });
        }
      }
    });

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        this.canAttachFiles = membership.canAttachFilesToCrewContent ?? false;
        await this.encryptionContent.whenReady();
        if (this.crewId > 0) {
          void this.chatHub.joinCrew(this.crewId);
        }
        void this.chatHub.joinRoom(this.roomId);
        const isFleetScope = this.route.snapshot.data['scope'] === 'fleet'
          || this.router.url.startsWith('/app/fleet/chats');
        if (isFleetScope) {
          this.canAttachFiles = membership.canAttachFilesToFleetContent ?? false;
          this.fleetService.getStatus().subscribe({
            next: status => {
              this.fleetId = status.fleetId ?? 0;
              this.contentPreferenceService.ensureLoaded().subscribe({
                next: () => this.loadRoomName()
              });
            },
            error: () => {
              this.loading = false;
              this.loadError = 'Failed to load fleet membership';
            }
          });
          return;
        }
        this.contentPreferenceService.ensureLoaded().subscribe({
          next: () => this.loadRoomName()
        });
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
    this.hubUpdateSubscription?.unsubscribe();
    void this.chatHub.leaveRoom();
  }

  goBack() {
    const isFleetScope = this.route.snapshot.data['scope'] === 'fleet'
      || this.router.url.startsWith('/app/fleet/chats');
    this.navigation.back([isFleetScope ? '/app/fleet/chats' : '/app/crew/chats']);
  }

  onAdultGateConfirmed() {
    const resourceKey = this.adultContentService.resourceKey('chat', this.roomId);
    this.adultContentService.grantConsent(resourceKey);
    this.showAdultGate = false;
    this.contentRevealed = true;
    this.loadLatestMessages(true);
  }

  onAdultGateDeclined() {
    this.showAdultGate = false;
    this.goBack();
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
    return this.composerFocused || this.pickingFile || this.messageAttachments.length > 0 || this.editingMessageId != null;
  }

  canSend(): boolean {
    return Boolean(this.messageText.trim() || this.messageAttachments.length > 0 || this.keptEditAttachments.length > 0);
  }

  toggleMessageMenu(messageId: number, event: Event) {
    event.stopPropagation();
    this.openMessageMenuId = this.openMessageMenuId === messageId ? null : messageId;
  }

  startEditMessage(message: ChatMessage, event?: Event) {
    event?.stopPropagation();
    this.openMessageMenuId = null;
    this.editingMessageId = message.id;
    this.messageText = message.body ?? '';
    this.keptEditAttachments = (message.resolvedAttachments ?? []).map(attachment => ({
      resourceId: attachment.resourceId,
      type: attachment.type,
      fileName: attachment.fileName,
      mimeType: attachment.mimeType
    }));
    this.messageAttachments = [];
    this.composerFocused = true;
  }

  openReportMessage(message: ChatMessage, event?: Event) {
    event?.stopPropagation();
    this.openMessageMenuId = null;
    this.reportTarget = message;
    this.showReportDialog = true;
  }

  onReportDismissed() {
    this.showReportDialog = false;
    this.reportTarget = null;
  }

  onReportSubmitted() {
    this.showReportDialog = false;
    this.reportTarget = null;
  }

  get reportMediaIds(): string[] {
    return (this.reportTarget?.resolvedAttachments ?? []).map(a => a.resourceId);
  }

  cancelEditMessage() {
    this.editingMessageId = null;
    this.messageText = '';
    this.messageAttachments = [];
    this.keptEditAttachments = [];
    this.composerFocused = false;
  }

  removeKeptAttachment(index: number) {
    this.keptEditAttachments.splice(index, 1);
  }

  async sendMessage() {
    if (!this.canSend() || this.sending || this.crewId <= 0) {
      return;
    }

    this.sending = true;
    try {
      const isFleetScope = this.route.snapshot.data['scope'] === 'fleet'
        || this.router.url.startsWith('/app/fleet/chats');
      const text = this.messageText.trim();
      const cryptoScope = isFleetScope && this.fleetId > 0
        ? { fleetId: this.fleetId }
        : { crewId: this.crewId };

      const encrypted = await this.chatCrypto.encryptMessagePayload(
        cryptoScope,
        text,
        this.authorDisplayName,
        this.messageAttachments,
        this.keptEditAttachments
      );

      const request$ = this.editingMessageId
        ? this.chatService.updateMessage(this.roomId, this.editingMessageId, {
            ...encrypted,
            mentionedUserIds: this.mentionedUserIds
          })
        : this.chatService.sendMessage(this.roomId, {
            ...encrypted,
            body: truncateNotificationPreview(text),
            mentionedUserIds: this.mentionedUserIds
          });

      request$.subscribe({
        next: response => {
          this.sending = false;
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to send message');
            return;
          }
          this.messageText = '';
          this.mentionedUserIds = [];
          this.messageAttachments = [];
          this.keptEditAttachments = [];
          this.editingMessageId = null;
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

  displayAuthor(message: ChatMessage): string {
    if (this.isAnonymousAuthor(message)) {
      return 'Anonymous';
    }

    return message.authorUsername;
  }

  isAnonymousAuthor(message: ChatMessage): boolean {
    return this.anonymousModeEnabled && !this.isOwnMessage(message);
  }

  toggleAnonymousMode() {
    if (!this.canToggleAnonymousMode) {
      return;
    }

    const nextValue = !this.anonymousModeEnabled;
    this.chatService.toggleAnonymousMode(this.roomId, nextValue).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to toggle anonymous mode');
          return;
        }

        this.anonymousModeEnabled = nextValue;
        this.toastService.success(response.message);
      },
      error: () => this.toastService.error('Failed to toggle anonymous mode')
    });
  }

  onAttachmentDeleted(resourceId: string, message: ChatMessage) {
    const attachment = message.resolvedAttachments?.find(item => item.resourceId === resourceId);
    if (!attachment || !this.crewId) {
      return;
    }

    const contentType = attachment.type === 'video'
      ? 'VideoAsset'
      : attachment.type === 'audio'
        ? 'AudioAsset'
        : 'ImageAsset';

    this.cryptoApi.deleteAttachment(contentType, resourceId, this.crewId).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to delete attachment');
          return;
        }

        message.resolvedAttachments = (message.resolvedAttachments ?? [])
          .filter(item => item.resourceId !== resourceId);
        this.toastService.success('Attachment deleted.');
      },
      error: () => this.toastService.error('Failed to delete attachment')
    });
  }

  private getCryptoScope() {
    const isFleetScope = this.route.snapshot.data['scope'] === 'fleet'
      || this.router.url.startsWith('/app/fleet/chats');
    return isFleetScope && this.fleetId > 0
      ? { fleetId: this.fleetId }
      : { crewId: this.crewId };
  }

  private loadRoomName() {
    this.chatService.getRoom(this.roomId).subscribe({
      next: async response => {
        const room = response.room;
        if (!room) {
          this.loading = false;
          this.loadError = response.message || 'Chat room not found';
          return;
        }

        const resourceKey = this.adultContentService.resourceKey('chat', room.id);
        if (this.adultContentService.needsAgeGate(room.isAdultContent, resourceKey)) {
          this.showAdultGate = true;
          this.contentRevealed = false;
          this.loading = false;
          return;
        }

        this.anonymousModeEnabled = !!room.anonymousModeEnabled;
        this.canToggleAnonymousMode = !!room.canToggleAnonymousMode;

        const decrypted = this.crewId > 0
          ? await this.chatCrypto.decryptRoom(room, this.getCryptoScope())
          : room;
        this.roomName = decrypted.name || 'Chat';
        this.loadLatestMessages(true);
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Chat room not found';
      }
    });
  }

  private async onMessageUpdated(message: ChatMessage) {
    const decrypted = this.crewId > 0
      ? await this.chatCrypto.decryptSingleMessage(message, this.getCryptoScope())
      : message;
    this.messages = this.messages.map(existing =>
      existing.id === decrypted.id ? decrypted : existing
    );
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
      ? await this.chatCrypto.decryptSingleMessage(message, this.getCryptoScope())
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
            ? await this.chatCrypto.decryptMessages(response.items ?? [], this.getCryptoScope())
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
            ? await this.chatCrypto.decryptMessages(response.items ?? [], this.getCryptoScope())
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

