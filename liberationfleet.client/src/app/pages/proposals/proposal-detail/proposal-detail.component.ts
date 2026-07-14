import { Component, OnInit, OnDestroy, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ProposalService } from '../../../services/proposal.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ProposalAttachmentDisplayComponent } from '../../../components/proposal-attachment-display/proposal-attachment-display.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { LibraryImageCarouselComponent } from '../../../components/library-image-carousel/library-image-carousel.component';
import { FallibleFooterComponent } from '../../../components/fallible-footer/fallible-footer.component';
import { KickReasonDialogComponent } from '../../../components/kick-reason-dialog/kick-reason-dialog.component';
import {
  PendingAttachment,
  ProposalAttachment,
  ProposalComment,
  ProposalDetail,
  ProposalVoteChoice,
  ResolvedAttachment
} from '../../../models/proposal.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../services/encryption-content.service';
import { AuthService } from '../../../services/auth.service';
import { getUserIdFromToken } from '../../../utils/jwt.util';
import { NavigationService } from '../../../services/navigation.service';
import { NotificationContentService } from '../../../services/notification-content.service';
import { MentionAutocompleteDirective } from '../../../directives/mention-autocomplete.directive';
import { MentionTextComponent } from '../../../components/mention-text/mention-text.component';

@Component({
  selector: 'app-proposal-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent,
    LibraryImageCarouselComponent,
    KickReasonDialogComponent,
    FallibleFooterComponent,
    MentionAutocompleteDirective,
    MentionTextComponent
  ],
  templateUrl: './proposal-detail.component.html',
  styleUrl: './proposal-detail.component.css'
})
export class ProposalDetailComponent implements OnInit, OnDestroy {
  proposal: ProposalDetail | null = null;
  loading = true;
  crewId = 0;
  canAttachFiles = false;
  authorDisplayName = '';
  commentText = '';
  mentionedUserIds: number[] = [];
  commentFocused = false;
  pickingFile = false;
  replyParentId: number | null = null;
  showVoteDialog = false;
  showKickReasonDialog = false;
  pendingKickCommentId: number | null = null;
  selectedVote: ProposalVoteChoice | '' = '';
  attachmentsExpanded = true;
  posting = false;
  savingEdit = false;
  editing = false;
  editTitle = '';
  editDescription = '';
  editMentionedUserIds: number[] = [];
  keptEditAttachments: ProposalAttachment[] = [];
  newEditAttachments: PendingAttachment[] = [];
  commentAttachments: PendingAttachment[] = [];
  keptCommentEditAttachments: ProposalAttachment[] = [];
  editingCommentId: number | null = null;
  editingCommentParentId: number | null = null;
  openCommentMenuId: number | null = null;
  openProposalAuthorMenu = false;
  countdownTick = 0;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private notificationContent = inject(NotificationContentService);
  private proposalService = inject(ProposalService);
  private proposalCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private authService = inject(AuthService);
  private countdownIntervalId?: ReturnType<typeof setInterval>;
  private encryptionReload?: EncryptionReloadHandle;
  private isFleetScope = false;

  ngOnInit() {
    this.isFleetScope = this.route.snapshot.data['scope'] === 'fleet'
      || this.router.url.startsWith('/app/fleet/proposals');
    const proposalId = this.proposalId;
    if (proposalId) {
      const prefix = this.isFleetScope ? '/app/fleet/proposals' : '/app/crew/proposals';
      this.notificationContent.markVisited(`${prefix}/${proposalId}`, proposalId);
    }
    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadProposal());

