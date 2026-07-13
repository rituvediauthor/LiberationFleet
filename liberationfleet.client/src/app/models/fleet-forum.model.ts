export interface FleetForumListItem {
  id: number;
  authorUserId: number;
  authorUsername: string;
  lastActivityAt: string;
  title?: string | null;
  body?: string | null;
  isAdultContent: boolean;
  hasEncryptedContent: boolean;
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
  replies?: FleetForumComment[];
  repliesExpanded?: boolean;
}

export interface FleetForumPost extends FleetForumListItem {
  createdAt: string;
  canEdit: boolean;
  canDelete: boolean;
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

export interface CreateFleetForumRequest {
  title: string;
  body: string;
  isAdultContent: boolean;
}

export interface UpdateFleetForumRequest {
  title: string;
  body: string;
}

export interface CreateFleetForumCommentRequest {
  parentCommentId?: number | null;
  body: string;
}

export interface UpdateFleetForumCommentRequest {
  body: string;
}
