import { LibraryTaskListItem, LibraryUnitListItem } from './library.model';

export type CrewDashboardPreviewKind = 'forum' | 'chat' | 'gift' | 'library';

export interface ForumDashboardPreview {
  kind: 'forum';
  id: number;
  authorUsername: string;
  authorAvatarResourceId?: string | null;
  lastActivityAt: Date | string;
  title: string;
  body: string;
  thumbnailUrl?: string | null;
}

export interface ChatDashboardPreview {
  kind: 'chat';
  roomId: number;
  messageId: number;
  authorUsername: string;
  authorAvatarResourceId?: string | null;
  createdAt: string;
  body: string;
  hasAttachments: boolean;
  isAnonymous?: boolean;
}

export interface GiftDashboardPreview {
  kind: 'gift';
  id: number;
  message: string;
  categoryLabel: string;
  timestamp: Date | string;
  unverified: boolean;
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
}

export type CrewDashboardPreview =
  | ForumDashboardPreview
  | ChatDashboardPreview
  | GiftDashboardPreview
  | LibraryDashboardPreview;

export type CrewDashboardPreviewOpenTarget =
  | ForumDashboardPreview
  | ChatDashboardPreview
  | GiftDashboardPreview
  | LibraryDashboardPreviewItem;

export interface CrewDashboardPreviews {
  forum: ForumDashboardPreview | null;
  chat: ChatDashboardPreview | null;
  gift: GiftDashboardPreview | null;
  library: LibraryDashboardPreview | null;
}
