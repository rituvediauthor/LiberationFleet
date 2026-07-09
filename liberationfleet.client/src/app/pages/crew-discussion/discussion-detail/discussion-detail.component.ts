import { Component, ElementRef, HostListener, OnInit, OnDestroy, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CrewDiscussionService } from '../../../services/crew-discussion.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ProposalAttachmentDisplayComponent } from '../../../components/proposal-attachment-display/proposal-attachment-display.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { FallibleFooterComponent } from '../../../components/fallible-footer/fallible-footer.component';
import { AdultContentGateComponent } from '../../../components/adult-content-gate/adult-content-gate.component';
import { DiscussionConfig, DiscussionKind, getDiscussionConfig } from '../../../config/discussion.config';
import {
  DiscussionComment,
  DiscussionDetail,
  PendingAttachment,
  ProposalAttachment
} from '../../../models/crew-discussion.model';
import { ProposalComment, ProposalDetail } from '../../../models/proposal.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../services/encryption-content.service';
import { AuthService } from '../../../services/auth.service';
import { getUserIdFromToken } from '../../../utils/jwt.util';
import { AdultContentService } from '../../../services/adult-content.service';
import { ContentPreferenceService } from '../../../services/content-preference.service';

@Component({
  selector: 'app-discussion-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent,
    FallibleFooterComponent,
    AdultContentGateComponent
  ],
  templateUrl: './discussion-detail.component.html',
  styleUrl: './discussion-detail.component.css'
})
export class DiscussionDetailComponent implements OnInit, OnDestroy {
  @ViewChild('detailScroll') detailScroll?: ElementRef<HTMLElement>;

  config!: DiscussionConfig;
  post: DiscussionDetail | null = null;
  loading = true;
  loadError = '';
  crewId = 0;
  canAttachFiles = false;
  authorDisplayName = '';
  commentText = '';
  commentFocused = false;
  pickingFile = false;
  replyParentId: number | null = null;
  attachmentsExpanded = true;
  posting = false;
  savingEdit = false;
  editing = false;
  editTitle = '';
  editDescription = '';
  keptEditAttachments: ProposalAttachment[] = [];
  newEditAttachments: PendingAttachment[] = [];
  commentAttachments: PendingAttachment[] = [];
  keptCommentEditAttachments: ProposalAttachment[] = [];
  editingCommentId: number | null = null;
  editingCommentParentId: number | null = null;
  openCommentMenuId: number | null = null;
  currentUserId: number | null = null;
  showAdultGate = false;
  contentRevealed = true;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private discussionService = inject(CrewDiscussionService);
  private discussionCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private authService = inject(AuthService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    const kind = this.route.snapshot.data['discussionKind'] as DiscussionKind;
    this.config = getDiscussionConfig(kind);
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;

    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadPost());

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        this.canAttachFiles = membership.canAttachFilesToCrewContent ?? false;
        await this.encryptionContent.whenReady();
        this.contentPreferenceService.ensureLoaded().subscribe({
          next: () => {
            this.loadPost();
            this.encryptionReload?.markInitialLoadDone();
          }
        });
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

  get postId(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  @HostListener('document:click')
  closeMenus() {
    this.openCommentMenuId = null;
  }

  goBack() {
    this.router.navigate([this.config.listRoute]);
  }

  onAdultGateConfirmed() {
    const resourceKey = this.adultContentService.resourceKey('forum', this.postId);
    this.adultContentService.grantConsent(resourceKey);
    this.showAdultGate = false;
    this.contentRevealed = true;
  }

  onAdultGateDeclined() {
    this.showAdultGate = false;
    this.goBack();
  }

  toggleAttachments() {
    this.attachmentsExpanded = !this.attachmentsExpanded;
  }

  onCommentFocus() {
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
      this.commentFocused = true;
      return;
    }
    if (!this.commentText.trim() && this.commentAttachments.length === 0) {
      this.commentFocused = false;
      this.replyParentId = null;
    }
  }

  get commentExpanded(): boolean {
    return this.commentFocused || this.pickingFile || this.commentAttachments.length > 0 || this.editingCommentId != null;
  }

  isOwnComment(comment: DiscussionComment): boolean {
    return this.currentUserId != null && comment.authorUserId === this.currentUserId;
  }

  toggleCommentMenu(commentId: number, event: Event) {
    event.stopPropagation();
    this.openCommentMenuId = this.openCommentMenuId === commentId ? null : commentId;
  }

