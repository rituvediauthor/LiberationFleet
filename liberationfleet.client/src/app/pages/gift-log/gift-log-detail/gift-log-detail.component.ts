import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { GiftService } from '../../../services/gift.service';
import { GiftLogCryptoService } from '../../../services/crypto/gift-log-crypto.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { GiftComment, GiftDetail, GiftLogEntry, GiftVerificationAction, ContentLiker } from '../../../models/gift.model';
import { ProposalComment } from '../../../models/proposal.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../services/encryption-content.service';
import { AuthService } from '../../../services/auth.service';
import { getUserIdFromToken } from '../../../utils/jwt.util';
import { NavigationService } from '../../../services/navigation.service';
import { NotificationContentService } from '../../../services/notification-content.service';
import { MentionAutocompleteDirective } from '../../../directives/mention-autocomplete.directive';
import { NotificationTargetDirective } from '../../../directives/notification-target.directive';
import { MentionTextComponent } from '../../../components/mention-text/mention-text.component';
import { UserAvatarComponent } from '../../../components/user-avatar/user-avatar.component';
import { ForumEngagementBarComponent } from '../../../components/forum-engagement-bar/forum-engagement-bar.component';
import { ForumCommentLikeComponent } from '../../../components/forum-comment-like/forum-comment-like.component';
import { ContentLikersDialogComponent } from '../../../components/content-likers-dialog/content-likers-dialog.component';
import { CharCounterComponent } from '../../../components/char-counter/char-counter.component';
import { ProposalAttachmentDisplayComponent } from '../../../components/proposal-attachment-display/proposal-attachment-display.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { AttachPermissionNoteComponent } from '../../../components/attach-permission-note/attach-permission-note.component';
import { truncateNotificationPreview } from '../../../utils/notification-preview.util';
import {
  clearNotificationHighlightParams,
  readNotificationHighlightId
} from '../../../utils/notification-deep-link.util';
import { ComposerFooterPadDirective } from '../../../directives/composer-footer-pad.directive';
import { LocationHeaderComponent } from '../../../components/location-header/location-header.component';
import { injectLocationHeaderInfo } from '../../../utils/inject-location-header';
import { PendingAttachment } from '../../../models/proposal.model';
import { pendingAttachmentsAllowSubmit } from '../../../utils/pending-attachment.util';

@Component({
  selector: 'app-gift-log-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MentionAutocompleteDirective,
    MentionTextComponent,
    UserAvatarComponent,
    ForumEngagementBarComponent,
    ForumCommentLikeComponent,
    ContentLikersDialogComponent,
    CharCounterComponent,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent,
    AttachPermissionNoteComponent,
    LocationHeaderComponent,
    NotificationTargetDirective,
    ComposerFooterPadDirective
  ],
  templateUrl: './gift-log-detail.component.html',
  styleUrl: './gift-log-detail.component.css'
})
export class GiftLogDetailComponent implements OnInit, OnDestroy {
  @ViewChild('detailScroll') detailScroll?: ElementRef<HTMLElement>;

