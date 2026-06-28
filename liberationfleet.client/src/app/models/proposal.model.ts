import { EncryptedPayload } from './crypto.model';

export type ProposalStatus = 'Pending' | 'Approved' | 'Rejected';
export type ProposalVoteChoice = 'approve' | 'disapprove';

export interface ProposalAttachment {
  resourceId: string;
  type: 'image' | 'video' | 'audio';
  fileName?: string;
  mimeType?: string;
}

export interface ResolvedAttachment extends ProposalAttachment {
  dataUrl?: string;
}

export interface ProposalEncryptedPayload {
  title: string;
  description: string;
  authorDisplayName?: string;
  attachments?: ProposalAttachment[];
  thumbnailResourceId?: string | null;
}

export interface ProposalCommentEncryptedPayload {
  body: string;
  authorDisplayName?: string;
  attachments?: ProposalAttachment[];
}

export interface ProposalListItem {
  id: number;
  authorUserId: number;
  authorUsername: string;
  lastActivityAt: Date;
  status: ProposalStatus;
  approveCount: number;
  disapproveCount: number;
  approvalTimerEndsAt?: Date | null;
  hasEncryptedContent?: boolean;
  hasPlaintextContent?: boolean;
  encryptedPayload?: EncryptedPayload | null;
  currentUserVote?: ProposalVoteChoice | null;
  title?: string;
  descriptionPreview?: string;
  thumbnailUrl?: string | null;
}

export interface ProposalComment {
  id: number;
  authorUserId: number;
  authorUsername: string;
  parentCommentId?: number | null;
  replyToCommentId?: number | null;
  replyToUsername?: string | null;
  createdAt: Date;
  replyCount: number;
  hasEncryptedContent?: boolean;
  encryptedPayload?: EncryptedPayload | null;
  body?: string;
  attachments?: ProposalAttachment[];
  resolvedAttachments?: ResolvedAttachment[];
  replies?: ProposalComment[];
  repliesExpanded?: boolean;
  isOwnComment?: boolean;
  canKick?: boolean;
}

export interface ProposalDetail extends ProposalListItem {
  createdAt: Date;
  canEdit: boolean;
  canDelete: boolean;
  canVote: boolean;
  usesAnonymousComments?: boolean;
  viewerAlias?: string | null;
  canKickAuthor?: boolean;
  description?: string;
  attachments?: ProposalAttachment[];
  resolvedAttachments?: ResolvedAttachment[];
  comments: ProposalComment[];
}

export interface ProposalListResponse {
  success: boolean;
  message: string;
  items: ProposalListItem[];
}

export interface ProposalDetailResponse {
  success: boolean;
  message: string;
  proposal?: ProposalDetail;
}

export interface ProposalOperationResponse {
  success: boolean;
  message: string;
  proposalId?: number;
  commentId?: number;
  alias?: string;
}

export interface ProposalCommentRepliesResponse {
  success: boolean;
  message: string;
  items: ProposalComment[];
}

export interface PendingAttachment {
  file?: File;
  type: 'image' | 'video' | 'audio';
  resourceId: string;
  previewUrl?: string;
  blob?: Blob;
}