  startEditComment(comment: DiscussionComment, parentCommentId: number | null = null, event?: Event) {
    event?.stopPropagation();
    this.openCommentMenuId = null;
    this.editingCommentId = comment.id;
    this.editingCommentParentId = parentCommentId;
    this.replyParentId = parentCommentId;
    this.commentText = comment.body ?? '';
    this.keptCommentEditAttachments = (comment.resolvedAttachments ?? []).map(attachment => ({
      resourceId: attachment.resourceId,
      type: attachment.type,
      fileName: attachment.fileName,
      mimeType: attachment.mimeType
    }));
    this.commentAttachments = [];
    this.commentFocused = true;
  }

  cancelEditComment() {
    this.editingCommentId = null;
    this.editingCommentParentId = null;
    this.commentText = '';
    this.commentAttachments = [];
    this.keptCommentEditAttachments = [];
    this.commentFocused = false;
    this.replyParentId = null;
  }

  removeKeptCommentAttachment(index: number) {
    this.keptCommentEditAttachments.splice(index, 1);
  }

  formatCommentAuthor(comment: DiscussionComment, siblingReplies?: DiscussionComment[]): string {
    if (!comment.replyToCommentId) {
      return comment.authorUsername;
    }

    const targetName = comment.replyToUsername
      ?? siblingReplies?.find(reply => reply.id === comment.replyToCommentId)?.authorUsername
      ?? 'User';
    return `${comment.authorUsername} > ${targetName}`;
  }

  startReply(comment: DiscussionComment) {
    this.replyParentId = comment.id;
    this.commentFocused = true;
  }

  startEdit() {
    if (!this.post?.canEdit) {
      return;
    }

    this.editing = true;
    this.editTitle = this.post.title ?? '';
    this.editDescription = this.post.description ?? '';
    this.keptEditAttachments = [...(this.post.attachments ?? [])];
    this.newEditAttachments = [];
  }

  cancelEdit() {
    this.editing = false;
    this.editTitle = '';
    this.editDescription = '';
    this.keptEditAttachments = [];
    this.newEditAttachments = [];
  }

  removeKeptAttachment(index: number) {
    this.keptEditAttachments.splice(index, 1);
  }

  async saveEdit() {
    if (!this.post?.canEdit || this.savingEdit || this.crewId <= 0) {
      return;
    }

    const title = this.editTitle.trim();
    const description = this.editDescription.trim();
    if (!title || !description) {
      this.toastService.error('Title and description are required');
      return;
    }

    this.savingEdit = true;
    try {
      const encrypted = await this.discussionCrypto.encryptProposalPayload(
        this.crewId,
        {
          title,
          description,
          authorDisplayName: this.authorDisplayName
        },
        this.newEditAttachments,
        this.keptEditAttachments
      );

      this.discussionService.updatePost(this.config, this.post.id, encrypted).subscribe({
        next: result => {
          this.savingEdit = false;
          if (result.success) {
            this.toastService.success('Post updated');
            this.cancelEdit();
            this.loadPost();
            return;
          }
          this.toastService.error(result.message || 'Failed to update post');
        },
        error: () => {
          this.savingEdit = false;
          this.toastService.error('Failed to update post');
        }
      });
    } catch {
      this.savingEdit = false;
      this.toastService.error('Failed to encrypt post content');
    }
  }

  async postComment() {
    const hasContent = this.commentText.trim() || this.commentAttachments.length > 0 || this.keptCommentEditAttachments.length > 0;
    if (!this.post || !hasContent || this.posting || this.crewId <= 0) {
      return;
    }

    const body = this.commentText.trim();
    const pendingAttachments = [...this.commentAttachments];
    const parentCommentId = this.replyParentId;
    const editingCommentId = this.editingCommentId;

    this.posting = true;
    try {
      const encrypted = await this.discussionCrypto.encryptCommentPayload(
        this.crewId,
        {
          body,
          authorDisplayName: this.authorDisplayName
        },
        pendingAttachments,
        this.keptCommentEditAttachments
      );

      const request$ = editingCommentId
        ? this.discussionService.updateComment(this.config, this.post.id, editingCommentId, encrypted)
        : this.discussionService.postComment(this.config, this.post.id, {
          parentCommentId,
          nonce: encrypted.nonce,
          ciphertext: encrypted.ciphertext
        });

      request$.subscribe({
        next: result => {
          this.posting = false;
          if (result.success) {
            if (!editingCommentId && result.commentId) {
              this.insertPostedComment(result.commentId, body, pendingAttachments, parentCommentId);
            } else {
              this.refreshPostPreservingScroll();
            }
            this.commentText = '';
            this.commentAttachments = [];
            this.keptCommentEditAttachments = [];
            this.commentFocused = false;
            this.replyParentId = null;
            this.editingCommentId = null;
            this.editingCommentParentId = null;
            this.toastService.success(editingCommentId ? 'Comment updated' : 'Comment posted');
            return;
          }
          this.toastService.error(result.message || (editingCommentId ? 'Failed to update comment' : 'Failed to post comment'));
        },
        error: () => {
          this.posting = false;
          this.toastService.error(editingCommentId ? 'Failed to update comment' : 'Failed to post comment');
        }
      });
    } catch {
      this.posting = false;
      this.toastService.error('Failed to encrypt comment');
    }
  }

