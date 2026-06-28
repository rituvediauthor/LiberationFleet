import { EncryptedPayload } from './crypto.model';
import { CrewmateFriendshipState, mapFriendshipState } from './crewmate.model';
import { ResolvedAttachment } from './proposal.model';

export interface FriendListItem {
  userId: number;
  username: string;
  lastLoginAt: string | null;
  lastMessageAt: string | null;
  isMuted: boolean;
}

export interface FriendListResponse {
  success: boolean;
  message: string;
  items: FriendListItem[];
}

export type FriendRequestDirection = 'incoming' | 'outgoing';

export interface FriendRequestListItem {
  userId: number;
  username: string;
  lastLoginAt: string | null;
  direction: FriendRequestDirection;
  createdAt: string;
}

export interface FriendRequestListResponse {
  success: boolean;
  message: string;
  items: FriendRequestListItem[];
}

export interface BlockedUserListItem {
  userId: number;
  username: string;
  blockedAt: string;
}

export interface BlockedUserListResponse {
  success: boolean;
  message: string;
  items: BlockedUserListItem[];
}

export interface UserSearchResult {
  userId: number;
  username: string;
  friendshipState: CrewmateFriendshipState;
}

export interface UserSearchResponse {
  success: boolean;
  message: string;
  items: UserSearchResult[];
}

export interface DirectMessage {
  id: number;
  authorUserId: number;
  authorUsername: string;
  createdAt: string;
  hasEncryptedContent: boolean;
  encryptedPayload?: EncryptedPayload | null;
  body?: string;
  resolvedAttachments?: ResolvedAttachment[];
}

export interface DirectMessageListResponse {
  success: boolean;
  message: string;
  items: DirectMessage[];
  hasMore: boolean;
  friendUsername: string;
}

export interface DirectMessageOperationResponse {
  success: boolean;
  message: string;
  messageId?: number;
}

export interface SendDirectMessageRequest {
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
}

export function mapFriendRequestDirection(value: number | string): FriendRequestDirection {
  const normalized = typeof value === 'string' ? value.toLowerCase() : '';
  if (normalized === 'outgoing' || value === 1) {
    return 'outgoing';
  }
  return 'incoming';
}

export function mapUserSearchResult(item: UserSearchResult): UserSearchResult {
  return {
    ...item,
    friendshipState: mapFriendshipState(item.friendshipState as unknown as number | string)
  };
}
