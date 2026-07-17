import { EncryptedPayload } from './crypto.model';
import { PendingAttachment, ProposalAttachment, ResolvedAttachment } from './proposal.model';

export interface DiscussionEncryptedPayload {
  title: string;
  description: string;
  authorDisplayName?: string;
  attachments?: ProposalAttachment[];
  thumbnailResourceId?: string | null;
}

export interface DiscussionCommentEncryptedPayload {
  body: string;
  authorDisplayName?: string;
  attachments?: ProposalAttachment[];
}

export interface DiscussionListItem {
  id: number;
  authorUserId: number;
  authorUsername: string;
  authorAvatarResourceId?: string | null;
  lastActivityAt: Date;
  hasEncryptedContent?: boolean;
  encryptedPayload?: EncryptedPayload | null;
  title?: string;
  descriptionPreview?: string;
  thumbnailUrl?: string | null;
  isAdultContent?: boolean;
}

export interface DiscussionComment {
  id: number;
  authorUserId: number;
  authorUsername: string;
  authorAvatarResourceId?: string | null;
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
  replies?: DiscussionComment[];
  repliesExpanded?: boolean;
}

export interface DiscussionDetail extends DiscussionListItem {
  createdAt: Date;
  canEdit: boolean;
  canDelete: boolean;
  description?: string;
  attachments?: ProposalAttachment[];
  resolvedAttachments?: ResolvedAttachment[];
  comments: DiscussionComment[];
}

export interface DiscussionListResponse {
  success: boolean;
  message: string;
  items: DiscussionListItem[];
}

export interface DiscussionDetailResponse {
  success: boolean;
  message: string;
  post?: DiscussionDetail;
}

export interface DiscussionOperationResponse {
  success: boolean;
  message: string;
  postId?: number;
  commentId?: number;
}

export interface DiscussionCommentRepliesResponse {
  success: boolean;
  message: string;
  items: DiscussionComment[];
}

export type { PendingAttachment, ProposalAttachment, ResolvedAttachment };
