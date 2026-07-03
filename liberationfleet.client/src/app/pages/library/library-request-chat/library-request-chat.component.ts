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
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { LibraryRequestMessage } from '../../../models/library.model';

@Component({
  selector: 'app-library-request-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent],
  templateUrl: './library-request-chat.component.html',
  styleUrl: './library-request-chat.component.css'
})
export class LibraryRequestChatComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('messageScroll') messageScroll?: ElementRef<HTMLDivElement>;
  @ViewChildren('messageItem') messageItems?: QueryList<ElementRef<HTMLElement>>;

  backButton!: ActionBarButton;
  requestId = 0;
  crewId = 0;
  authorDisplayName = '';
  messages: LibraryRequestMessage[] = [];
  messageText = '';
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
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);

  ngOnInit() {
    this.requestId = Number(this.route.snapshot.paramMap.get('id'));
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/library-of-things/requests', this.requestId])
    };

    if (!this.requestId) {
      this.loading = false;
      this.loadError = 'Invalid request.';
      return;
    }

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
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

  async sendMessage() {
    if (!this.messageText.trim() || this.sending || this.crewId <= 0) {
      return;
    }

    this.sending = true;
    try {
      const encrypted = await this.chatCrypto.encryptMessagePayload(
        this.crewId,
        this.messageText.trim(),
        this.authorDisplayName
      );

      this.libraryService.sendRequestMessage(this.requestId, encrypted).subscribe({
        next: async response => {
          this.sending = false;
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to send message');
            return;
          }

          this.messageText = '';
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

  private getScrollContainer(): HTMLElement | null {
    return this.messageScroll?.nativeElement?.closest('.page-content') as HTMLElement | null;
  }

  private loadOlderMessages() {
    if (this.loadingOlder || !this.hasMore || this.messages.length === 0) {
      return;
    }

    const oldestId = this.messages[0].id;
    const scrollEl = this.getScrollContainer();
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
      root: this.getScrollContainer(),
      threshold: 0.1
    });

    this.intersectionObserver.observe(triggerElement);
  }

  private scrollToBottom() {
    const scrollEl = this.getScrollContainer();
    if (!scrollEl) {
      return;
    }
    scrollEl.scrollTop = scrollEl.scrollHeight;
  }
}
