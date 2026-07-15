import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { FleetService } from '../../../../services/fleet.service';
import { ProposalCryptoService } from '../../../../services/crypto/proposal-crypto.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { FallibleFooterComponent } from '../../../../components/fallible-footer/fallible-footer.component';
import { AdultContentGateComponent } from '../../../../components/adult-content-gate/adult-content-gate.component';
import { FleetForumComment, FleetForumPost } from '../../../../models/fleet-forum.model';
import { ProposalComment, ProposalDetail } from '../../../../models/proposal.model';
import { AuthService } from '../../../../services/auth.service';
import { getUserIdFromToken } from '../../../../utils/jwt.util';
import { AdultContentService } from '../../../../services/adult-content.service';
import { NavigationService } from '../../../../services/navigation.service';
import { ContentPreferenceService } from '../../../../services/content-preference.service';
import { ProfileService } from '../../../../services/profile.service';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../../services/encryption-content.service';

import { MentionAutocompleteDirective } from '../../../../directives/mention-autocomplete.directive';
import { ReportContentDialogComponent } from '../../../../components/report-content-dialog/report-content-dialog.component';
import { ContentReportTargetType } from '../../../../models/content-report.model';

@Component({
  selector: 'app-fleet-forum-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    FallibleFooterComponent,
    AdultContentGateComponent,
    MentionAutocompleteDirective,
    ReportContentDialogComponent
  ],
  templateUrl: './fleet-forum-detail.component.html',
  styleUrl: './fleet-forum-detail.component.css'
})
export class FleetForumDetailComponent implements OnInit, OnDestroy {
  @ViewChild('detailScroll') detailScroll?: ElementRef<HTMLElement>;

  post: FleetForumPost | null = null;
  loading = true;
  loadError = '';
  fleetId = 0;
  authorDisplayName = '';
  commentText = '';
  mentionedUserIds: number[] = [];
  commentFocused = false;
  replyParentId: number | null = null;
  posting = false;
  savingEdit = false;
  editing = false;
  editTitle = '';
  editBody = '';
  editingCommentId: number | null = null;
  editingCommentParentId: number | null = null;
  openCommentMenuId: number | null = null;
  currentUserId: number | null = null;
  showAdultGate = false;
  contentRevealed = true;
  showReportDialog = false;
  reportTargetType: ContentReportTargetType = 'ForumPost';
  reportTargetResourceId: number | null = null;
  reportTargetParentId: number | null = null;
  reportTargetAuthorUserId: number | null = null;
  reportEvidenceTitle = '';
  reportEvidenceText = '';
  reportEvidenceAuthorUsername = '';
  reportMediaIds: string[] = [];

