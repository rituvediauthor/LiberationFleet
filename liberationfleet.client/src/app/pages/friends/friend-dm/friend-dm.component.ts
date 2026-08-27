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
import { CharCounterComponent } from '../../../components/char-counter/char-counter.component';
import { UserAvatarComponent } from '../../../components/user-avatar/user-avatar.component';
import { ToastService } from '../../../components/toast/toast.component';
import { FriendService } from '../../../services/friend.service';
import { ChatHubService } from '../../../services/chat-hub.service';
import { FriendDmCryptoService } from '../../../services/crypto/friend-dm-crypto.service';
import { ProfileService } from '../../../services/profile.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { AuthService } from '../../../services/auth.service';
import { NavigationService } from '../../../services/navigation.service';
import { NotificationContentService } from '../../../services/notification-content.service';
import { DirectMessage } from '../../../models/friend.model';
import { PendingAttachment, ProposalAttachment } from '../../../models/proposal.model';
import { pendingAttachmentsAllowSubmit } from '../../../utils/pending-attachment.util';
import { getUserIdFromToken } from '../../../utils/jwt.util';
import { TextFieldLimits } from '../../../utils/text-field-limits';
import { ReportContentDialogComponent } from '../../../components/report-content-dialog/report-content-dialog.component';
import { ComposerFooterPadDirective } from '../../../directives/composer-footer-pad.directive';
import { LocationHeaderComponent } from '../../../components/location-header/location-header.component';
import { injectLocationHeaderInfo } from '../../../utils/inject-location-header';
import { LocationHeaderInfo } from '../../../utils/location-header.util';

