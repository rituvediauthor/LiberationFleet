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
import { MentionAutocompleteDirective } from '../../../directives/mention-autocomplete.directive';
import { MentionTextComponent } from '../../../components/mention-text/mention-text.component';
import { ProposalAttachmentDisplayComponent } from '../../../components/proposal-attachment-display/proposal-attachment-display.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { AttachPermissionNoteComponent } from '../../../components/attach-permission-note/attach-permission-note.component';
import { CharCounterComponent } from '../../../components/char-counter/char-counter.component';
import { ComposerFooterPadDirective } from '../../../directives/composer-footer-pad.directive';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { AuthService } from '../../../services/auth.service';
import { NavigationService } from '../../../services/navigation.service';
import { NotificationContentService } from '../../../services/notification-content.service';
import { LibraryRequestMessage } from '../../../models/library.model';
import { PendingAttachment } from '../../../models/proposal.model';
import { pendingAttachmentsAllowSubmit } from '../../../utils/pending-attachment.util';
import { getUserIdFromToken } from '../../../utils/jwt.util';
import { LocationHeaderComponent } from '../../../components/location-header/location-header.component';
import { injectLocationHeaderInfo } from '../../../utils/inject-location-header';
import { LocationHeaderInfo } from '../../../utils/location-header.util';

@Component({
  selector: 'app-library-request-chat',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MentionAutocompleteDirective,
    MentionTextComponent,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent,
    AttachPermissionNoteComponent,
    CharCounterComponent,
    ComposerFooterPadDirective,
    LocationHeaderComponent
  ],
  templateUrl: './library-request-chat.component.html',
  styleUrl: './library-request-chat.component.css'
})
export class LibraryRequestChatComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('messageScroll') messageScroll?: ElementRef<HTMLDivElement>;
  @ViewChildren('messageItem') messageItems?: QueryList<ElementRef<HTMLElement>>;

  pageTitle = 'Request messages';
  private readonly baseLocationHeader = injectLocationHeaderInfo();

  get locationHeaderView(): LocationHeaderInfo | null {
    if (!this.baseLocationHeader) {
      return null;
    }
    const pageLabel = this.pageTitle?.trim() || this.baseLocationHeader.pageLabel;
    return { ...this.baseLocationHeader, pageLabel };
  }

  requestId = 0;
  crewId = 0;
  currentUserId: number | null = null;
  authorDisplayName = '';
  messages: LibraryRequestMessage[] = [];
  messageText = '';
  readonly messageMaxLength = 5000;
  mentionedUserIds: number[] = [];
  messageAttachments: PendingAttachment[] = [];
  canAttachFiles = false;
  composerFocused = false;
  composerUiMinimized = false;
  pickingFile = false;
  loading = true;
  loadingOlder = false;
  hasMore = false;
  sending = false;
  loadError = '';

  private readonly pageSize = 50;
  private intersectionObserver?: IntersectionObserver;
  private messageItemsSubscription?: { unsubscribe(): void };

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private notificationContent = inject(NotificationContentService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private authService = inject(AuthService);

  ngOnInit() {
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;
    this.requestId = Number(this.route.snapshot.paramMap.get('id'));

    if (!this.requestId) {
      this.loading = false;
      this.loadError = 'Invalid request.';
      return;
    }

    this.notificationContent.markVisited(
      `/app/crew/library-of-things/requests/${this.requestId}`,
      this.requestId
    );

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        // A library request chat is a private 1:1 conversation between the requester
        // and the item holder (like a DM), so attachments aren't gated by the
        // crew-content moderation permission.
        this.canAttachFiles = true;
        await this.encryptionContent.whenReady();
        this.loadRequestTitle();
        this.loadLatestMessages(true);
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Failed to load crew membership.';
      }
    });
  }

  ngAfterViewInit() {
    this.messageItemsSubscription = this.messageItems?.changes.subscribe(() => {
      this.setupLazyLoadObserver();
    });
    this.setupLazyLoadObserver();
  }

  ngOnDestroy() {
    this.intersectionObserver?.disconnect();
    this.messageItemsSubscription?.unsubscribe();
  }

  goBack() {
    this.navigation.back(['/app/crew/library-of-things/requests', String(this.requestId)]);
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
      if (this.messageAttachments.length > 0 || this.messageText.trim()) {
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
    return this.composerFocused
      || Boolean(this.messageText.trim())
      || this.messageAttachments.length > 0;
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
    const hasContent = Boolean(this.messageText.trim()) || this.messageAttachments.length > 0;
    return hasContent
      && this.messageText.length <= this.messageMaxLength
      && pendingAttachmentsAllowSubmit(this.messageAttachments);
  }

  onAttachmentsChange() {
    // Triggers change detection so send button gating updates during compress.
  }

  isOwnMessage(message: LibraryRequestMessage): boolean {
    return this.currentUserId != null && message.authorUserId === this.currentUserId;
  }

  async sendMessage() {
    if (!this.canSend() || this.sending || this.crewId <= 0) {
      return;
    }

    this.sending = true;
    const pendingAttachments = [...this.messageAttachments];
    try {
      const encrypted = await this.chatCrypto.encryptMessagePayload(
        { crewId: this.crewId },
        this.messageText.trim(),
        this.authorDisplayName,
        pendingAttachments
      );

      this.libraryService.sendRequestMessage(this.requestId, {
        ...encrypted,
        mentionedUserIds: this.mentionedUserIds
      }).subscribe({
        next: response => {
          this.sending = false;
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to send message');
            return;
          }

          this.messageText = '';
          this.mentionedUserIds = [];
          this.messageAttachments = [];
          this.composerFocused = false;
          this.loadLatestMessages(true);
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

  private loadRequestTitle() {
    this.libraryService.getRequestDetail(this.requestId).subscribe({
      next: detail => {
        if (detail.title) {
          this.pageTitle = detail.title;
        }
      }
    });
  }

  private loadLatestMessages(scrollToBottom: boolean) {
    this.loading = true;
    this.loadError = '';

    this.libraryService.getRequestMessages(this.requestId, { limit: this.pageSize }).subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.loadError = response.message || 'Failed to load messages';
            return;
          }

          this.hasMore = response.hasMore;
          this.messages = await this.libraryCrypto.decryptRequestMessages(response.items, this.crewId);
          if (scrollToBottom) {
            setTimeout(() => this.scrollToBottom(), 0);
          }
          setTimeout(() => this.setupLazyLoadObserver(), 0);
        } catch {
          this.messages = [];
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
    this.libraryService.getRequestMessages(this.requestId, {
      limit: this.pageSize,
      beforeMessageId: oldestId
    }).subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to load older messages');
            return;
          }

          this.hasMore = response.hasMore;
          const older = await this.libraryCrypto.decryptRequestMessages(response.items, this.crewId);
          this.messages = [...older, ...this.messages];
          setTimeout(() => {
            if (scrollEl) {
              scrollEl.scrollTop = scrollEl.scrollHeight - previousHeight;
            }
            this.setupLazyLoadObserver();
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
    if (!this.messageItems || this.messages.length === 0 || !this.hasMore) {
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