  private route = inject(ActivatedRoute);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private forumCrypto = inject(ProposalCryptoService);
  private toastService = inject(ToastService);
  private authService = inject(AuthService);
  private adultContentService = inject(AdultContentService);
  private contentPreferenceService = inject(ContentPreferenceService);
  private profileService = inject(ProfileService);
  private encryptionContent = inject(EncryptionContentService);
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    const token = this.authService.getToken();
    this.currentUserId = token ? getUserIdFromToken(token) : null;

    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadPost());

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    this.fleetService.getStatus().subscribe({
      next: async status => {
        this.fleetId = status.fleetId ?? 0;
        await this.encryptionContent.whenReady();
        this.contentPreferenceService.ensureLoaded().subscribe({
          next: () => this.loadPost(),
          error: () => this.loadPost()
        });
        this.encryptionReload?.markInitialLoadDone();
      },
      error: () => {
        this.loadError = 'Failed to load fleet status';
        this.loading = false;
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
    this.navigation.back(['/app/fleet/forums']);
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

  onCommentFocus() {
    this.commentFocused = true;
  }

  onCommentBlur() {
    setTimeout(() => {
      if (!this.commentText.trim()) {
        this.commentFocused = false;
        this.replyParentId = null;
      }
    }, 150);
  }

  get commentExpanded(): boolean {
    return this.commentFocused || this.editingCommentId != null;
  }

  isOwnComment(comment: FleetForumComment): boolean {
    return this.currentUserId != null && comment.authorUserId === this.currentUserId;
  }

  get isOwnPost(): boolean {
    return this.post != null && this.currentUserId != null && this.post.authorUserId === this.currentUserId;
  }

  openReportPost() {
    if (!this.post) {
      return;
    }
    this.reportTargetType = 'ForumPost';
    this.reportTargetResourceId = this.post.id;
    this.reportTargetParentId = null;
    this.reportTargetAuthorUserId = this.post.authorUserId;
    this.reportEvidenceTitle = this.post.title ?? '';
    this.reportEvidenceText = this.post.description ?? this.post.body ?? '';
    this.reportEvidenceAuthorUsername = this.post.authorUsername ?? '';
    this.reportMediaIds = [];
    this.showReportDialog = true;
  }

  openReportComment(comment: FleetForumComment, event?: Event) {
    event?.stopPropagation();
    this.openCommentMenuId = null;
    this.reportTargetType = 'ForumComment';
    this.reportTargetResourceId = comment.id;
    this.reportTargetParentId = this.post?.id ?? null;
    this.reportTargetAuthorUserId = comment.authorUserId;
    this.reportEvidenceTitle = '';
    this.reportEvidenceText = comment.body ?? '';
    this.reportEvidenceAuthorUsername = comment.authorUsername ?? '';
    this.reportMediaIds = [];
    this.showReportDialog = true;
  }

  onReportDismissed() {
    this.showReportDialog = false;
  }

  onReportSubmitted() {
    this.showReportDialog = false;
  }

  toggleCommentMenu(commentId: number, event: Event) {
    event.stopPropagation();
    this.openCommentMenuId = this.openCommentMenuId === commentId ? null : commentId;
  }

  startEditComment(comment: FleetForumComment, parentCommentId: number | null = null, event?: Event) {
    event?.stopPropagation();
    this.openCommentMenuId = null;
    this.editingCommentId = comment.id;
    this.editingCommentParentId = parentCommentId;
    this.replyParentId = parentCommentId;
    this.commentText = comment.body ?? '';
    this.mentionedUserIds = [];
    this.commentFocused = true;
  }

  cancelEditComment() {
    this.editingCommentId = null;
    this.editingCommentParentId = null;
    this.commentText = '';
    this.mentionedUserIds = [];
    this.commentFocused = false;
    this.replyParentId = null;
  }

  formatCommentAuthor(comment: FleetForumComment, siblingReplies?: FleetForumComment[]): string {
    if (!comment.replyToCommentId) {
      return comment.authorUsername;
    }

    const targetName = comment.replyToUsername
      ?? siblingReplies?.find(reply => reply.id === comment.replyToCommentId)?.authorUsername
      ?? 'User';
    return `${comment.authorUsername} > ${targetName}`;
  }

  startReply(comment: FleetForumComment) {
    this.replyParentId = comment.id;
    this.commentFocused = true;
  }

  startEdit() {
    if (!this.post?.canEdit) {
      return;
    }

    this.editing = true;
    this.editTitle = this.post.title ?? '';
    this.editBody = this.post.description ?? this.post.body ?? '';
  }

  cancelEdit() {
    this.editing = false;
    this.editTitle = '';
    this.editBody = '';
  }

  async saveEdit() {
    if (!this.post?.canEdit || this.savingEdit || this.fleetId <= 0) {
      return;
    }

    const title = this.editTitle.trim();
    const body = this.editBody.trim();
    if (!title || !body) {
      this.toastService.error('Title and body are required');
      return;
    }

    this.savingEdit = true;
    try {
      const encrypted = await this.forumCrypto.encryptProposalPayload(
        { fleetId: this.fleetId },
        {
          title,
          description: body,
          authorDisplayName: this.authorDisplayName
        }
      );

      this.fleetService.updateForum(this.post.id, {
        ...encrypted,
        mentionedUserIds: []
      }).subscribe({
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
    if (!this.post || !this.commentText.trim() || this.posting || this.fleetId <= 0) {
      return;
    }

    const body = this.commentText.trim();
    const parentCommentId = this.replyParentId;
    const editingCommentId = this.editingCommentId;

    this.posting = true;
    try {
      const encrypted = await this.forumCrypto.encryptCommentPayload(
        { fleetId: this.fleetId },
        {
          body,
          authorDisplayName: this.authorDisplayName
        }
      );

      const request$ = editingCommentId
        ? this.fleetService.updateForumComment(this.post.id, editingCommentId, {
          ...encrypted,
          mentionedUserIds: this.mentionedUserIds
        })
        : this.fleetService.createForumComment(this.post.id, {
          parentCommentId: parentCommentId ?? undefined,
          ...encrypted,
          mentionedUserIds: this.mentionedUserIds
        });

      request$.subscribe({
        next: result => {
          this.posting = false;
          if (result.success) {
            if (!editingCommentId && result.commentId) {
              this.insertPostedComment(result.commentId, body, parentCommentId);
            } else {
              this.refreshPostPreservingScroll();
            }
            this.commentText = '';
            this.mentionedUserIds = [];
            this.commentFocused = false;
            this.replyParentId = null;
            this.editingCommentId = null;
            this.editingCommentParentId = null;
            this.toastService.success(editingCommentId ? 'Comment updated' : 'Comment posted');
            return;
          }
          this.toastService.error(
            result.message || (editingCommentId ? 'Failed to update comment' : 'Failed to post comment')
          );
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

  toggleReplies(comment: FleetForumComment) {
    if (comment.repliesExpanded && comment.replies) {
      comment.repliesExpanded = false;
      return;
    }

    if (comment.replies && comment.replies.length > 0) {
      comment.repliesExpanded = true;
      return;
    }

    this.fleetService.getForumCommentReplies(this.postId, comment.id).subscribe({
      next: async response => {
        if (!response.success) {
          this.toastService.error(response.message || 'Failed to load replies');
          return;
        }

        const replies = response.items ?? [];
        comment.replies = this.fleetId > 0
          ? await this.forumCrypto.decryptComments(
            replies as unknown as ProposalComment[],
            { fleetId: this.fleetId }
          ) as unknown as FleetForumComment[]
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

    this.fleetService.deleteForum(this.post.id).subscribe({
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
    return Boolean(this.commentText.trim());
  }

  retryLoad(): void {
    this.loadPost();
  }

  private insertPostedComment(
    commentId: number,
    body: string,
    parentCommentId: number | null
  ) {
    if (!this.post) {
      return;
    }

    const token = this.authService.getToken();
    const authorUserId = token ? getUserIdFromToken(token) ?? 0 : 0;
    const { threadRootId, replyToCommentId, replyToUsername } = parentCommentId
      ? this.resolveReplyTargets(parentCommentId)
      : {
          threadRootId: null as number | null,
          replyToCommentId: null as number | null,
          replyToUsername: null as string | null
        };

    const newComment: FleetForumComment = {
      id: commentId,
      authorUserId,
      authorUsername: this.authorDisplayName,
      parentCommentId: threadRootId,
      replyToCommentId,
      replyToUsername,
      createdAt: new Date().toISOString(),
      replyCount: 0,
      body,
      hasEncryptedContent: true
    };

    if (!parentCommentId) {
      this.post = {
        ...this.post,
        comments: [newComment, ...this.post.comments]
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

    this.fleetService.getForum(this.postId).subscribe({
      next: async response => {
        try {
          if (!response.success || !response.post) {
            if (!options?.silent) {
              this.post = null;
              this.loading = false;
            }
            this.loadError = response.message || 'Failed to load post';
            this.toastService.error(this.loadError);
            return;
          }

          const post = response.post;
          if (this.fleetId > 0) {
            this.post = await this.forumCrypto.decryptDetail(
              post as unknown as ProposalDetail,
              { fleetId: this.fleetId }
            ) as unknown as FleetForumPost;
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
        this.loadError = err?.error?.message ?? err?.message ?? 'Failed to load post';
        this.toastService.error(this.loadError);
      }
    });
  }
}
