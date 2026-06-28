import { Component, OnInit, OnDestroy, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { CrewDiscussionService } from '../../../services/crew-discussion.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { DiscussionConfig, DiscussionKind, getDiscussionConfig } from '../../../config/discussion.config';
import { DiscussionListItem } from '../../../models/crew-discussion.model';
import { ProposalListItem } from '../../../models/proposal.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../services/encryption-content.service';
import { MutedContentItem, MutedContentType } from '../../../models/notification.model';
import { NotificationService } from '../../../services/notification.service';

@Component({
  selector: 'app-discussion-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './discussion-list.component.html',
  styleUrl: './discussion-list.component.css'
})
export class DiscussionListComponent implements OnInit, OnDestroy {
  config!: DiscussionConfig;
  items: DiscussionListItem[] = [];
  loading = true;
  errorMessage = '';
  crewId = 0;
  openMenuItemId: number | null = null;
  mutedItems: MutedContentItem[] = [];
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private discussionService = inject(CrewDiscussionService);
  private discussionCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private notificationService = inject(NotificationService);
  private encryptionContent = inject(EncryptionContentService);
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    const kind = this.route.snapshot.data['discussionKind'] as DiscussionKind;
    this.config = getDiscussionConfig(kind);

    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadPosts());

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate([this.config.backRoute])
    };

    this.createButton = {
      label: `Create ${this.config.label}`,
      type: 'primary',
      onClick: () => this.router.navigate([this.config.createRoute])
    };

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        this.loadMutes();
        this.loadPosts();
        this.encryptionReload?.markInitialLoadDone();
      },
      error: () => {
        this.errorMessage = 'Failed to load crew membership';
        this.loading = false;
      }
    });
  }

  ngOnDestroy() {
    this.encryptionReload?.subscription.unsubscribe();
  }

  @HostListener('document:click')
  closeMenus() {
    this.openMenuItemId = null;
  }

  muteContentType(): MutedContentType {
    return this.config.kind === 'forums' ? 'Forum' : 'Project';
  }

  isItemMuted(itemId: number): boolean {
    return this.notificationService.isMuted(this.mutedItems, this.muteContentType(), itemId);
  }

  toggleMenu(itemId: number, event: Event) {
    event.stopPropagation();
    this.openMenuItemId = this.openMenuItemId === itemId ? null : itemId;
  }

  toggleMute(item: DiscussionListItem, event: Event) {
    event.stopPropagation();
    this.openMenuItemId = null;
    const contentType = this.muteContentType();
    const muted = !this.isItemMuted(item.id);
    this.notificationService.setMute(contentType, item.id, muted).subscribe({
      next: response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update mute setting');
          return;
        }
        if (muted) {
          this.mutedItems = [...this.mutedItems, { contentType, resourceId: item.id }];
          this.toastService.success(`${this.config.label} muted`);
        } else {
          this.mutedItems = this.mutedItems.filter(
            entry => !(entry.contentType === contentType && entry.resourceId === item.id)
          );
          this.toastService.success(`${this.config.label} unmuted`);
        }
      },
      error: () => this.toastService.error('Failed to update mute setting')
    });
  }

  formatActivity(date: Date): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  openPost(item: DiscussionListItem) {
    this.router.navigate([this.config.detailRoute(item.id)]);
  }

  private loadMutes() {
    this.notificationService.getMutes().subscribe({
      next: response => {
        if (response.success) {
          this.mutedItems = response.items ?? [];
        }
      }
    });
  }

  private loadPosts() {
    this.loading = true;
    this.errorMessage = '';

    this.discussionService.getPosts(this.config).subscribe({
      next: async items => {
        try {
          if (this.crewId > 0) {
            this.items = await this.discussionCrypto.decryptListItems(
              items as ProposalListItem[],
              this.crewId
            ) as DiscussionListItem[];
          } else {
            this.items = items;
          }
        } catch (error: unknown) {
          this.items = [];
          this.errorMessage = error instanceof Error
            ? error.message
            : `Failed to decrypt ${this.config.labelPlural.toLowerCase()}`;
          this.toastService.error(this.errorMessage);
        } finally {
          this.loading = false;
        }
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.message ?? `Failed to load ${this.config.labelPlural.toLowerCase()}`;
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
