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
import { ChatMessage } from '../../../models/chat.model';
import { PendingAttachment, ProposalAttachment, ResolvedAttachment } from '../../../models/proposal.model';
import { waitForPendingAttachmentsReady } from '../../../utils/pending-attachment.util';
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
import { describeLoadError } from '../../../utils/http-error.util';

interface ChatOutboxEntry {
  localId: string;
  body: string;
  attachments: PendingAttachment[];
  keptAttachments: ProposalAttachment[];
  editingMessageId: number | null;
  mentionedUserIds: number[];
  isAnonymous: boolean;
  status: 'queued' | 'sending' | 'failed';
  error?: string;
}

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
  outbox: ChatOutboxEntry[] = [];
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
  private toastService = inject(ToastService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private storage = inject(AppStorageService);
  private intersectionObserver?: IntersectionObserver;
  private bottomStickObserver?: ResizeObserver;
  private preferStickToBottom = true;
  private outboxPumpRunning = false;
  private nextOptimisticId = -1;
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
      this.restoreComposeAnonymously();
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
    this.setupBottomStickObserver();
    this.messageItems?.changes.subscribe(() => this.setupLazyLoadObserver());
  }

  ngOnDestroy() {
    this.intersectionObserver?.disconnect();
    this.bottomStickObserver?.disconnect();
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
    // Allow send while uploads are still running — the outbox waits, then posts.
    return hasContent && this.messageText.length <= this.messageMaxLength;
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
    this.restoreComposeAnonymously();
  }

  removeKeptAttachment(index: number) {
    this.keptEditAttachments.splice(index, 1);
  }

  async sendMessage() {
    if (!this.canSend() || this.crewId <= 0) {
      return;
    }

    const localId = `local-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    const entry: ChatOutboxEntry = {
      localId,
      body: this.messageText.trim(),
      attachments: [...this.messageAttachments],
      keptAttachments: [...this.keptEditAttachments],
      editingMessageId: this.editingMessageId,
      mentionedUserIds: [...this.mentionedUserIds],
      isAnonymous: this.composeAnonymously,
      status: 'queued'
    };

    this.insertOptimisticMessage(entry);
    this.outbox = [...this.outbox, entry];
    this.messageText = '';
    this.mentionedUserIds = [];
    this.messageAttachments = [];
    this.keptEditAttachments = [];
    this.editingMessageId = null;
    this.composerFocused = false;
    this.restoreComposeAnonymously();
    this.preferStickToBottom = true;
    this.scrollToBottom();
    void this.pumpOutbox();
  }

  retryFailedMessage(message: ChatMessage, event?: Event) {
    event?.stopPropagation();
    if (!message.clientLocalId || message.sendStatus !== 'failed') {
      return;
    }
    const entry = this.outbox.find(item => item.localId === message.clientLocalId);
    if (!entry) {
      this.messages = this.messages.filter(item => item.clientLocalId !== message.clientLocalId);
      return;
    }
    this.messages = this.messages.map(item =>
      item.clientLocalId === message.clientLocalId
        ? { ...item, sendStatus: 'sending' }
        : item
    );
    this.retryOutboxEntry(entry);
  }

  dismissFailedMessage(message: ChatMessage, event?: Event) {
    event?.stopPropagation();
    if (!message.clientLocalId) {
      return;
    }
    const entry = this.outbox.find(item => item.localId === message.clientLocalId);
    this.outbox = this.outbox.filter(item => item.localId !== message.clientLocalId);
    if (entry?.editingMessageId != null || message.id > 0) {
      this.messages = this.messages.map(item =>
        item.clientLocalId === message.clientLocalId
          ? { ...item, clientLocalId: undefined, sendStatus: undefined }
          : item
      );
      return;
    }
    this.messages = this.messages.filter(item => item.clientLocalId !== message.clientLocalId);
  }

  retryOutboxEntry(entry: ChatOutboxEntry) {
    if (entry.status === 'sending') {
      return;
    }
    entry.status = 'queued';
    entry.error = undefined;
    this.outbox = [...this.outbox];
    this.markOptimisticStatus(entry.localId, 'sending');
    void this.pumpOutbox();
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
    this.persistComposeAnonymously();
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

  private composeAnonymouslyStorageKey(): string {
    return `lf.chat.anonymous.${this.roomId}`;
  }

  private restoreComposeAnonymously() {
    if (!this.roomId) {
      this.composeAnonymously = false;
      return;
    }
    this.composeAnonymously =
      this.storage.get(StorageScope.Persistent, this.composeAnonymouslyStorageKey()) === 'true';
  }

  private persistComposeAnonymously() {
    if (!this.roomId) {
      return;
    }
    this.storage.set(
      StorageScope.Persistent,
      this.composeAnonymouslyStorageKey(),
      this.composeAnonymously ? 'true' : 'false'
    );
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
    if (message.id <= 0) {
      return;
    }

    const shouldStickToBottom = this.preferStickToBottom || this.isNearBottom();
    const decrypted = this.crewId > 0
      ? await this.chatCrypto.decryptSingleMessage(message, this.getCryptoScope())
      : message;

    const existingById = this.messages.findIndex(existing => existing.id === decrypted.id);
    if (existingById >= 0) {
      const previous = this.messages[existingById];
      this.messages = this.messages.map((existing, index) =>
        index === existingById
          ? {
              ...decrypted,
              clientLocalId: previous.clientLocalId,
              sendStatus: undefined
            }
          : existing
      );
      return;
    }

    const optimisticIndex = this.messages.findIndex(existing =>
      !!existing.clientLocalId
      && existing.id < 0
      && this.isOptimisticMatch(existing, decrypted)
    );
    if (optimisticIndex >= 0) {
      const localId = this.messages[optimisticIndex].clientLocalId;
      this.outbox = this.outbox.filter(item => item.localId !== localId);
      this.messages = this.messages.map((existing, index) =>
        index === optimisticIndex
          ? { ...decrypted, clientLocalId: localId, sendStatus: undefined }
          : existing
      );
    } else {
      this.messages = [...this.messages, decrypted];
    }

    const ownMessage = this.isOwnMessage(decrypted);
    if (shouldStickToBottom || ownMessage) {
      this.preferStickToBottom = true;
      setTimeout(() => this.scrollToBottom(), 0);
      // Media decode can grow the bubble after the first stick.
      setTimeout(() => this.scrollToBottom(), 100);
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
            this.loading = false;
            return;
          }
          this.hasMore = response.hasMore;
          this.messages = this.crewId > 0
            ? await this.chatCrypto.decryptMessages(response.items ?? [], this.getCryptoScope())
            : response.items ?? [];
          // Keep the channel behind the loading state until media URLs are ready.
          await this.resolveLoadedMessageAttachments(false);
          this.loading = false;
          if (scrollToBottom && !this.highlightSeekActive) {
            this.preferStickToBottom = true;
            setTimeout(() => this.scrollToBottom(), 0);
          }
          this.continueHighlightSeek();
        } catch (error: unknown) {
          this.loadError = error instanceof Error ? error.message : 'Failed to decrypt messages';
          this.loading = false;
        }
      },
      error: (error: unknown) => {
        this.loading = false;
        this.loadError = describeLoadError(error, 'Failed to load messages');
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
            this.loadingOlder = false;
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
          void this.resolveLoadedMessageAttachments(false);
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
  private async resolveLoadedMessageAttachments(scrollAfter = false): Promise<void> {
    const scope = this.getCryptoScope();
    if (!scope.crewId && !scope.fleetId) {
      return;
    }

    const snapshot = this.messages;
    if (!snapshot.some(message => (message.resolvedAttachments?.length ?? 0) > 0)) {
      return;
    }

    try {
      const resolved = await this.chatCrypto.resolveMessageAttachments(snapshot, scope);
      const resolvedById = new Map(resolved.map(message => [message.id, message] as const));
      // Merge by id so hub messages that arrived during resolve are not dropped.
      this.messages = this.messages.map(message => {
        const next = resolvedById.get(message.id);
        if (!next) {
          return message;
        }
        return {
          ...message,
          resolvedAttachments: next.resolvedAttachments ?? message.resolvedAttachments
        };
      });

      if (scrollAfter || this.preferStickToBottom) {
        setTimeout(() => this.scrollToBottom(), 0);
        setTimeout(() => this.scrollToBottom(), 150);
      }
    } catch {
      // Media resolve is best-effort; text already rendered.
    }
  }

  private async pumpOutbox(): Promise<void> {
    if (this.outboxPumpRunning) {
      return;
    }
    this.outboxPumpRunning = true;
    try {
      while (true) {
        const entry = this.outbox.find(item => item.status === 'queued');
        if (!entry) {
          break;
        }
        await this.flushOutboxEntry(entry);
      }
    } finally {
      this.outboxPumpRunning = false;
      this.sending = this.outbox.some(item => item.status === 'sending' || item.status === 'queued');
    }
  }

  private async flushOutboxEntry(entry: ChatOutboxEntry): Promise<void> {
    entry.status = 'sending';
    entry.error = undefined;
    this.sending = true;
    this.outbox = [...this.outbox];
    this.markOptimisticStatus(entry.localId, 'sending');

    try {
      await waitForPendingAttachmentsReady(entry.attachments);

      const cryptoScope = this.getCryptoScope();
      const encrypted = await this.chatCrypto.encryptMessagePayload(
        cryptoScope,
        entry.body,
        entry.isAnonymous ? 'Anonymous' : this.authorDisplayName,
        entry.attachments,
        entry.keptAttachments
      );

      const response = entry.editingMessageId
        ? await new Promise<{ success: boolean; message?: string; messageId?: number }>((resolve, reject) => {
            this.chatService.updateMessage(this.roomId, entry.editingMessageId!, {
              ...encrypted,
              mentionedUserIds: entry.mentionedUserIds
            }).subscribe({ next: resolve, error: reject });
          })
        : await new Promise<{ success: boolean; message?: string; messageId?: number }>((resolve, reject) => {
            this.chatService.sendMessage(this.roomId, {
              ...encrypted,
              body: '',
              mentionedUserIds: entry.mentionedUserIds,
              isAnonymous: entry.isAnonymous
            }).subscribe({ next: resolve, error: reject });
          });

      if (!response.success) {
        entry.status = 'failed';
        entry.error = response.message || 'Failed to send message';
        this.outbox = [...this.outbox];
        this.markOptimisticStatus(entry.localId, 'failed');
        this.toastService.error(entry.error);
        return;
      }

      if (response.messageId && entry.isAnonymous) {
        this.recentOwnMessageIds.add(response.messageId);
      }

      if (response.messageId) {
        this.promoteOptimisticMessage(entry, response.messageId);
      } else {
        this.markOptimisticStatus(entry.localId, undefined);
      }

      this.outbox = this.outbox.filter(item => item.localId !== entry.localId);
      this.preferStickToBottom = true;
      this.scrollToBottom();
    } catch (error: unknown) {
      entry.status = 'failed';
      entry.error = error instanceof Error ? error.message : 'Failed to send message';
      this.outbox = [...this.outbox];
      this.markOptimisticStatus(entry.localId, 'failed');
      this.toastService.error(entry.error);
    }
  }

  private insertOptimisticMessage(entry: ChatOutboxEntry): void {
    const resolvedAttachments = this.buildOptimisticAttachments(entry.attachments, entry.keptAttachments);

    if (entry.editingMessageId != null) {
      this.messages = this.messages.map(message =>
        message.id === entry.editingMessageId
          ? {
              ...message,
              body: entry.body,
              resolvedAttachments,
              isAnonymous: entry.isAnonymous,
              authorUsername: entry.isAnonymous ? 'Anonymous' : (this.authorDisplayName || message.authorUsername),
              clientLocalId: entry.localId,
              sendStatus: 'sending'
            }
          : message
      );
      return;
    }

    const optimistic: ChatMessage = {
      id: this.nextOptimisticId--,
      authorUserId: this.currentUserId ?? 0,
      authorUsername: entry.isAnonymous ? 'Anonymous' : (this.authorDisplayName || 'You'),
      authorAvatarResourceId: entry.isAnonymous ? null : undefined,
      createdAt: new Date().toISOString(),
      hasEncryptedContent: true,
      body: entry.body,
      resolvedAttachments,
      isAnonymous: entry.isAnonymous,
      clientLocalId: entry.localId,
      sendStatus: 'sending'
    };
    this.messages = [...this.messages, optimistic];
  }

  private buildOptimisticAttachments(
    pending: PendingAttachment[],
    kept: ProposalAttachment[]
  ): ResolvedAttachment[] {
    const fromKept: ResolvedAttachment[] = kept.map(attachment => ({ ...attachment }));
    const fromPending: ResolvedAttachment[] = pending.map(attachment => ({
      resourceId: attachment.resourceId,
      type: attachment.type,
      fileName: attachment.fileName,
      encrypted: attachment.encrypted,
      dataUrl: attachment.previewUrl,
      posterUrl: attachment.thumbnailUrl
    }));
    return [...fromKept, ...fromPending];
  }

  private promoteOptimisticMessage(entry: ChatOutboxEntry, messageId: number): void {
    this.messages = this.messages.map(message => {
      if (message.clientLocalId !== entry.localId) {
        return message;
      }
      return {
        ...message,
        id: entry.editingMessageId ?? messageId,
        sendStatus: undefined
      };
    });
  }

  private markOptimisticStatus(localId: string, status: 'sending' | 'failed' | undefined): void {
    this.messages = this.messages.map(message =>
      message.clientLocalId === localId
        ? { ...message, sendStatus: status }
        : message
    );
  }

  private isOptimisticMatch(optimistic: ChatMessage, incoming: ChatMessage): boolean {
    if (!this.isOwnMessage(optimistic)) {
      return false;
    }
    if (!!optimistic.isAnonymous !== !!incoming.isAnonymous) {
      return false;
    }
    const optimisticBody = (optimistic.body ?? '').trim();
    const incomingBody = (incoming.body ?? '').trim();
    if (optimisticBody || incomingBody) {
      return optimisticBody === incomingBody;
    }
    const optimisticCount = optimistic.resolvedAttachments?.length ?? 0;
    const incomingCount = incoming.resolvedAttachments?.length ?? 0;
    return optimisticCount > 0 && optimisticCount === incomingCount;
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

  private setupBottomStickObserver() {
    this.bottomStickObserver?.disconnect();
    const scrollEl = this.messageScroll?.nativeElement;
    if (!scrollEl) {
      return;
    }

    const body = scrollEl.querySelector('.chat-channel-body');
    if (!body) {
      return;
    }

    this.bottomStickObserver = new ResizeObserver(() => {
      if (this.preferStickToBottom) {
        scrollEl.scrollTop = scrollEl.scrollHeight;
      }
    });
    this.bottomStickObserver.observe(body);

    scrollEl.addEventListener('scroll', () => {
      this.preferStickToBottom = this.isNearBottom(120);
    }, { passive: true });
  }

  private isNearBottom(thresholdPx = 80): boolean {
    const scrollEl = this.messageScroll?.nativeElement;
    if (!scrollEl) {
      return true;
    }
    return scrollEl.scrollHeight - scrollEl.scrollTop - scrollEl.clientHeight < thresholdPx;
  }

  private scrollToBottom() {
    const scrollEl = this.messageScroll?.nativeElement;
    if (!scrollEl) {
      return;
    }
    this.preferStickToBottom = true;
    scrollEl.scrollTop = scrollEl.scrollHeight;
  }
}

