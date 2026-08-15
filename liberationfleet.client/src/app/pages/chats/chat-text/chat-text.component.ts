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
import { AttachPermissionNoteComponent } from '../../../components/attach-permission-note/attach-permission-note.component';
import { CharCounterComponent } from '../../../components/char-counter/char-counter.component';
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
import { pendingAttachmentsAllowSubmit } from '../../../utils/pending-attachment.util';
import { getUserIdFromToken } from '../../../utils/jwt.util';
import { AdultContentService } from '../../../services/adult-content.service';
import { NavigationService } from '../../../services/navigation.service';
import { NotificationContentService } from '../../../services/notification-content.service';
import { ContentPreferenceService } from '../../../services/content-preference.service';
import { MentionAutocompleteDirective } from '../../../directives/mention-autocomplete.directive';
import { NotificationTargetDirective } from '../../../directives/notification-target.directive';
import { MentionTextComponent } from '../../../components/mention-text/mention-text.component';
import { ReportContentDialogComponent } from '../../../components/report-content-dialog/report-content-dialog.component';
import { KickReasonDialogComponent } from '../../../components/kick-reason-dialog/kick-reason-dialog.component';
import { UserAvatarComponent } from '../../../components/user-avatar/user-avatar.component';
import { AccessibleDialogDirective } from '../../../directives/accessible-dialog.directive';
import { truncateNotificationPreview } from '../../../utils/notification-preview.util';
import {
  clearNotificationHighlightParams,
  readNotificationHighlightId
} from '../../../utils/notification-deep-link.util';
import { AppStorageService, StorageScope } from '../../../services/storage/app-storage.service';
import { ANONYMOUS_CHAT_REMINDER_DISMISSED_KEY } from '../../../services/storage/storage-keys';
import { ComposerFooterPadDirective } from '../../../directives/composer-footer-pad.directive';
import { LocationHeaderComponent } from '../../../components/location-header/location-header.component';
import { injectLocationHeaderInfo } from '../../../utils/inject-location-header';
import { LocationHeaderInfo } from '../../../utils/location-header.util';

