import { EncryptedPayload } from './crypto.model';

export interface FleetForumListItem {
  id: number;
  authorUserId: number;
  authorUsername: string;
  lastActivityAt: string;
  title?: string | null;
  body?: string | null;
  descriptionPreview?: string | null;
  isAdultContent: boolean;
  hasEncryptedContent: boolean;
  encryptedPayload?: EncryptedPayload | null;
}

export interface FleetForumComment {
  id: number;
  authorUserId: number;
  authorUsername: string;
  parentCommentId?: number | null;
  replyToCommentId?: number | null;
  replyToUsername?: string | null;
  createdAt: string;
  replyCount: number;
  body?: string | null;
  hasEncryptedContent?: boolean;
  encryptedPayload?: EncryptedPayload | null;
  replies?: FleetForumComment[];
  repliesExpanded?: boolean;
}

export interface FleetForumPost extends FleetForumListItem {
  createdAt: string;
  canEdit: boolean;
  canDelete: boolean;
  description?: string | null;
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
}

export interface UpdateFleetForumRequest extends EncryptedFleetForumSend {
  mentionedUserIds?: number[];
}

export interface CreateFleetForumCommentRequest extends EncryptedFleetForumSend {
  parentCommentId?: number | null;
  mentionedUserIds?: number[];
}

export interface UpdateFleetForumCommentRequest extends EncryptedFleetForumSend {
  mentionedUserIds?: number[];
}