  toggleReplies(comment: DiscussionComment) {
    if (comment.repliesExpanded && comment.replies) {
      comment.repliesExpanded = false;
      return;
    }

    if (comment.replies && comment.replies.length > 0) {
      comment.repliesExpanded = true;
      return;
    }

    this.discussionService.getCommentReplies(this.config, this.postId, comment.id).subscribe({
      next: async replies => {
        comment.replies = this.crewId > 0
          ? await this.discussionCrypto.decryptComments(
            replies as ProposalComment[],
            this.crewId
          ) as DiscussionComment[]
          : replies;
        comment.repliesExpanded = true;
      },
      error: () => this.toastService.error('Failed to load replies')
    });
  }

  deletePost() {
    if (!this.post?.canDelete) {
      return;
    }

    this.discussionService.deletePost(this.config, this.post.id).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success('Post deleted');
          this.goBack();
          return;
        }
        this.toastService.error(result.message || 'Failed to delete post');
      },
      error: () => this.toastService.error('Failed to delete post')
    });
  }

  canPostComment(): boolean {
    return Boolean(this.commentText.trim() || this.commentAttachments.length > 0 || this.keptCommentEditAttachments.length > 0);
  }

  retryLoad(): void {
    this.loadPost();
  }

  private insertPostedComment(
    commentId: number,
    body: string,
    pendingAttachments: PendingAttachment[],
    parentCommentId: number | null
  ) {
    if (!this.post) {
      return;
    }

    const token = this.authService.getToken();
    const authorUserId = token ? getUserIdFromToken(token) ?? 0 : 0;
    const { threadRootId, replyToCommentId, replyToUsername } = parentCommentId
      ? this.resolveReplyTargets(parentCommentId)
      : { threadRootId: null as number | null, replyToCommentId: null as number | null, replyToUsername: null as string | null };
    const newComment: DiscussionComment = {
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
      this.post = {
        ...this.post,
        comments: [...this.post.comments, newComment]
      };
      return;
    }

    this.post = {
      ...this.post,
      comments: this.post.comments.map(comment => {
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
    const topLevel = this.post!.comments.find(comment => comment.id === parentCommentId);
    if (topLevel) {
      return { threadRootId: parentCommentId, replyToCommentId: null, replyToUsername: null };
    }

    for (const comment of this.post!.comments) {
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

  private refreshPostPreservingScroll() {
    const scrollEl = this.detailScroll?.nativeElement;
    const scrollTop = scrollEl?.scrollTop ?? 0;
    const expandedCommentIds = new Set(
      this.post?.comments
        .filter(comment => comment.repliesExpanded)
        .map(comment => comment.id) ?? []
    );

    this.loadPost({
      silent: true,
      onLoaded: () => {
        if (!this.post) {
          return;
        }

        this.post.comments = this.post.comments.map(comment => ({
          ...comment,
          repliesExpanded: expandedCommentIds.has(comment.id) ? true : comment.repliesExpanded
        }));

        requestAnimationFrame(() => {
          if (scrollEl) {
            scrollEl.scrollTop = scrollTop;
          }
        });
      }
    });
  }

  private loadPost(options?: { silent?: boolean; onLoaded?: () => void }) {
    if (!options?.silent) {
      this.loading = true;
      this.loadError = '';
    }

    this.discussionService.getPost(this.config, this.postId).subscribe({
      next: async post => {
        try {
          if (this.crewId > 0) {
            this.post = await this.discussionCrypto.decryptDetail(
              post as ProposalDetail,
              this.crewId
            ) as DiscussionDetail;
          } else {
            this.post = post;
          }

          const resourceKey = this.adultContentService.resourceKey('forum', this.postId);
          if (this.adultContentService.needsAgeGate(this.post.isAdultContent, resourceKey)) {
            this.showAdultGate = true;
            this.contentRevealed = false;
          } else {
            this.contentRevealed = true;
          }

          options?.onLoaded?.();
        } catch (error: unknown) {
          if (!options?.silent) {
            this.post = null;
          }
          this.loadError = error instanceof Error
            ? error.message
            : 'Failed to decrypt post';
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
        this.loadError = err?.message ?? 'Failed to load post';
        this.toastService.error(this.loadError);
      }
    });
  }
}
