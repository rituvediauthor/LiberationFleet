import { EncryptedContentType } from './crypto.model';

export type UserActivityFilterCategory =
  | 'All'
  | 'Chats'
  | 'Forums'
  | 'Library'
  | 'Gifts'
  | 'Proposals';

export type UserActivityKind =
  | 'ChatRoom'
  | 'ChatMessage'
  | 'ForumPost'
  | 'ForumComment'
  | 'LibraryOffering'
  | 'LibraryRequest'
  | 'LibraryRequestMessage'
  | 'LibraryMaintenance'
  | 'Gift'
  | 'Proposal'
  | 'ProposalComment';

export interface UserActivityItem {
  key: string;
  kind: UserActivityKind;
  category: UserActivityFilterCategory;
  label: string;
  detail?: string | null;
  createdAt: string;
  crewId: number;
  resourceId: number;
  parentResourceId?: number | null;
  relatedUserId?: number | null;
  chatRoomType?: 'Text' | 'Voice' | null;
  libraryUnitId?: number | null;
  isAccessible: boolean;
  previewContentType?: EncryptedContentType | null;
  thumbnailResourceId?: string | null;
  plaintextPreview?: string | null;
  previewText?: string | null;
  thumbnailUrl?: string | null;
}

export interface UserActivityListResponse {
  success: boolean;
  message: string;
  items: UserActivityItem[];
  hasMore: boolean;
}

export const ACTIVITY_FILTER_OPTIONS: { value: UserActivityFilterCategory; label: string }[] = [
  { value: 'All', label: 'All' },
  { value: 'Chats', label: 'Chats' },
  { value: 'Forums', label: 'Forums' },
  { value: 'Library', label: 'Library of Things' },
  { value: 'Gifts', label: 'Gifts' },
  { value: 'Proposals', label: 'Proposals' }
];
