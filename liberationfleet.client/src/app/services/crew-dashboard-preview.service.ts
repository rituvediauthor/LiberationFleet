import { Injectable, inject } from '@angular/core';
import { firstValueFrom, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { FORUM_DISCUSSION_CONFIG } from '../config/discussion.config';
import {
  ChatDashboardPreview,
  CrewDashboardPreviews,
  ForumDashboardPreview,
  GiftDashboardPreview,
  LibraryDashboardPreview,
  LibraryDashboardPreviewItem
} from '../models/crew-dashboard-preview.model';
import { ChatMessage, ChatRoomListItem } from '../models/chat.model';
import { DiscussionListItem } from '../models/crew-discussion.model';
import { GiftLogEntry } from '../models/gift.model';
import { ProposalListItem } from '../models/proposal.model';
import { AdultContentService } from './adult-content.service';
import { ChatService } from './chat.service';
import { ChatCryptoService } from './crypto/chat-crypto.service';
import { CryptoSessionService } from './crypto/crypto-session.service';
import { GiftLogCryptoService } from './crypto/gift-log-crypto.service';
import { LibraryCryptoService } from './crypto/library-crypto.service';
import { ProposalCryptoService } from './crypto/proposal-crypto.service';
import { CrewDiscussionService } from './crew-discussion.service';
import { ForumListPrefetchService } from './forum-list-prefetch.service';
import { GiftService } from './gift.service';
import { LibraryService } from './library.service';
import { AppStorageService, StorageScope } from './storage/app-storage.service';
import { AUTH_TOKEN_STORAGE_KEY, CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY } from './storage/storage-keys';
import { getUserIdFromToken } from '../utils/jwt.util';
import {
  formatQuestDueLabel,
  giftCategoryLabel,
  LibraryPreviewCandidate,
  pickLibraryPreviewCarousel,
  pickFirstAllowedAdult,
  truncateDashboardPreview
} from '../utils/dashboard-preview.util';

const LIBRARY_UNIT_SAMPLE_LIMIT = 5;
const LIBRARY_TASK_SAMPLE_LIMIT = 8;
const LIBRARY_CAROUSEL_SIZE = 5;
const CHAT_PREVIEW_ROOM_BATCH = 3;
const PREVIEW_CACHE_TTL_MS = 90_000;
type CrewPreviewKey = keyof CrewDashboardPreviews;

interface PreviewCacheEntry {
  preview: CrewDashboardPreviews[CrewPreviewKey];
  fetchedAt: number;
}

interface PersistedPreviewCache {
  userId: number;
  entries: [string, PreviewCacheEntry][];
}

@Injectable({
  providedIn: 'root'
})
export class CrewDashboardPreviewService {
  private discussionService = inject(CrewDiscussionService);
  private forumPrefetch = inject(ForumListPrefetchService);
  private proposalCrypto = inject(ProposalCryptoService);
  private chatService = inject(ChatService);
  private chatCrypto = inject(ChatCryptoService);
  private giftService = inject(GiftService);
  private giftLogCrypto = inject(GiftLogCryptoService);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private cryptoSession = inject(CryptoSessionService);
  private adultContent = inject(AdultContentService);
  private storage = inject(AppStorageService);
  private cache = new Map<string, PreviewCacheEntry>();
  private inFlight = new Map<string, Promise<CrewDashboardPreviews[CrewPreviewKey]>>();
  private hydratedUserId: number | null = null;

  async loadPreviews(
    crewId: number,
    options?: { force?: boolean }
  ): Promise<CrewDashboardPreviews> {
    if (crewId <= 0) {
      return { forum: null, chat: null, gift: null, library: null };
    }

    const [forum, chat, gift, library] = await Promise.all([
      this.loadPreview(crewId, 'forum', options),
      this.loadPreview(crewId, 'chat', options),
      this.loadPreview(crewId, 'gift', options),
      this.loadPreview(crewId, 'library', options)
    ]);

    return { forum, chat, gift, library };
  }

  async loadPreview<K extends CrewPreviewKey>(
    crewId: number,
    previewKey: K,
    options?: { force?: boolean }
  ): Promise<CrewDashboardPreviews[K]> {
    if (crewId <= 0) {
      return null as CrewDashboardPreviews[K];
    }

    this.ensureHydrated();
    const cacheKey = this.getCacheKey(crewId, previewKey);
    if (options?.force) {
      this.cache.delete(cacheKey);
    } else {
      const cached = this.cache.get(cacheKey);
      if (cached && this.isFresh(cached)) {
        return cached.preview as CrewDashboardPreviews[K];
      }
    }

    const existing = this.inFlight.get(cacheKey);
    if (existing) {
      return existing as Promise<CrewDashboardPreviews[K]>;
    }

    const pending = this.fetchPreview(crewId, previewKey)
      .then(preview => {
        this.cache.set(cacheKey, {
          preview,
          fetchedAt: Date.now()
        });
        this.persistCache();
        return preview;
      })
      .finally(() => {
        this.inFlight.delete(cacheKey);
      });

    this.inFlight.set(cacheKey, pending);
    return pending as Promise<CrewDashboardPreviews[K]>;
  }

  /**
   * Last-seen tile for this crew/session state, including stale entries.
   * `undefined` is a miss; `null` is a cached empty tile.
   */
  peekCachedPreview<K extends CrewPreviewKey>(
    crewId: number,
    previewKey: K
  ): CrewDashboardPreviews[K] | undefined {
    if (crewId <= 0) {
      return undefined;
    }

    this.ensureHydrated();
    const cached = this.cache.get(this.getCacheKey(crewId, previewKey));
    return cached ? cached.preview as CrewDashboardPreviews[K] : undefined;
  }

  clearSessionCache(): void {
    this.cache.clear();
    this.inFlight.clear();
    this.hydratedUserId = null;
    this.storage.remove(StorageScope.Persistent, CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY);
  }

  private async loadForumPreview(crewId: number): Promise<ForumDashboardPreview | null> {
    const peeked = this.forumPrefetch.peekCrewSpacePage(crewId)?.items ?? [];
    if (peeked.length) {
      return this.toForumPreview(
        pickFirstAllowedAdult(
          peeked,
          item => item.isAdultContent,
          flag => this.adultContent.shouldShowEntry(flag)
        )
      );
    }

    if (!this.cryptoSession.isUnlocked()) {
      return null;
    }

    const page = await firstValueFrom(
      this.discussionService.getPosts(FORUM_DISCUSSION_CONFIG, { offset: 0, limit: 5 })
    );
    const rawItems = (page.items ?? []) as DiscussionListItem[];
    for (const candidate of rawItems) {
      if (!this.adultContent.shouldShowEntry(candidate.isAdultContent)) {
        continue;
      }
      const [item] = await this.proposalCrypto.decryptListItems(
        [candidate as ProposalListItem],
        crewId
      ) as DiscussionListItem[];
      const preview = this.toForumPreview(item);
      if (preview) {
        return preview;
      }
    }
    return null;
  }

  private toForumPreview(item: DiscussionListItem | null | undefined): ForumDashboardPreview | null {
    if (!item) {
      return null;
    }
    if (item.hasEncryptedContent && !this.cryptoSession.isUnlocked()) {
      return null;
    }
    if (item.title === '[Encrypted]' || item.title === '[Unable to decrypt]') {
      return null;
    }

    return {
      kind: 'forum',
      id: item.id,
      authorUsername: item.authorUsername,
      authorAvatarResourceId: item.authorAvatarResourceId,
      lastActivityAt: item.lastActivityAt,
      title: item.title || 'Untitled post',
      body: truncateDashboardPreview(item.descriptionPreview),
      thumbnailUrl: item.thumbnailUrl ?? item.previewImageUrls?.[0] ?? null
    };
  }

  private async loadChatPreview(crewId: number): Promise<ChatDashboardPreview | null> {
    if (!this.cryptoSession.isUnlocked()) {
      return null;
    }

    const roomsResponse = await firstValueFrom(this.chatService.getRooms());
    if (!roomsResponse.success) {
      return null;
    }

    const textRooms = (roomsResponse.items ?? [])
      .filter(room => room.roomType === 'Text')
      .filter(room => this.adultContent.shouldShowEntry(room.isAdultContent))
      .sort((a, b) => new Date(b.lastActivityAt).getTime() - new Date(a.lastActivityAt).getTime());

    for (let index = 0; index < textRooms.length; index += CHAT_PREVIEW_ROOM_BATCH) {
      const batch = textRooms.slice(index, index + CHAT_PREVIEW_ROOM_BATCH);
      const previews = await Promise.all(batch.map(room => this.loadLatestMessagePreview(crewId, room)));
      const preview = previews.find(item => !!item);
      if (preview) {
        return preview;
      }
    }
    return null;
  }

  private async loadLatestMessagePreview(
    crewId: number,
    room: ChatRoomListItem
  ): Promise<ChatDashboardPreview | null> {
    const response = await firstValueFrom(this.chatService.getMessages(room.id, 1));
    if (!response.success) {
      return null;
    }
    const raw = response.items ?? [];
    if (!raw.length) {
      return null;
    }

    const messages = await this.chatCrypto.decryptMessages(raw, { crewId });
    // limit=1 arrives chronological after reverse — sole item is newest.
    const message = messages[messages.length - 1] as ChatMessage | undefined;
    if (!message) {
      return null;
    }

    const rawBody = (message.body ?? '').trim();
    if (
      rawBody === '[Unlock encryption to view]'
      || rawBody === '[Unable to decrypt]'
      || rawBody.startsWith('[Encrypted')
    ) {
      return null;
    }

    const hasAttachments = (message.resolvedAttachments?.length ?? 0) > 0;
    const body = truncateDashboardPreview(rawBody) || (hasAttachments ? 'Attachment' : '');
    if (!body) {
      return null;
    }

    return {
      kind: 'chat',
      roomId: room.id,
      messageId: message.id,
      authorUsername: message.isAnonymous ? 'Anonymous' : (message.authorUsername || 'Unknown'),
      authorAvatarResourceId: message.isAnonymous ? null : message.authorAvatarResourceId,
      createdAt: message.createdAt,
      body,
      hasAttachments,
      isAnonymous: !!message.isAnonymous
    };
  }

  private async loadGiftPreview(crewId: number): Promise<GiftDashboardPreview | null> {
    if (!this.cryptoSession.isUnlocked()) {
      return null;
    }

    const page = await firstValueFrom(this.giftService.getLogs({ limit: 1 }));
    const raw = page.items ?? [];
    if (!raw.length) {
      return null;
    }

    const entries = await this.giftLogCrypto.decryptEntries(raw, crewId);
    const entry = entries[entries.length - 1] as GiftLogEntry | undefined;
    if (!entry) {
      return null;
    }
    if (entry.hasEncryptedContent && (
      !entry.message
      || entry.message.startsWith('[Unable to decrypt')
      || entry.message.startsWith('[Encrypted')
    )) {
      return null;
    }

    const message = truncateDashboardPreview(entry.message);
    if (!message) {
      return null;
    }

    return {
      kind: 'gift',
      id: entry.id,
      message,
      categoryLabel: giftCategoryLabel(entry.customGiftCategory),
      timestamp: entry.timestamp,
      unverified: entry.displayFlag === 'unverified' || entry.status === 'unverified'
    };
  }

  private async loadLibraryPreview(crewId: number): Promise<LibraryDashboardPreview | null> {
    const emptyPage = { items: [] as never[], hasMore: false };
    const [durable, consumable, services, digital, deadlineTasks, noDeadlineTasks] = await Promise.all([
      firstValueFrom(
        this.libraryService.getDurableUnits({ limit: LIBRARY_UNIT_SAMPLE_LIMIT, offset: 0 }).pipe(
          catchError(() => of(emptyPage))
        )
      ),
      firstValueFrom(
        this.libraryService.getConsumableUnits({ limit: LIBRARY_UNIT_SAMPLE_LIMIT, offset: 0 }).pipe(
          catchError(() => of(emptyPage))
        )
      ),
      firstValueFrom(
        this.libraryService.getServicesUnits({ limit: LIBRARY_UNIT_SAMPLE_LIMIT, offset: 0 }).pipe(
          catchError(() => of(emptyPage))
        )
      ),
      firstValueFrom(
        this.libraryService.getDigitalUnits({ limit: LIBRARY_UNIT_SAMPLE_LIMIT, offset: 0 }).pipe(
          catchError(() => of(emptyPage))
        )
      ),
      firstValueFrom(this.libraryService.getTasks().pipe(catchError(() => of([])))),
      firstValueFrom(this.libraryService.getNoDeadlineTasks().pipe(catchError(() => of([]))))
    ]);

    const rawUnits = [
      ...(durable.items ?? []),
      ...(consumable.items ?? []),
      ...(services.items ?? []),
      ...(digital.items ?? [])
    ];
    const rawTasks = [
      ...(deadlineTasks ?? []),
      ...(noDeadlineTasks ?? [])
    ].slice(0, LIBRARY_TASK_SAMPLE_LIMIT);

    const candidates: LibraryPreviewCandidate[] = [
      ...rawUnits
        .filter(unit => !!unit.title && unit.title !== 'Encrypted quest')
        .map(unit => ({ type: 'unit' as const, unit })),
      ...rawTasks
        .filter(task => !!task.title && task.title !== 'Encrypted quest')
        .map(task => ({ type: 'quest' as const, task }))
    ];
    const picked = pickLibraryPreviewCarousel(candidates, LIBRARY_CAROUSEL_SIZE);
    const pickedUnits = picked
      .filter((candidate): candidate is { type: 'unit'; unit: typeof rawUnits[number] } => candidate.type === 'unit')
      .map(candidate => candidate.unit);
    const pickedTasks = picked
      .filter((candidate): candidate is { type: 'quest'; task: typeof rawTasks[number] } => candidate.type === 'quest')
      .map(candidate => candidate.task);
    const [units, tasks] = await Promise.all([
      this.libraryCrypto.enrichUnitListItems(pickedUnits, crewId),
      this.libraryCrypto.enrichTaskListItems(pickedTasks, crewId)
    ]);
    const unitById = new Map(units.map(unit => [unit.unitId, unit]));
    const taskById = new Map(tasks.map(task => [task.taskId, task]));
    const items = picked
      .map(candidate => {
        if (candidate.type === 'unit') {
          const unit = unitById.get(candidate.unit.unitId);
          return unit ? this.toLibraryPreviewItem({ type: 'unit', unit }) : null;
        }
        const task = taskById.get(candidate.task.taskId);
        return task && task.title !== 'Encrypted quest'
          ? this.toLibraryPreviewItem({ type: 'quest', task })
          : null;
      })
      .filter((item): item is LibraryDashboardPreviewItem => !!item);
    if (!items.length) {
      return null;
    }

    return {
      kind: 'library',
      items
    };
  }

  private toLibraryPreviewItem(candidate: LibraryPreviewCandidate): LibraryDashboardPreviewItem {
    if (candidate.type === 'unit') {
      return {
        libraryKind: 'unit',
        unit: {
          ...candidate.unit,
          descriptionPreview: truncateDashboardPreview(candidate.unit.descriptionPreview)
        }
      };
    }

    return {
      libraryKind: 'quest',
      task: {
        ...candidate.task,
        title: truncateDashboardPreview(candidate.task.title) || candidate.task.title
      },
      dueLabel: formatQuestDueLabel(candidate.task)
    };
  }

  private fetchPreview<K extends CrewPreviewKey>(
    crewId: number,
    previewKey: K
  ): Promise<CrewDashboardPreviews[K]> {
    const loaders: Record<CrewPreviewKey, () => Promise<CrewDashboardPreviews[CrewPreviewKey]>> = {
      forum: () => this.loadForumPreview(crewId),
      chat: () => this.loadChatPreview(crewId),
      gift: () => this.loadGiftPreview(crewId),
      library: () => this.loadLibraryPreview(crewId)
    };
    return loaders[previewKey]().catch(() => null) as Promise<CrewDashboardPreviews[K]>;
  }

  private getCacheKey(crewId: number, previewKey: CrewPreviewKey): string {
    const unlocked = this.cryptoSession.isUnlocked() ? 'unlocked' : 'locked';
    const adultPreference = this.adultContent.preference;
    return `${crewId}:${previewKey}:${unlocked}:${adultPreference}`;
  }

  private isFresh(entry: PreviewCacheEntry): boolean {
    return Date.now() - entry.fetchedAt < PREVIEW_CACHE_TTL_MS;
  }

  private ensureHydrated(): void {
    const userId = this.readCurrentUserId();
    if (userId == null) {
      if (this.hydratedUserId != null) {
        this.cache.clear();
        this.hydratedUserId = null;
      }
      return;
    }
    if (this.hydratedUserId === userId) {
      return;
    }

    this.hydratedUserId = userId;
    this.cache = this.readPersistedCache(userId);
  }

  private persistCache(): void {
    const userId = this.readCurrentUserId();
    if (userId == null) {
      return;
    }

    const payload: PersistedPreviewCache = {
      userId,
      entries: Array.from(this.cache.entries())
    };
    this.storage.set(
      StorageScope.Persistent,
      CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY,
      JSON.stringify(payload)
    );
  }

  private readPersistedCache(userId: number): Map<string, PreviewCacheEntry> {
    const raw = this.storage.get(StorageScope.Persistent, CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY);
    if (!raw) {
      return new Map();
    }

    try {
      const parsed = JSON.parse(raw) as PersistedPreviewCache;
      if (parsed.userId !== userId || !Array.isArray(parsed.entries)) {
        return new Map();
      }
      return new Map(
        parsed.entries.filter(entry => Array.isArray(entry) && entry.length === 2 && typeof entry[1]?.fetchedAt === 'number')
      );
    } catch {
      return new Map();
    }
  }

  private readCurrentUserId(): number | null {
    const token = this.storage.get(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY)
      ?? this.storage.get(StorageScope.Session, AUTH_TOKEN_STORAGE_KEY);
    return token ? getUserIdFromToken(token) : null;
  }
}
