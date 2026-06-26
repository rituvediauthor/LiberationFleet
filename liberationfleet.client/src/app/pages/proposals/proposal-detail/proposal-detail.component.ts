import { Component, OnInit, OnDestroy, inject } from '@angular/core';
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
import {
  PendingAttachment,
  ProposalAttachment,
  ProposalComment,
  ProposalDetail,
  ProposalVoteChoice
} from '../../../models/proposal.model';
import { EncryptionContentService, EncryptionReloadHandle } from '../../../services/encryption-content.service';

@Component({
  selector: 'app-proposal-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProposalAttachmentDisplayComponent,
    ProposalAttachmentPickerComponent
  ],
  templateUrl: './proposal-detail.component.html',
  styleUrl: './proposal-detail.component.css'
})
export class ProposalDetailComponent implements OnInit, OnDestroy {
  proposal: ProposalDetail | null = null;
  loading = true;
  crewId = 0;
  authorDisplayName = '';
  commentText = '';
  commentFocused = false;
  pickingFile = false;
  replyParentId: number | null = null;
  showVoteDialog = false;
  selectedVote: ProposalVoteChoice | '' = '';
  attachmentsExpanded = true;
  posting = false;
  savingEdit = false;
  editing = false;
  editTitle = '';
  editDescription = '';
  keptEditAttachments: ProposalAttachment[] = [];
  newEditAttachments: PendingAttachment[] = [];
  commentAttachments: PendingAttachment[] = [];
  countdownTick = 0;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private proposalService = inject(ProposalService);
  private proposalCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private countdownIntervalId?: ReturnType<typeof setInterval>;
  private encryptionReload?: EncryptionReloadHandle;

  ngOnInit() {
    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadProposal());

    this.countdownIntervalId = setInterval(() => {
      this.countdownTick++;
    }, 1000);

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
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

  get proposalId(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  goBack() {
    if (this.proposal) {
      this.router.navigate(['/app/crew/proposals/list', this.proposal.status.toLowerCase()]);
      return;
    }
    this.router.navigate(['/app/crew/proposals']);
  }

  toggleAttachments() {
    this.attachmentsExpanded = !this.attachmentsExpanded;
  }

  openVoteDialog() {
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
    return this.commentFocused || this.pickingFile || this.commentAttachments.length > 0;
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
    this.keptEditAttachments = [...(this.proposal.attachments ?? [])];
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

      this.proposalService.updateProposal(this.proposal.id, encrypted).subscribe({
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
    const hasContent = this.commentText.trim() || this.commentAttachments.length > 0;
    if (!this.proposal || !hasContent || this.posting || this.crewId <= 0) {
      return;
    }

    this.posting = true;
    try {
      const encrypted = await this.proposalCrypto.encryptCommentPayload(
        this.crewId,
        {
          body: this.commentText.trim(),
          authorDisplayName: this.authorDisplayName
        },
        this.commentAttachments
      );

      this.proposalService.postComment(this.proposal.id, {
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
            this.loadProposal();
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
        comment.replies = this.crewId > 0
          ? await this.proposalCrypto.decryptComments(replies, this.crewId)
          : replies;
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

  canPostComment(): boolean {
    return Boolean(this.commentText.trim() || this.commentAttachments.length > 0);
  }

  private loadProposal() {
    this.loading = true;
    this.proposalService.getProposal(this.proposalId).subscribe({
      next: async proposal => {
        if (this.crewId > 0) {
          this.proposal = await this.proposalCrypto.decryptDetail(proposal, this.crewId);
        } else {
          this.proposal = proposal;
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

