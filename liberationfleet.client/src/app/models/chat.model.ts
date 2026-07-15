import { EncryptedPayload } from './crypto.model';
import { ResolvedAttachment } from './proposal.model';

export type ChatRoomType = 'Text' | 'Voice';

export interface ChatRoomListItem {
  id: number;
  name: string;
  purpose?: string;
  hasEncryptedContent?: boolean;
  encryptedPayload?: EncryptedPayload | null;
  roomType: ChatRoomType;
  createdByUserId: number;
  createdByUsername: string;
  createdAt: string;
  lastActivityAt: string;
  anonymousModeEnabled?: boolean;
  canToggleAnonymousMode?: boolean;
  isAdultContent?: boolean;
}

export interface ChatRoomDetail extends ChatRoomListItem {}

export interface ChatMessage {
  id: number;
  authorUserId: number;
  authorUsername: string;
  createdAt: string;
  hasEncryptedContent: boolean;
  encryptedPayload?: EncryptedPayload | null;
  body?: string;
  resolvedAttachments?: ResolvedAttachment[];
}

export interface ChatRoomListResponse {
  success: boolean;
  message: string;
  items: ChatRoomListItem[];
}

export interface ChatRoomDetailResponse {
  success: boolean;
  message: string;
  room?: ChatRoomDetail;
}

export interface ChatMessageListResponse {
  success: boolean;
  message: string;
  items: ChatMessage[];
  hasMore: boolean;
}

export interface ChatOperationResponse {
  success: boolean;
  message: string;
  roomId?: number;
  messageId?: number;
  proposalsSubmitted?: boolean;
  proposalId?: number;
}

export interface CreateChatRoomRequest {
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
  roomType: ChatRoomType;
  purpose: string;
  plaintextName: string;
  isAdultContent?: boolean;
  scope?: 'crew' | 'fleet';
}

export interface UpdateChatRoomRequest {
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
  roomType: ChatRoomType;
  purpose: string;
  plaintextName: string;
  plaintextOldName: string;
  plaintextOldPurpose: string;
}

export interface DeleteChatRoomRequest {
  plaintextName: string;
  plaintextPurpose: string;
}

export interface SendChatMessageRequest {
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
}

export interface ChatRoomNamePayload {
  name: string;
}
