import { Component, OnInit, OnDestroy, inject } from '@angular/core';
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
import { DiscussionConfig, DiscussionKind, getDiscussionConfig } from '../../../config/discussion.config';
import {
  DiscussionComment,
  DiscussionDetail,
  PendingAttachment,
  ProposalAttachment
} from '../../../models/crew-discussion.model';
import { ProposalComment, ProposalDetail } from '../../../models/proposal.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../services/encryption-content.service';

@Component({
  selector: 'app-discussion-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent
  ],
  templateUrl: './discussion-detail.component.html',
  styleUrl: './discussion-detail.component.css'
})
export class DiscussionDetailComponent implements OnInit, OnDestroy {
  config!: DiscussionConfig;
  post: DiscussionDetail | null = null;
  loading = true;
  crewId = 0;
  authorDisplayName = '';
  commentText = '';
  commentFocused = false;
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

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private discussionService = inject(CrewDiscussionService);
  private discussionCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    const kind = this.route.snapshot.data['discussionKind'] as DiscussionKind;
    this.config = getDiscussionConfig(kind);

    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadPost());

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        this.loadPost();
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

  get postId(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  goBack() {
    this.router.navigate([this.config.listRoute]);
  }

  toggleAttachments() {
    this.attachmentsExpanded = !this.attachmentsExpanded;
  }

  onCommentFocus() {
    this.commentFocused = true;
  }

  onCommentBlur() {
    setTimeout(() => {
      if (!this.commentText.trim() && this.commentAttachments.length === 0) {
        this.commentFocused = false;
        this.replyParentId = null;
      }
    }, 150);
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
    const hasContent = this.commentText.trim() || this.commentAttachments.length > 0;
    if (!this.post || !hasContent || this.posting || this.crewId <= 0) {
      return;
    }

    this.posting = true;
    try {
      const encrypted = await this.discussionCrypto.encryptCommentPayload(
        this.crewId,
        {
          body: this.commentText.trim(),
          authorDisplayName: this.authorDisplayName
        },
        this.commentAttachments
      );

      this.discussionService.postComment(this.config, this.post.id, {
        parentCommentId: this.replyParentId,
        nonce: encrypted.nonce,
        ciphertext: encrypted.ciphertext
      }).subscribe({
        next: result => {
          this.posting = false;
          if (result.success) {
            this.commentText = '';
            this.commentAttachments = [];
            this.commentFocused = false;
            this.replyParentId = null;
            this.toastService.success('Comment posted');
            this.loadPost();
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
    return Boolean(this.commentText.trim() || this.commentAttachments.length > 0);
  }

  private loadPost() {
    this.loading = true;
    this.discussionService.getPost(this.config, this.postId).subscribe({
      next: async post => {
        if (this.crewId > 0) {
          this.post = await this.discussionCrypto.decryptDetail(
            post as ProposalDetail,
            this.crewId
          ) as DiscussionDetail;
        } else {
          this.post = post;
        }
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toastService.error(err?.message ?? 'Failed to load post');
      }
    });
  }
}
