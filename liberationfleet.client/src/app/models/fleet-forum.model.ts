import { EncryptedPayload } from './crypto.model';
import { PendingAttachment, ProposalAttachment, ResolvedAttachment } from './proposal.model';

export interface FleetForumListItem {
  id: number;
  authorUserId: number;
  authorUsername: string;
  authorAvatarResourceId?: string | null;
  lastActivityAt: string;
  title?: string | null;
  body?: string | null;
  descriptionPreview?: string | null;
  thumbnailUrl?: string | null;
  previewImageUrls?: string[];
  isAdultContent: boolean;
  hasEncryptedContent: boolean;
  encryptedPayload?: EncryptedPayload | null;
  likeCount?: number;
  likedByCurrentUser?: boolean;
  commentCount?: number;
}

export interface FleetForumComment {
  id: number;
  authorUserId: number;
  authorUsername: string;
  authorAvatarResourceId?: string | null;
  parentCommentId?: number | null;
  replyToCommentId?: number | null;
  replyToUsername?: string | null;
  createdAt: string;
  replyCount: number;
  body?: string | null;
  hasEncryptedContent?: boolean;
  encryptedPayload?: EncryptedPayload | null;
  attachments?: ProposalAttachment[];
  resolvedAttachments?: ResolvedAttachment[];
  replies?: FleetForumComment[];
  repliesExpanded?: boolean;
  likeCount?: number;
  likedByCurrentUser?: boolean;
}

export interface FleetForumLikeToggleResponse {
  success: boolean;
  message: string;
  liked: boolean;
  likeCount: number;
}

export interface FleetForumPost extends FleetForumListItem {
  createdAt: string;
  canEdit: boolean;
  canDelete: boolean;
  description?: string | null;
  attachments?: ProposalAttachment[];
  resolvedAttachments?: ResolvedAttachment[];
  comments: FleetForumComment[];
}

export interface FleetForumListResponse {
  success: boolean;
  message: string;
  items: FleetForumListItem[];
}

export interface FleetForumDetailResponse {
  success: boolean;
  message: string;
  post?: FleetForumPost;
}

export interface FleetForumCommentRepliesResponse {
  success: boolean;
  message: string;
  items: FleetForumComment[];
}

export interface FleetForumOperationResponse {
  success: boolean;
  message: string;
  postId?: number;
  commentId?: number;
}

export interface EncryptedFleetForumSend {
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
}

export interface CreateFleetForumRequest extends EncryptedFleetForumSend {
  isAdultContent: boolean;
  mentionedUserIds?: number[];
  notificationPreview?: string | null;
}

export interface UpdateFleetForumRequest extends EncryptedFleetForumSend {
  mentionedUserIds?: number[];
}

export interface CreateFleetForumCommentRequest extends EncryptedFleetForumSend {
  parentCommentId?: number | null;
  mentionedUserIds?: number[];
  notificationPreview?: string | null;
}

export interface UpdateFleetForumCommentRequest extends EncryptedFleetForumSend {
  mentionedUserIds?: number[];
}

export type { PendingAttachment, ProposalAttachment, ResolvedAttachment };