    this.countdownIntervalId = setInterval(() => {
      this.countdownTick++;
    }, 1000);

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        this.canAttachFiles = this.isFleetScope
          ? (membership.canAttachFilesToFleetContent ?? false)
          : (membership.canAttachFilesToCrewContent ?? false);
        await this.encryptionContent.whenReady();
        this.loadProposal();
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
    if (this.countdownIntervalId) {
      clearInterval(this.countdownIntervalId);
    }
    this.encryptionReload?.subscription.unsubscribe();
  }

  @HostListener('document:click')
  closeMenus() {
    this.openCommentMenuId = null;
    this.openProposalAuthorMenu = false;
  }

  toggleCommentMenu(commentId: number, event: Event) {
    event.stopPropagation();
    this.openProposalAuthorMenu = false;
    this.openCommentMenuId = this.openCommentMenuId === commentId ? null : commentId;
  }

  toggleProposalAuthorMenu(event: Event) {
    event.stopPropagation();
    this.openCommentMenuId = null;
    this.openProposalAuthorMenu = !this.openProposalAuthorMenu;
  }

  rerollNickname(event: Event) {
    event.stopPropagation();
    this.openCommentMenuId = null;
    if (!this.proposal) {
      return;
    }

    this.proposalService.rerollAlias(this.proposal.id).subscribe({
      next: result => {
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to reroll nickname');
          return;
        }
        this.toastService.success(`Your nickname is now ${result.alias ?? 'updated'}`);
        if (this.proposal && result.alias) {
          this.proposal = {
            ...this.proposal,
            viewerAlias: result.alias,
            comments: this.proposal.comments.map(comment => this.applyAliasToOwnComment(comment, result.alias!))
          };
        }
      },
      error: () => this.toastService.error('Failed to reroll nickname')
    });
  }

  kickFromComment(comment: ProposalComment, event: Event) {
    event.stopPropagation();
    this.openCommentMenuId = null;
    if (!this.proposal) {
      return;
    }

    this.pendingKickCommentId = comment.id;
    this.showKickReasonDialog = true;
  }

  kickFromProposalAuthor(event: Event) {
    event.stopPropagation();
    this.openProposalAuthorMenu = false;
    if (!this.proposal) {
      return;
    }

    this.pendingKickCommentId = null;
    this.showKickReasonDialog = true;
  }

  onConfirmKickProposal(reason: string) {
    this.showKickReasonDialog = false;
    if (!this.proposal) {
      return;
    }

    const request = this.pendingKickCommentId != null
      ? this.proposalService.kickFromComment(this.proposal.id, this.pendingKickCommentId, reason)
      : this.proposalService.kickFromProposalAuthor(this.proposal.id, reason);

    this.pendingKickCommentId = null;

    request.subscribe({
      next: result => {
        if (!result.success) {
          this.toastService.error(result.message || 'Failed to submit kick proposal');
          if (result.proposalId) {
            this.router.navigate(['/app/crew/proposals', result.proposalId]);
          }
          return;
        }
        this.toastService.success(result.message || 'Kick proposal submitted');
        if (result.proposalId) {
          this.router.navigate(['/app/crew/proposals', result.proposalId]);
        }
      },
      error: () => this.toastService.error('Failed to submit kick proposal')
    });
  }

  onCancelKickProposal() {
    this.showKickReasonDialog = false;
    this.pendingKickCommentId = null;
  }

  get proposalId(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  goBack() {
    const fallback = this.proposal
      ? [this.isFleetScope ? '/app/fleet/proposals/list' : '/app/crew/proposals/list', this.proposal.status.toLowerCase()]
      : [this.isFleetScope ? '/app/fleet/proposals' : '/app/crew/proposals'];
    this.navigation.back(fallback);
  }

  toggleAttachments() {
    this.attachmentsExpanded = !this.attachmentsExpanded;
  }

  showKickVoteRestriction(): boolean {
    return this.proposal?.status === 'Pending' && !!this.proposal?.isKickVoteTarget;
  }

  openVoteDialog() {
    if (!this.proposal?.canVote) {
      return;
    }
    this.showVoteDialog = true;
    this.selectedVote = '';
  }

  closeVoteDialog() {
    this.showVoteDialog = false;
    this.selectedVote = '';
  }

  submitVote() {
    if (!this.proposal || !this.selectedVote) {
      return;
    }

    this.proposalService.vote(this.proposal.id, this.selectedVote as ProposalVoteChoice).subscribe({
      next: result => {
        this.closeVoteDialog();
        if (result.success) {
          this.toastService.success(result.message || 'Vote recorded');
          this.loadProposal();
          return;
        }
        this.toastService.error(result.message || 'Failed to vote');
      },
      error: () => this.toastService.error('Failed to vote')
    });
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

  startEditComment(comment: ProposalComment, parentCommentId: number | null = null, event?: Event) {
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
    this.mentionedUserIds = [];
    this.commentFocused = true;
  }

  cancelEditComment() {
    this.editingCommentId = null;
    this.editingCommentParentId = null;
    this.commentText = '';
    this.mentionedUserIds = [];
    this.commentAttachments = [];
    this.keptCommentEditAttachments = [];
    this.commentFocused = false;
    this.replyParentId = null;
  }

  removeKeptCommentAttachment(index: number) {
    this.keptCommentEditAttachments.splice(index, 1);
  }

  formatCommentAuthor(comment: ProposalComment, siblingReplies?: ProposalComment[]): string {
    if (!comment.replyToCommentId) {
      return comment.authorUsername;
    }

    const targetName = comment.replyToUsername
      ?? siblingReplies?.find(reply => reply.id === comment.replyToCommentId)?.authorUsername
      ?? 'User';
    return `${comment.authorUsername} > ${targetName}`;
  }

  startReply(comment: ProposalComment) {
    this.replyParentId = comment.id;
    this.commentFocused = true;
  }

  startEdit() {
    if (!this.proposal?.canEdit) {
      return;
    }

    this.editing = true;
    this.editTitle = this.proposal.title ?? '';
    this.editDescription = this.proposal.description ?? '';
    this.editMentionedUserIds = [];
    this.keptEditAttachments = [...(this.proposal.attachments ?? [])];
    this.newEditAttachments = [];
  }

  cancelEdit() {
    this.editing = false;
    this.editTitle = '';
    this.editDescription = '';
    this.editMentionedUserIds = [];
    this.keptEditAttachments = [];
    this.newEditAttachments = [];
  }

  removeKeptAttachment(index: number) {
    this.keptEditAttachments.splice(index, 1);
  }

  async saveEdit() {
    if (!this.proposal?.canEdit || this.savingEdit || this.crewId <= 0) {
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
      const encrypted = await this.proposalCrypto.encryptProposalPayload(
        this.crewId,
        {
          title,
          description,
          authorDisplayName: this.authorDisplayName
        },
        this.newEditAttachments,
        this.keptEditAttachments
      );

      this.proposalService.updateProposal(this.proposal.id, {
        ...encrypted,
        mentionedUserIds: this.editMentionedUserIds
      }).subscribe({
        next: result => {
          this.savingEdit = false;
          if (result.success) {
            this.toastService.success('Proposal updated');
            this.cancelEdit();
            this.loadProposal();
            return;
          }
          this.toastService.error(result.message || 'Failed to update proposal');
        },
        error: () => {
          this.savingEdit = false;
          this.toastService.error('Failed to update proposal');
        }
      });
    } catch {
      this.savingEdit = false;
      this.toastService.error('Failed to encrypt proposal content');
    }
  }

  async postComment() {
    const hasContent = this.commentText.trim() || this.commentAttachments.length > 0 || this.keptCommentEditAttachments.length > 0;
    if (!this.proposal || !hasContent || this.posting || this.crewId <= 0) {
      return;
    }

    this.posting = true;
    const body = this.commentText.trim();
    const pendingAttachments = [...this.commentAttachments];
    const parentCommentId = this.replyParentId;
    const editingCommentId = this.editingCommentId;

    try {
      const request$ = editingCommentId
        ? this.isFleetScope
          ? this.proposalService.updateComment(this.proposal.id, editingCommentId, {
            body,
            mentionedUserIds: this.mentionedUserIds
          })
          : this.proposalService.updateComment(this.proposal.id, editingCommentId, {
            ...(await this.proposalCrypto.encryptCommentPayload(
              this.crewId,
              {
                body,
                authorDisplayName: this.proposal.usesAnonymousComments
                  ? (this.proposal.viewerAlias ?? 'Anonymous')
                  : this.authorDisplayName
              },
              pendingAttachments,
              this.keptCommentEditAttachments
            )),
            mentionedUserIds: this.mentionedUserIds
          })
        : this.isFleetScope
          ? this.proposalService.postComment(this.proposal.id, {
            parentCommentId,
            body,
            mentionedUserIds: this.mentionedUserIds
          })
          : this.proposalService.postComment(this.proposal.id, {
            parentCommentId,
            ...(await this.proposalCrypto.encryptCommentPayload(
              this.crewId,
              {
                body,
                authorDisplayName: this.proposal.usesAnonymousComments
                  ? (this.proposal.viewerAlias ?? 'Anonymous')
                  : this.authorDisplayName
              },
              pendingAttachments,
              this.keptCommentEditAttachments
            )),
            mentionedUserIds: this.mentionedUserIds
          });

      request$.subscribe({
        next: result => {
          this.posting = false;
          if (result.success) {
            if (result.alias && this.proposal) {
              this.proposal = { ...this.proposal, viewerAlias: result.alias };
            }
            this.commentText = '';
            this.mentionedUserIds = [];
            this.commentAttachments = [];
            this.keptCommentEditAttachments = [];
            this.commentFocused = false;
            this.replyParentId = null;
            this.editingCommentId = null;
            this.editingCommentParentId = null;
            this.toastService.success(editingCommentId ? 'Comment updated' : 'Comment posted');
            if (!editingCommentId && result.commentId) {
              this.insertPostedComment(result.commentId, body, pendingAttachments, parentCommentId);
            } else {
              this.loadProposal();
            }
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
      this.toastService.error(this.isFleetScope ? 'Failed to post comment' : 'Failed to encrypt comment');
    }
  }

  toggleReplies(comment: ProposalComment) {
    if (comment.repliesExpanded && comment.replies) {
      comment.repliesExpanded = false;
      return;
    }

    if (comment.replies && comment.replies.length > 0) {
      comment.repliesExpanded = true;
      return;
    }

    this.proposalService.getCommentReplies(this.proposalId, comment.id).subscribe({
      next: async replies => {
        comment.replies = this.isFleetScope || this.crewId <= 0
          ? replies
          : await this.proposalCrypto.decryptComments(replies, this.crewId);
        comment.repliesExpanded = true;
      },
      error: () => this.toastService.error('Failed to load replies')
    });
  }

  deleteProposal() {
    if (!this.proposal?.canDelete) {
      return;
    }

    this.proposalService.deleteProposal(this.proposal.id).subscribe({
      next: result => {
        if (result.success) {
          this.toastService.success('Proposal deleted');
          this.goBack();
          return;
        }
        this.toastService.error(result.message || 'Failed to delete proposal');
      },
      error: () => this.toastService.error('Failed to delete proposal')
    });
  }

  countdownText(): string | null {
    void this.countdownTick;
    if (!this.proposal || this.proposal.status !== 'Pending') {
      return null;
    }
    return this.proposalService.formatCountdown(this.proposal.approvalTimerEndsAt ?? null);
  }

  get carouselImages(): string[] {
    return (this.proposal?.resolvedAttachments ?? [])
      .filter(attachment => attachment.type === 'image' && attachment.dataUrl)
      .map(attachment => attachment.dataUrl!);
  }

  get nonImageAttachments(): ResolvedAttachment[] {
    return (this.proposal?.resolvedAttachments ?? []).filter(attachment => attachment.type !== 'image');
  }

  canPostComment(): boolean {
    return Boolean(this.commentText.trim() || this.commentAttachments.length > 0 || this.keptCommentEditAttachments.length > 0);
  }

  private insertPostedComment(
    commentId: number,
    body: string,
    pendingAttachments: PendingAttachment[],
    parentCommentId: number | null
  ) {
    if (!this.proposal) {
      return;
    }

    const token = this.authService.getToken();
    const authorUserId = token ? getUserIdFromToken(token) ?? 0 : 0;
    const displayName = this.proposal.usesAnonymousComments
      ? (this.proposal.viewerAlias ?? 'Anonymous')
      : this.authorDisplayName;
    const { threadRootId, replyToCommentId, replyToUsername } = parentCommentId
      ? this.resolveReplyTargets(parentCommentId)
      : { threadRootId: null as number | null, replyToCommentId: null as number | null, replyToUsername: null as string | null };
    const newComment: ProposalComment = {
      id: commentId,
      authorUserId,
      authorUsername: displayName,
      parentCommentId: threadRootId,
      replyToCommentId,
      replyToUsername,
      createdAt: new Date(),
      replyCount: 0,
      hasEncryptedContent: !this.isFleetScope,
      isOwnComment: true,
      canKick: false,
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
      this.proposal = {
        ...this.proposal,
        comments: [newComment, ...this.proposal.comments]
      };
      return;
    }

    this.proposal = {
      ...this.proposal,
      comments: this.proposal.comments.map(comment => {
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
    const topLevel = this.proposal!.comments.find(comment => comment.id === parentCommentId);
    if (topLevel) {
      return { threadRootId: parentCommentId, replyToCommentId: null, replyToUsername: null };
    }

    for (const comment of this.proposal!.comments) {
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

  private applyAliasToOwnComment(comment: ProposalComment, alias: string): ProposalComment {
    const updated: ProposalComment = {
      ...comment,
      authorUsername: comment.isOwnComment ? alias : comment.authorUsername,
      replies: comment.replies?.map(reply => this.applyAliasToOwnComment(reply, alias))
    };
    return updated;
  }

  private loadProposal() {
    this.loading = true;
    this.proposalService.getProposal(this.proposalId).subscribe({
      next: async proposal => {
        if (this.isFleetScope || this.crewId <= 0) {
          this.proposal = proposal;
        } else {
          this.proposal = await this.proposalCrypto.decryptDetail(proposal, this.crewId);
        }
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.toastService.error(err?.message ?? 'Failed to load proposal');
      }
    });
  }
}

