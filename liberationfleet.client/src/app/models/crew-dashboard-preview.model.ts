import { LibraryTaskListItem, LibraryUnitListItem } from './library.model';

export type CrewDashboardPreviewKind = 'forum' | 'chat' | 'gift' | 'library';

export interface ForumDashboardPreviewItem {
  id: number;
  authorUserId: number;
  authorUsername: string;
  authorAvatarResourceId?: string | null;
  lastActivityAt: Date | string;
  title: string;
  body: string;
  thumbnailUrl?: string | null;
}

export interface ForumDashboardPreview {
  kind: 'forum';
  items: ForumDashboardPreviewItem[];
  hasMore: boolean;
}

export interface ChatDashboardPreviewItem {
  roomId: number;
  messageId: number;
  authorUserId: number;
  authorUsername: string;
  authorAvatarResourceId?: string | null;
  createdAt: string;
  body: string;
  hasAttachments: boolean;
  isAnonymous?: boolean;
}

export interface ChatDashboardPreview {
  kind: 'chat';
  items: ChatDashboardPreviewItem[];
  hasMore: boolean;
}

export interface GiftDashboardPreviewItem {
  id: number;
  message: string;
  categoryLabel: string;
  timestamp: Date | string;
  unverified: boolean;
}

export interface GiftDashboardPreview {
  kind: 'gift';
  items: GiftDashboardPreviewItem[];
  hasMore: boolean;
}

export interface LibraryUnitDashboardPreview {
  libraryKind: 'unit';
  unit: LibraryUnitListItem;
}

export interface LibraryQuestDashboardPreview {
  libraryKind: 'quest';
  task: LibraryTaskListItem;
  dueLabel: string;
}

export type LibraryDashboardPreviewItem = LibraryUnitDashboardPreview | LibraryQuestDashboardPreview;

export interface LibraryDashboardPreview {
  kind: 'library';
  items: LibraryDashboardPreviewItem[];
  hasMore: boolean;
}

export type CrewDashboardPreview =
  | ForumDashboardPreview
  | ChatDashboardPreview
  | GiftDashboardPreview
  | LibraryDashboardPreview;

export type CrewDashboardPreviewOpenTarget =
  | ForumDashboardPreviewItem
  | ChatDashboardPreviewItem
  | GiftDashboardPreviewItem
  | LibraryDashboardPreviewItem;

export interface CrewDashboardPreviews {
  forum: ForumDashboardPreview | null;
  chat: ChatDashboardPreview | null;
  gift: GiftDashboardPreview | null;
  library: LibraryDashboardPreview | null;
}