  locationHeaderInfo = injectLocationHeaderInfo();
  gift: GiftDetail | null = null;
  loading = true;
  loadError = '';
  crewId = 0;
  canAttachFiles = false;
  authorDisplayName = '';
  commentText = '';
  readonly commentMaxLength = 10000;
  mentionedUserIds: number[] = [];
  commentFocused = false;
  commentUiMinimized = false;
  pickingFile = false;
  replyParentId: number | null = null;
  commentAttachments: PendingAttachment[] = [];
  posting = false;
  likingGift = false;
  likingCommentId: number | null = null;
  verifyingGiftId: number | null = null;
  completionPlatformSelection: number | '' = '';
  currentUserId: number | null = null;
  highlightId: number | null = null;
  notifyPrefix = '';
  likersDialogOpen = false;
  likersDialogLoading = false;
  likersDialogItems: ContentLiker[] = [];
  likersDialogTitle = 'Liked by';

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private notificationContent = inject(NotificationContentService);
  private giftService = inject(GiftService);
  private giftLogCrypto = inject(GiftLogCryptoService);
  private discussionCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private authService = inject(AuthService);
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    const giftId = this.giftId;
    this.highlightId = readNotificationHighlightId(this.route);
    clearNotificationHighlightParams(this.router, this.route);
    if (this.highlightId == null && giftId && this.navigation.cameFromNotifications()) {
      this.highlightId = giftId;
    }
    this.notifyPrefix = `/app/crew/gift-log/${giftId}`;
    if (giftId) {
      this.notificationContent.markVisited(this.notifyPrefix, giftId);
    }

    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;

    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadGift());

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        this.canAttachFiles = membership.canAttachFilesToCrewContent ?? false;
        await this.encryptionContent.whenReady();
        this.loadGift();
        this.encryptionReload?.markInitialLoadDone();
      },
      error: () => {
        this.loading = false;
        this.toastService.error('Failed to load crew membership');
      }
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });
  }

  ngOnDestroy() {
    this.encryptionReload?.subscription.unsubscribe();
  }

  get giftId(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  @HostListener('document:click')
  closeMenus() {
    // Reserved for future comment menus.
  }

  goBack() {
    this.navigation.back(['/app/crew/gift-log']);
  }

  onCommentFocus() {
    this.commentUiMinimized = false;
    this.commentFocused = true;
  }

  onCommentBlur() {
    setTimeout(() => {
      if (this.pickingFile) {
        return;
      }
      if (!this.commentText.trim() && this.commentAttachments.length === 0) {
        this.commentFocused = false;
        this.replyParentId = null;
      }
    }, 150);
  }

  onFileDialogOpenChange(open: boolean) {
    this.pickingFile = open;
    if (open) {
      this.commentUiMinimized = false;
      this.commentFocused = true;
      return;
    }
    setTimeout(() => {
      if (this.pickingFile) {
        return;
      }
      if (this.commentAttachments.length > 0 || this.commentText.trim() || this.replyParentId != null) {
        this.commentUiMinimized = false;
        this.commentFocused = true;
        return;
      }
      this.commentFocused = false;
      this.replyParentId = null;
    }, 150);
  }

  onAttachmentsChange() {
    // Triggers change detection so post button gating updates during compress.
  }

  get commentExpanded(): boolean {
    if (this.commentUiMinimized) {
      return false;
    }
    return this.commentFocused || !!this.replyParentId || this.pickingFile || this.commentAttachments.length > 0;
  }

  minimizeCommentComposer() {
    this.commentUiMinimized = true;
    this.commentFocused = false;
    const active = document.activeElement as HTMLElement | null;
    active?.blur?.();
  }

  onBackAction() {
    if (this.commentExpanded) {
      this.minimizeCommentComposer();
      return;
    }
    this.goBack();
  }

  formatCommentAuthor(comment: GiftComment, siblingReplies?: GiftComment[]): string {
    if (!comment.replyToCommentId) {
      return comment.authorUsername;
    }

    const targetName = comment.replyToUsername
      ?? siblingReplies?.find(reply => reply.id === comment.replyToCommentId)?.authorUsername
      ?? 'User';
    return `${comment.authorUsername} > ${targetName}`;
  }

  startReply(comment: GiftComment) {
    this.replyParentId = comment.id;
    this.commentUiMinimized = false;
    this.commentFocused = true;
  }

  formatTimestamp(date: Date): string {
    return new Date(date).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    });
  }

  hasAction(action: GiftVerificationAction): boolean {
    return this.gift?.availableActions?.includes(action) ?? false;
  }

  canCompleteTransfer(): boolean {
    return !!this.completionPlatformSelection;
  }

  verifyGift(action: GiftVerificationAction) {
    if (!this.gift || this.verifyingGiftId) {
      return;
    }

    let paymentPlatformId: number | undefined;
    if (action === 'completeTransfer') {
      paymentPlatformId = Number(this.completionPlatformSelection);
      if (!paymentPlatformId) {
        this.toastService.error('Select a payment platform before completing this gift.');
        return;
      }
    }

    this.verifyingGiftId = this.gift.id;
    this.giftService.verifyGift(this.gift.id, action, paymentPlatformId).subscribe({
      next: async result => {
        this.verifyingGiftId = null;
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to update gift');
          return;
        }

        this.toastService.success(result.message || 'Gift updated');
        this.completionPlatformSelection = '';
        if (result.entry) {
          await this.applyUpdatedEntry(result.entry);
        }
        this.loadGift({ silent: true });
      },
      error: () => {
        this.verifyingGiftId = null;
        this.toastService.error('Failed to update gift');
      }
    });
  }

  toggleGiftLike() {
    if (!this.gift || this.likingGift) {
      return;
    }
    this.likingGift = true;
    this.giftService.toggleGiftLike(this.gift.id).subscribe({
      next: response => {
        this.likingGift = false;
        if (!response.success || !this.gift) {
          this.toastService.error(response.message || 'Failed to update like');
          return;
        }
        this.gift.likedByCurrentUser = response.liked;
        this.gift.likeCount = response.likeCount;
      },
      error: () => {
        this.likingGift = false;
        this.toastService.error('Failed to update like');
      }
    });
  }

  toggleCommentLike(comment: GiftComment) {
    if (!this.gift || this.likingCommentId === comment.id) {
      return;
    }
    this.likingCommentId = comment.id;
    this.giftService.toggleGiftCommentLike(this.gift.id, comment.id).subscribe({
      next: response => {
        this.likingCommentId = null;
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to update like');
          return;
        }
        comment.likedByCurrentUser = response.liked;
        comment.likeCount = response.likeCount;
      },
      error: () => {
        this.likingCommentId = null;
        this.toastService.error('Failed to update like');
      }
    });
  }

  openGiftLikers() {
    if (!this.gift) {
      return;
    }
    this.likersDialogTitle = 'Liked by';
    this.likersDialogOpen = true;
    this.likersDialogLoading = true;
    this.likersDialogItems = [];
    this.giftService.getGiftLikers(this.gift.id).subscribe({
      next: items => {
        this.likersDialogLoading = false;
        this.likersDialogItems = items;
      },
      error: err => {
        this.likersDialogLoading = false;
        this.likersDialogOpen = false;
        this.toastService.error(err?.message ?? 'Failed to load likers');
      }
    });
  }

  openCommentLikers(comment: GiftComment) {
    if (!this.gift) {
      return;
    }
    this.likersDialogTitle = 'Liked by';
    this.likersDialogOpen = true;
    this.likersDialogLoading = true;
    this.likersDialogItems = [];
    this.giftService.getGiftCommentLikers(this.gift.id, comment.id).subscribe({
      next: items => {
        this.likersDialogLoading = false;
        this.likersDialogItems = items;
      },
      error: err => {
        this.likersDialogLoading = false;
        this.likersDialogOpen = false;
        this.toastService.error(err?.message ?? 'Failed to load likers');
      }
    });
  }

  closeLikersDialog() {
    this.likersDialogOpen = false;
    this.likersDialogItems = [];
  }

  toggleReplies(comment: GiftComment) {
    if (comment.repliesExpanded && comment.replies) {
      comment.repliesExpanded = false;
      return;
    }

    if (comment.replies && comment.replies.length > 0) {
      comment.repliesExpanded = true;
      return;
    }

    this.giftService.getGiftCommentReplies(this.giftId, comment.id).subscribe({
      next: async replies => {
        comment.replies = this.crewId > 0
          ? await this.discussionCrypto.decryptComments(
            replies as ProposalComment[],
            this.crewId
          ) as GiftComment[]
          : replies;
        comment.repliesExpanded = true;
      },
      error: () => this.toastService.error('Failed to load replies')
    });
  }

  canPostComment(): boolean {
    return Boolean(
      (this.commentText.trim() || this.commentAttachments.length > 0)
      && this.commentText.length <= this.commentMaxLength
      && pendingAttachmentsAllowSubmit(this.commentAttachments)
    );
  }

  async postComment() {
    if (!this.gift || !this.canPostComment() || this.posting || this.crewId <= 0) {
      return;
    }
    if (!pendingAttachmentsAllowSubmit(this.commentAttachments)) {
      this.toastService.error('Wait for attachments to finish processing, or cancel them.');
      return;
    }

    const body = this.commentText.trim();
    const pendingAttachments = [...this.commentAttachments];
    const parentCommentId = this.replyParentId;

    this.posting = true;
    try {
      const encrypted = await this.discussionCrypto.encryptCommentPayload(
        this.crewId,
        {
          body,
          authorDisplayName: this.authorDisplayName
        },
        pendingAttachments
      );

      this.giftService.createGiftComment(this.gift.id, {
        parentCommentId,
        ...encrypted,
        notificationPreview: truncateNotificationPreview(body),
        mentionedUserIds: this.mentionedUserIds
      }).subscribe({
        next: result => {
          this.posting = false;
          if (result.success) {
            if (result.commentId) {
              this.insertPostedComment(result.commentId, body, pendingAttachments, parentCommentId);
            } else {
              this.loadGift({ silent: true });
            }
            this.commentText = '';
            this.mentionedUserIds = [];
            this.commentAttachments = [];
            this.commentFocused = false;
            this.replyParentId = null;
            this.toastService.success('Comment posted');
            return;
          }
          this.toastService.error(result.message || 'Failed to post comment');
        },
        error: () => {
          this.posting = false;
          this.toastService.error('Failed to post comment');
        }
      });
    } catch {
      this.posting = false;
      this.toastService.error('Failed to encrypt comment');
    }
  }

  retryLoad() {
    this.loadGift();
  }

  private insertPostedComment(
    commentId: number,
    body: string,
    pendingAttachments: PendingAttachment[],
    parentCommentId: number | null
  ) {
    if (!this.gift) {
      return;
    }

    const authorUserId = this.currentUserId ?? 0;
    const { threadRootId, replyToCommentId, replyToUsername } = parentCommentId
      ? this.resolveReplyTargets(parentCommentId)
      : { threadRootId: null as number | null, replyToCommentId: null as number | null, replyToUsername: null as string | null };
    const newComment: GiftComment = {
      id: commentId,
      authorUserId,
      authorUsername: this.authorDisplayName,
      parentCommentId: threadRootId,
      replyToCommentId,
      replyToUsername,
      createdAt: new Date(),
      replyCount: 0,
      hasEncryptedContent: true,
      body,
      resolvedAttachments: pendingAttachments.map(attachment => ({
        resourceId: attachment.resourceId,
        type: attachment.type,
        fileName: attachment.file?.name,
        mimeType: attachment.file?.type,
        dataUrl: attachment.previewUrl
      }))
    };

    if (!parentCommentId) {
      this.gift = {
        ...this.gift,
        commentCount: (this.gift.commentCount ?? 0) + 1,
        comments: [newComment, ...this.gift.comments]
      };
      return;
    }

    this.gift = {
      ...this.gift,
      commentCount: (this.gift.commentCount ?? 0) + 1,
      comments: this.gift.comments.map(comment => {
        if (comment.id !== threadRootId) {
          return comment;
        }

        return {
          ...comment,
          replyCount: comment.replyCount + 1,
          repliesExpanded: true,
          replies: [...(comment.replies ?? []), newComment]
        };
      })
    };
  }

  private resolveReplyTargets(parentCommentId: number): {
    threadRootId: number;
    replyToCommentId: number | null;
    replyToUsername: string | null;
  } {
    const topLevel = this.gift!.comments.find(comment => comment.id === parentCommentId);
    if (topLevel) {
      return { threadRootId: parentCommentId, replyToCommentId: null, replyToUsername: null };
    }

    for (const comment of this.gift!.comments) {
      const reply = comment.replies?.find(item => item.id === parentCommentId);
      if (reply) {
        return {
          threadRootId: comment.id,
          replyToCommentId: parentCommentId,
          replyToUsername: reply.authorUsername
        };
      }
    }

    return { threadRootId: parentCommentId, replyToCommentId: null, replyToUsername: null };
  }

  private async applyUpdatedEntry(updated: GiftLogEntry) {
    if (!this.gift) {
      return;
    }

    let entry = updated;
    if (this.crewId > 0) {
      try {
        const decrypted = await this.giftLogCrypto.decryptEntries([updated], this.crewId);
        entry = decrypted[0] ?? updated;
      } catch {
        // Keep server-provided fields if decrypt fails.
      }
    }

    this.gift = {
      ...this.gift,
      ...entry,
      timestamp: entry.timestamp instanceof Date ? entry.timestamp : new Date(entry.timestamp),
      comments: this.gift.comments
    };

    if (entry.availableActions?.includes('completeTransfer') && entry.completionPlatformOptions?.length === 1) {
      this.completionPlatformSelection = entry.completionPlatformOptions[0].id;
    }
  }

  private expandHighlightedReply() {
    if (!this.gift || !this.highlightId || this.highlightId === this.gift.id) {
      return;
    }

    if (this.gift.comments.some(comment => comment.id === this.highlightId)) {
      return;
    }

    for (const comment of this.gift.comments) {
      if (comment.replies?.some(reply => reply.id === this.highlightId)) {
        comment.repliesExpanded = true;
        return;
      }
    }

    const candidates = this.gift.comments.filter(comment => comment.replyCount > 0);
    const tryNext = (index: number) => {
      if (index >= candidates.length || !this.gift) {
        return;
      }
      const comment = candidates[index];
      if (comment.replies?.length) {
        if (comment.replies.some(reply => reply.id === this.highlightId)) {
          comment.repliesExpanded = true;
        } else {
          tryNext(index + 1);
        }
        return;
      }
      this.giftService.getGiftCommentReplies(this.giftId, comment.id).subscribe({
        next: async replies => {
          comment.replies = this.crewId > 0
            ? await this.discussionCrypto.decryptComments(
              replies as ProposalComment[],
              this.crewId
            ) as GiftComment[]
            : replies;
          if (comment.replies.some(reply => reply.id === this.highlightId)) {
            comment.repliesExpanded = true;
            return;
          }
          tryNext(index + 1);
        },
        error: () => tryNext(index + 1)
      });
    };
    tryNext(0);
  }

  private loadGift(options?: { silent?: boolean }) {
    if (!options?.silent) {
      this.loading = true;
      this.loadError = '';
    }

    this.giftService.getGiftDetail(this.giftId).subscribe({
      next: async detail => {
        try {
          let entry: GiftDetail = detail;
          if (this.crewId > 0) {
            const decryptedEntries = await this.giftLogCrypto.decryptEntries([detail], this.crewId);
            entry = {
              ...decryptedEntries[0],
              comments: detail.comments
            };
            entry.comments = await this.discussionCrypto.decryptComments(
              detail.comments as ProposalComment[],
              this.crewId
            ) as GiftComment[];
          }
          this.gift = entry;
          if (entry.availableActions?.includes('completeTransfer') && entry.completionPlatformOptions?.length === 1) {
            this.completionPlatformSelection = entry.completionPlatformOptions[0].id;
          }
          this.expandHighlightedReply();
        } catch (error: unknown) {
          if (!options?.silent) {
            this.gift = null;
          }
          this.loadError = error instanceof Error ? error.message : 'Failed to decrypt gift';
          this.toastService.error(this.loadError);
        } finally {
          if (!options?.silent) {
            this.loading = false;
          }
        }
      },
      error: err => {
        if (!options?.silent) {
          this.loading = false;
        }
        this.loadError = err?.message ?? 'Failed to load gift';
        this.toastService.error(this.loadError);
      }
    });
  }
}