@Component({
  selector: 'app-chat-text',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent,
    AttachPermissionNoteComponent,
    CharCounterComponent,
    AdultContentGateComponent,
    MentionAutocompleteDirective,
    MentionTextComponent,
    ReportContentDialogComponent,
    KickReasonDialogComponent,
    UserAvatarComponent,
    AccessibleDialogDirective,
    LocationHeaderComponent,
    NotificationTargetDirective,
    ComposerFooterPadDirective
  ],
  templateUrl: './chat-text.component.html',
  styleUrl: './chat-text.component.css'
})
export class ChatTextComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('messageScroll') messageScroll?: ElementRef<HTMLDivElement>;
  @ViewChildren('messageItem') messageItems?: QueryList<ElementRef<HTMLElement>>;

  roomId = 0;
  roomName = 'Chat';
  private readonly baseLocationHeader = injectLocationHeaderInfo();

  get locationHeaderView(): LocationHeaderInfo | null {
    if (!this.baseLocationHeader) {
      return null;
    }
    const pageLabel = this.roomName?.trim() || this.baseLocationHeader.pageLabel;
    return { ...this.baseLocationHeader, pageLabel };
  }

  get isFleetScope(): boolean {
    return this.route.snapshot.data['scope'] === 'fleet'
      || this.router.url.startsWith('/app/fleet/chats');
  }

  composeAnonymously = false;
  canModerateAttachments = false;
  canAttachFiles = false;
  messages: ChatMessage[] = [];
  crewId = 0;
  fleetId = 0;
  currentUserId: number | null = null;
  authorDisplayName = '';
  messageText = '';
  readonly messageMaxLength = 5000;
  mentionedUserIds: number[] = [];
  messageAttachments: PendingAttachment[] = [];
  keptEditAttachments: ProposalAttachment[] = [];
  editingMessageId: number | null = null;
  openMessageMenuId: number | null = null;
  showReportDialog = false;
  reportTarget: ChatMessage | null = null;
  showKickReasonDialog = false;
  pendingKickMessageId: number | null = null;
  showAnonymousReminderDialog = false;
  dontRemindAnonymousMode = false;
  dismissAnonymousReminderBound = () => this.confirmAnonymousReminder();
  composerFocused = false;
  composerUiMinimized = false;
  pickingFile = false;
  loading = true;
  loadingOlder = false;
  hasMore = false;
  sending = false;
  loadError = '';
  showAdultGate = false;
  contentRevealed = true;
  highlightId: number | null = null;
  notifyPrefix = '';

  /** Own anonymous messages from this session (SignalR strips author id). */
  private recentOwnMessageIds = new Set<number>();
  private highlightSeekPagesLeft = 0;
  private highlightSeekActive = false;

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
  private storage = inject(AppStorageService);
  private intersectionObserver?: IntersectionObserver;
  private hubSubscription?: Subscription;
  private hubUpdateSubscription?: Subscription;
  private hubDeleteSubscription?: Subscription;

  @HostListener('document:click')
  closeMenus() {
    this.openMessageMenuId = null;
  }

  ngOnInit() {
    this.roomId = Number(this.route.snapshot.paramMap.get('id'));
    this.highlightId = readNotificationHighlightId(this.route);
    clearNotificationHighlightParams(this.router, this.route);
    const isFleetScope = this.route.snapshot.data['scope'] === 'fleet'
      || this.router.url.startsWith('/app/fleet/chats');
    const prefix = isFleetScope ? '/app/fleet/chats' : '/app/crew/chats';
    this.notifyPrefix = `${prefix}/${this.roomId}`;
    if (this.roomId) {
      this.notificationContent.markVisited(this.notifyPrefix, this.roomId);
    }
    if (this.highlightId) {
      this.highlightSeekPagesLeft = 5;
      this.highlightSeekActive = true;
    }
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;

    this.hubSubscription = this.chatHub.messageReceived$.subscribe(message => {
      void this.onMessageReceived(message);
    });

    this.hubUpdateSubscription = this.chatHub.messageUpdated$.subscribe(message => {
      void this.onMessageUpdated(message);
    });
    this.hubDeleteSubscription = this.chatHub.messageDeleted$.subscribe(event => {
      if (event.roomId === this.roomId) {
        this.messages = this.messages.filter(existing => existing.id !== event.messageId);
      }
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
    this.hubDeleteSubscription?.unsubscribe();
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
    this.composerUiMinimized = false;
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
      this.composerUiMinimized = false;
      this.composerFocused = true;
      return;
    }
    // Defer collapse: iOS can close the dialog flag before attachments are pushed.
    setTimeout(() => {
      if (this.pickingFile) {
        return;
      }
      if (this.messageAttachments.length > 0 || this.messageText.trim() || this.editingMessageId != null) {
        this.composerUiMinimized = false;
        this.composerFocused = true;
        return;
      }
      this.composerFocused = false;
    }, 0);
  }

  get composerExpanded(): boolean {
    if (this.composerUiMinimized) {
      return false;
    }
    return this.composerFocused || this.pickingFile || this.messageAttachments.length > 0 || this.editingMessageId != null;
  }

  minimizeComposer() {
    this.composerUiMinimized = true;
    this.composerFocused = false;
    const active = document.activeElement as HTMLElement | null;
    active?.blur?.();
  }

  onBackAction() {
    if (this.composerExpanded) {
      this.minimizeComposer();
      return;
    }
    this.goBack();
  }

  canSend(): boolean {
    const hasContent = Boolean(
      this.messageText.trim() || this.messageAttachments.length > 0 || this.keptEditAttachments.length > 0
    );
    return hasContent
      && this.messageText.length <= this.messageMaxLength
      && pendingAttachmentsAllowSubmit(this.messageAttachments);
  }

  onAttachmentsChange() {
    // Triggers change detection so send button gating updates during compress.
  }

  toggleMessageMenu(messageId: number, event: Event) {
    event.stopPropagation();
    this.openMessageMenuId = this.openMessageMenuId === messageId ? null : messageId;
  }

  startEditMessage(message: ChatMessage, event?: Event) {
    event?.stopPropagation();
    this.openMessageMenuId = null;
    this.editingMessageId = message.id;
    this.composeAnonymously = !!message.isAnonymous;
    this.messageText = message.body ?? '';
    this.keptEditAttachments = (message.resolvedAttachments ?? []).map(attachment => ({
      resourceId: attachment.resourceId,
      type: attachment.type,
      fileName: attachment.fileName,
      mimeType: attachment.mimeType,
      encrypted: attachment.encrypted,
      posterResourceId: attachment.posterResourceId
    }));
    this.messageAttachments = [];
    this.composerUiMinimized = false;
    this.composerFocused = true;
  }

  deleteOwnMessage(message: ChatMessage, event?: Event) {
    event?.stopPropagation();
    this.openMessageMenuId = null;
    if (!this.isOwnMessage(message) || this.sending) {
      return;
    }

    this.chatService.deleteMessage(this.roomId, message.id).subscribe({
      next: result => {
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to delete message');
          return;
        }
        this.messages = this.messages.filter(existing => existing.id !== message.id);
        if (this.editingMessageId === message.id) {
          this.cancelEditMessage();
        }
      },
      error: () => this.toastService.error('Failed to delete message')
    });
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
        this.composeAnonymously ? 'Anonymous' : this.authorDisplayName,
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
            mentionedUserIds: this.mentionedUserIds,
            isAnonymous: this.composeAnonymously
          });

      request$.subscribe({
        next: response => {
          this.sending = false;
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to send message');
            return;
          }
          if (response.messageId && this.composeAnonymously) {
            this.recentOwnMessageIds.add(response.messageId);
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
    if (this.recentOwnMessageIds.has(message.id)) {
      return true;
    }
    return this.currentUserId != null && message.authorUserId === this.currentUserId;
  }

  displayAuthor(message: ChatMessage): string {
    if (this.isAnonymousAuthor(message)) {
      return 'Anonymous';
    }

    return message.authorUsername;
  }

  isAnonymousAuthor(message: ChatMessage): boolean {
    return !!message.isAnonymous;
  }

  canKickAnonymous(message: ChatMessage): boolean {
    return !!message.canKick && !this.isOwnMessage(message);
  }

  toggleComposeAnonymously() {
    const enabling = !this.composeAnonymously;
    this.composeAnonymously = enabling;
    if (enabling && !this.isAnonymousReminderDismissed()) {
      this.dontRemindAnonymousMode = false;
      this.showAnonymousReminderDialog = true;
    }
  }

  confirmAnonymousReminder() {
    if (this.dontRemindAnonymousMode) {
      this.storage.set(StorageScope.Persistent, ANONYMOUS_CHAT_REMINDER_DISMISSED_KEY, 'true');
    }
    this.showAnonymousReminderDialog = false;
  }

  onAnonymousReminderBackdrop(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.confirmAnonymousReminder();
    }
  }

  private isAnonymousReminderDismissed(): boolean {
    return this.storage.get(StorageScope.Persistent, ANONYMOUS_CHAT_REMINDER_DISMISSED_KEY) === 'true';
  }

  openKickFromMessage(message: ChatMessage, event: Event) {
    event.stopPropagation();
    this.closeMenus();
    this.pendingKickMessageId = message.id;
    this.showKickReasonDialog = true;
  }

  onKickReasonCancelled() {
    this.showKickReasonDialog = false;
    this.pendingKickMessageId = null;
  }

  onKickReasonConfirmed(reason: string) {
    const messageId = this.pendingKickMessageId;
    this.showKickReasonDialog = false;
    this.pendingKickMessageId = null;
    if (messageId == null) {
      return;
    }

    this.chatService.kickFromMessage(this.roomId, messageId, reason).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to submit kick proposal');
          return;
        }
        this.toastService.success(response.message || 'Kick proposal submitted.');
        if (response.proposalId) {
          this.router.navigate(['/app/crew/proposals', response.proposalId]);
        }
      },
      error: () => this.toastService.error('Failed to submit kick proposal')
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
          this.loading = false;
          if (scrollToBottom && !this.highlightSeekActive) {
            setTimeout(() => this.scrollToBottom(), 0);
          }
          this.continueHighlightSeek();
          void this.resolveLoadedMessageAttachments();
        } catch (error: unknown) {
          this.loadError = error instanceof Error ? error.message : 'Failed to decrypt messages';
          this.loading = false;
        }
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Failed to load messages';
      }
    });
  }

  private loadOlderMessages(options?: { preserveScroll?: boolean; forHighlightSeek?: boolean }) {
    if (this.loadingOlder || !this.hasMore || this.messages.length === 0) {
      return;
    }

    const oldestId = this.messages[0].id;
    const scrollEl = this.messageScroll?.nativeElement;
    const previousHeight = scrollEl?.scrollHeight ?? 0;
    const preserveScroll = options?.preserveScroll !== false;

    this.loadingOlder = true;
    this.chatService.getMessages(this.roomId, 50, oldestId).subscribe({
      next: async response => {
        try {
          if (!response.success) {
            if (!options?.forHighlightSeek) {
              this.toastService.error(response.message || 'Failed to load older messages');
            }
            this.highlightSeekActive = false;
            return;
          }
          this.hasMore = response.hasMore;
          const older = this.crewId > 0
            ? await this.chatCrypto.decryptMessages(response.items ?? [], this.getCryptoScope())
            : response.items ?? [];
          this.messages = [...older, ...this.messages];
          this.loadingOlder = false;
          if (preserveScroll && !options?.forHighlightSeek) {
            setTimeout(() => {
              if (scrollEl) {
                scrollEl.scrollTop = scrollEl.scrollHeight - previousHeight;
              }
            }, 0);
          }
          if (options?.forHighlightSeek) {
            this.continueHighlightSeek();
          }
          void this.resolveLoadedMessageAttachments();
        } catch {
          this.loadingOlder = false;
        }
      },
      error: () => {
        this.loadingOlder = false;
        if (!options?.forHighlightSeek) {
          this.toastService.error('Failed to load older messages');
        }
        this.highlightSeekActive = false;
      }
    });
  }

  /** Fill video/image URLs after text is already on screen. */
  private async resolveLoadedMessageAttachments(): Promise<void> {
    const scope = this.getCryptoScope();
    if (!scope.crewId && !scope.fleetId) {
      return;
    }
    try {
      const resolved = await this.chatCrypto.resolveMessageAttachments(this.messages, scope);
      this.messages = resolved;
    } catch {
      // Media resolve is best-effort; text already rendered.
    }
  }

  private continueHighlightSeek() {
    if (!this.highlightSeekActive || !this.highlightId) {
      return;
    }

    if (this.messages.some(message => message.id === this.highlightId)) {
      this.highlightSeekActive = false;
      return;
    }

    if (this.highlightSeekPagesLeft <= 0 || !this.hasMore) {
      this.highlightSeekActive = false;
      return;
    }

    this.highlightSeekPagesLeft -= 1;
    this.loadOlderMessages({ preserveScroll: false, forHighlightSeek: true });
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

