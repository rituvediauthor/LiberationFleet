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
  previewImageUrls?: string[];
  /** True when the encrypted payload includes a video attachment. */
  hasVideoAttachment?: boolean;
  isAdultContent?: boolean;
  likeCount?: number;
  likedByCurrentUser?: boolean;
  commentCount?: number;
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
  likeCount?: number;
  likedByCurrentUser?: boolean;
}

export interface ForumLikeToggleResponse {
  success: boolean;
  message: string;
  liked: boolean;
  likeCount: number;
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
  hasMore?: boolean;
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