@Component({
  selector: 'app-friend-dm',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent,
    CharCounterComponent,
    ReportContentDialogComponent,
    UserAvatarComponent,
    LocationHeaderComponent,
    ComposerFooterPadDirective
  ],
  templateUrl: './friend-dm.component.html',
  styleUrl: './friend-dm.component.css'
})
export class FriendDmComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('messageScroll') messageScroll?: ElementRef<HTMLDivElement>;
  @ViewChildren('messageItem') messageItems?: QueryList<ElementRef<HTMLElement>>;

  friendUserId = 0;
  friendUsername = 'Friend';
  friendAvatarResourceId: string | null = null;
  private readonly baseLocationHeader = injectLocationHeaderInfo();

  get locationHeaderView(): LocationHeaderInfo | null {
    if (!this.baseLocationHeader) {
      return null;
    }
    const pageLabel = this.friendUsername?.trim() || this.baseLocationHeader.pageLabel;
    return { ...this.baseLocationHeader, pageLabel };
  }
  messages: DirectMessage[] = [];
  currentUserId: number | null = null;
  authorDisplayName = '';
  messageText = '';
  readonly messageMaxLength = TextFieldLimits.message;
  messageAttachments: PendingAttachment[] = [];
  keptEditAttachments: ProposalAttachment[] = [];
  editingMessageId: number | null = null;
  openMessageMenuId: number | null = null;
  composerFocused = false;
  composerUiMinimized = false;
  pickingFile = false;
  loading = true;
  loadingOlder = false;
  hasMore = false;
  sending = false;
  loadError = '';
  showReportDialog = false;
  reportTarget: DirectMessage | null = null;
  readonly canAttachFiles = true;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private friendService = inject(FriendService);
  private chatHub = inject(ChatHubService);
  private friendDmCrypto = inject(FriendDmCryptoService);
  private profileService = inject(ProfileService);
  private encryptionContent = inject(EncryptionContentService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private notificationContent = inject(NotificationContentService);
  private intersectionObserver?: IntersectionObserver;
  private hubSubscription?: Subscription;
  private hubUpdateSubscription?: Subscription;

  @HostListener('document:click')
  closeMenus() {
    this.openMessageMenuId = null;
  }

  ngOnInit() {
    this.friendUserId = Number(this.route.snapshot.paramMap.get('userId'));
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;

    this.notificationContent.markVisited(`/app/friends/messages/${this.friendUserId}`);

    this.hubSubscription = this.chatHub.directMessageReceived$.subscribe(event => {
      if (event.friendUserId === this.friendUserId) {
        void this.onMessageReceived(event.message as DirectMessage);
      }
    });

    this.hubUpdateSubscription = this.chatHub.directMessageUpdated$.subscribe(event => {
      if (event.friendUserId === this.friendUserId) {
        void this.onMessageUpdated(event.message as DirectMessage);
      }
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    void this.bootstrap();
  }

  ngAfterViewInit() {
    this.setupLazyLoadObserver();
    this.messageItems?.changes.subscribe(() => this.setupLazyLoadObserver());
  }

  ngOnDestroy() {
    this.intersectionObserver?.disconnect();
    this.hubSubscription?.unsubscribe();
    this.hubUpdateSubscription?.unsubscribe();
  }

  goBack() {
    this.navigation.back(['/app/friends']);
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

  startEditMessage(message: DirectMessage, event?: Event) {
    event?.stopPropagation();
    this.openMessageMenuId = null;
    this.editingMessageId = message.id;
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

  openReportMessage(message: DirectMessage, event?: Event) {
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
    void this.router.navigate(['/app/friends']);
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
    if (!this.canSend() || this.sending || this.friendUserId <= 0) {
      return;
    }

    this.sending = true;
    const plaintextBody = this.messageText.trim();
    const attachmentsSnapshot = [...this.messageAttachments];
    const keptSnapshot = [...this.keptEditAttachments];
    const editingId = this.editingMessageId;
    try {
      const encrypted = await this.friendDmCrypto.encryptMessagePayload(
        this.friendUserId,
        plaintextBody,
        this.authorDisplayName,
        attachmentsSnapshot,
        keptSnapshot
      );

      const request$ = editingId
        ? this.friendService.updateMessage(this.friendUserId, editingId, encrypted)
        : this.friendService.sendMessage(this.friendUserId, encrypted);

      request$.subscribe({
        next: response => {
          this.sending = false;
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to send message');
            return;
          }

          if (editingId != null) {
            this.messages = this.messages.map(message =>
              message.id === editingId
                ? {
                    ...message,
                    body: plaintextBody,
                    hasEncryptedContent: true,
                    encryptedPayload: {
                      keyVersion: encrypted.keyVersion,
                      nonce: encrypted.nonce,
                      ciphertext: encrypted.ciphertext
                    }
                  }
                : message
            );
            void this.resolveLoadedMessageAttachments();
          } else if (response.messageId) {
            void this.appendLocalSentMessage(
              response.messageId,
              plaintextBody,
              encrypted
            );
          }

          this.messageText = '';
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

  private async appendLocalSentMessage(
    messageId: number,
    body: string,
    encrypted: { nonce: string; ciphertext: string; keyVersion: number }
  ): Promise<void> {
    if (this.messages.some(existing => existing.id === messageId)) {
      return;
    }

    const local: DirectMessage = {
      id: messageId,
      authorUserId: this.currentUserId ?? 0,
      authorUsername: this.authorDisplayName || 'You',
      authorAvatarResourceId: null,
      createdAt: new Date().toISOString(),
      hasEncryptedContent: true,
      encryptedPayload: {
        keyVersion: encrypted.keyVersion,
        nonce: encrypted.nonce,
        ciphertext: encrypted.ciphertext
      },
      body
    };

    const decrypted = await this.decryptMessage(local);
    if (this.messages.some(existing => existing.id === messageId)) {
      return;
    }

    this.messages = [...this.messages, decrypted];
    setTimeout(() => this.scrollToBottom(), 0);
    void this.resolveLoadedMessageAttachments();
  }

  isOwnMessage(message: DirectMessage): boolean {
    return this.currentUserId != null && message.authorUserId === this.currentUserId;
  }

  private async bootstrap(): Promise<void> {
    try {
      await this.encryptionContent.whenReady();
      await this.chatHub.ensureConnected();
      this.loadLatestMessages(true);
    } catch {
      this.loading = false;
      this.loadError = 'Failed to prepare messaging';
    }
  }

  private async onMessageUpdated(message: DirectMessage) {
    const decrypted = await this.decryptMessage(message);
    this.messages = this.messages.map(existing =>
      existing.id === decrypted.id ? decrypted : existing
    );
  }

  private async onMessageReceived(message: DirectMessage) {
    if (message.id <= 0 || this.messages.some(existing => existing.id === message.id)) {
      return;
    }

    const scrollEl = this.messageScroll?.nativeElement;
    const shouldStickToBottom = scrollEl
      ? scrollEl.scrollHeight - scrollEl.scrollTop - scrollEl.clientHeight < 80
      : true;

    const decrypted = await this.decryptMessage(message);
    this.messages = [...this.messages, decrypted];

    if (shouldStickToBottom) {
      setTimeout(() => this.scrollToBottom(), 0);
    }
  }

  private loadLatestMessages(scrollToBottom: boolean) {
    this.loading = true;
    this.loadError = '';
    this.friendService.getMessages(this.friendUserId, 50).subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.loadError = response.message || 'Failed to load messages';
            return;
          }
          this.friendUsername = response.friendUsername || 'Friend';
          this.friendAvatarResourceId = response.friendAvatarResourceId ?? null;
          this.hasMore = response.hasMore;
          this.messages = await this.decryptMessages(response.items ?? []);
          this.loading = false;
          if (scrollToBottom) {
            setTimeout(() => this.scrollToBottom(), 0);
          }
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

  private loadOlderMessages() {
    if (this.loadingOlder || !this.hasMore || this.messages.length === 0) {
      return;
    }

    const oldestId = this.messages[0].id;
    const scrollEl = this.messageScroll?.nativeElement;
    const previousHeight = scrollEl?.scrollHeight ?? 0;

    this.loadingOlder = true;
    this.friendService.getMessages(this.friendUserId, 50, oldestId).subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to load older messages');
            return;
          }
          this.hasMore = response.hasMore;
          const older = await this.decryptMessages(response.items ?? []);
          this.messages = [...older, ...this.messages];
          this.loadingOlder = false;
          setTimeout(() => {
            if (scrollEl) {
              scrollEl.scrollTop = scrollEl.scrollHeight - previousHeight;
            }
          }, 0);
          void this.resolveLoadedMessageAttachments();
        } catch {
          this.loadingOlder = false;
        }
      },
      error: () => {
        this.loadingOlder = false;
        this.toastService.error('Failed to load older messages');
      }
    });
  }

  private async resolveLoadedMessageAttachments(): Promise<void> {
    try {
      this.messages = await this.friendDmCrypto.resolveMessageAttachments(
        this.messages,
        this.friendUserId
      );
    } catch {
      // Media resolve is best-effort; text already rendered.
    }
  }

  private async decryptMessages(messages: DirectMessage[]): Promise<DirectMessage[]> {
    return this.friendDmCrypto.decryptMessages(messages, this.friendUserId);
  }

  private async decryptMessage(message: DirectMessage): Promise<DirectMessage> {
    return this.friendDmCrypto.decryptSingleMessage(message, this.friendUserId);
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
