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
import { UserAvatarComponent } from '../../../components/user-avatar/user-avatar.component';
import { ToastService } from '../../../components/toast/toast.component';
import { FriendService } from '../../../services/friend.service';
import { ChatHubService } from '../../../services/chat-hub.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { AuthService } from '../../../services/auth.service';
import { NavigationService } from '../../../services/navigation.service';
import { DirectMessage } from '../../../models/friend.model';
import { PendingAttachment, ProposalAttachment } from '../../../models/proposal.model';
import { getUserIdFromToken } from '../../../utils/jwt.util';
import { ReportContentDialogComponent } from '../../../components/report-content-dialog/report-content-dialog.component';

@Component({
  selector: 'app-friend-dm',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent,
    FallibleFooterComponent,
    ReportContentDialogComponent,
    UserAvatarComponent
  ],
  templateUrl: './friend-dm.component.html',
  styleUrl: './friend-dm.component.css'
})
export class FriendDmComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('messageScroll') messageScroll?: ElementRef<HTMLDivElement>;
  @ViewChildren('messageItem') messageItems?: QueryList<ElementRef<HTMLElement>>;

  friendUserId = 0;
  friendUsername = 'Friend';
  messages: DirectMessage[] = [];
  crewId = 0;
  canAttachFiles = false;
  currentUserId: number | null = null;
  authorDisplayName = '';
  messageText = '';
  messageAttachments: PendingAttachment[] = [];
  keptEditAttachments: ProposalAttachment[] = [];
  editingMessageId: number | null = null;
  openMessageMenuId: number | null = null;
  composerFocused = false;
  pickingFile = false;
  loading = true;
  loadingOlder = false;
  hasMore = false;
  sending = false;
  loadError = '';
  showReportDialog = false;
  reportTarget: DirectMessage | null = null;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private friendService = inject(FriendService);
  private chatHub = inject(ChatHubService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private encryptionContent = inject(EncryptionContentService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
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

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        this.canAttachFiles = membership.canAttachFilesToCrewContent ?? false;
        await this.encryptionContent.whenReady();
        if (this.crewId > 0) {
          void this.chatHub.joinCrew(this.crewId);
        }
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
    this.hubUpdateSubscription?.unsubscribe();
  }

  goBack() {
    this.navigation.back(['/app/friends']);
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

  startEditMessage(message: DirectMessage, event?: Event) {
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
      const encrypted = await this.chatCrypto.encryptMessagePayload(
        { crewId: this.crewId },
        this.messageText.trim(),
        this.authorDisplayName,
        this.messageAttachments,
        this.keptEditAttachments
      );

      const request$ = this.editingMessageId
        ? this.friendService.updateMessage(this.friendUserId, this.editingMessageId, encrypted)
        : this.friendService.sendMessage(this.friendUserId, encrypted);

      request$.subscribe({
        next: response => {
          this.sending = false;
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to send message');
            return;
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

  isOwnMessage(message: DirectMessage): boolean {
    return this.currentUserId != null && message.authorUserId === this.currentUserId;
  }

  private async onMessageUpdated(message: DirectMessage) {
    const decrypted = this.crewId > 0
      ? await this.decryptMessage(message)
      : message;
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

    const decrypted = this.crewId > 0
      ? await this.decryptMessage(message)
      : message;
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
          this.hasMore = response.hasMore;
          this.messages = this.crewId > 0
            ? await this.decryptMessages(response.items ?? [])
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
    this.friendService.getMessages(this.friendUserId, 50, oldestId).subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to load older messages');
            return;
          }
          this.hasMore = response.hasMore;
          const older = this.crewId > 0
            ? await this.decryptMessages(response.items ?? [])
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

  private async decryptMessages(messages: DirectMessage[]): Promise<DirectMessage[]> {
    const chatMessages = messages.map(message => ({
      ...message,
      authorUsername: message.authorUsername
    }));
    const decrypted = await this.chatCrypto.decryptMessages(chatMessages, { crewId: this.crewId });
    return decrypted as DirectMessage[];
  }

  private async decryptMessage(message: DirectMessage): Promise<DirectMessage> {
    const decrypted = await this.chatCrypto.decryptSingleMessage(message, { crewId: this.crewId });
    return decrypted as DirectMessage;
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
