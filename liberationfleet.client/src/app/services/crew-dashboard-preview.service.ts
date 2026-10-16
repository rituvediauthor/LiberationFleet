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
import {
  formatQuestDueLabel,
  giftCategoryLabel,
  LibraryPreviewCandidate,
  pickLibraryPreviewCarousel,
  pickFirstAllowedAdult,
  truncateDashboardPreview
} from '../utils/dashboard-preview.util';

const LIBRARY_UNIT_SAMPLE_LIMIT = 10;
const LIBRARY_CAROUSEL_SIZE = 5;
const PREVIEW_CACHE_TTL_MS = 90_000;
type CrewPreviewKey = keyof CrewDashboardPreviews;

interface PreviewCacheEntry {
  preview: CrewDashboardPreviews[CrewPreviewKey];
  fetchedAt: number;
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
  private cache = new Map<string, PreviewCacheEntry>();
  private inFlight = new Map<string, Promise<CrewDashboardPreviews[CrewPreviewKey]>>();

  async loadPreviews(crewId: number): Promise<CrewDashboardPreviews> {
    if (crewId <= 0) {
      return { forum: null, chat: null, gift: null, library: null };
    }

    const [forum, chat, gift, library] = await Promise.all([
      this.loadPreview(crewId, 'forum'),
      this.loadPreview(crewId, 'chat'),
      this.loadPreview(crewId, 'gift'),
      this.loadPreview(crewId, 'library')
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
        return preview;
      })
      .finally(() => {
        this.inFlight.delete(cacheKey);
      });

    this.inFlight.set(cacheKey, pending);
    return pending as Promise<CrewDashboardPreviews[K]>;
  }

  clearSessionCache(): void {
    this.cache.clear();
    this.inFlight.clear();
  }

  private async loadForumPreview(crewId: number): Promise<ForumDashboardPreview | null> {
    const peeked = this.forumPrefetch.peekCrewSpacePage(crewId)?.items ?? [];
    let items = peeked;

    if (!items.length) {
      if (!this.cryptoSession.isUnlocked()) {
        return null;
      }
      const page = await firstValueFrom(
        this.discussionService.getPosts(FORUM_DISCUSSION_CONFIG, { offset: 0, limit: 5 })
      );
      items = await this.proposalCrypto.decryptListItems(
        page.items as ProposalListItem[],
        crewId
      ) as DiscussionListItem[];
    }

    const item = pickFirstAllowedAdult(
      items,
      i => i.isAdultContent,
      flag => this.adultContent.shouldShowEntry(flag)
    );
    if (!item) {
      return null;
    }
    if (item.hasEncryptedContent && !this.cryptoSession.isUnlocked()) {
      return null;
    }
    if (item.title === '[Encrypted]' || item.title === '[Unable to decrypt]') {
      return null;
    }

    const body = truncateDashboardPreview(item.descriptionPreview);
    return {
      kind: 'forum',
      id: item.id,
      authorUsername: item.authorUsername,
      authorAvatarResourceId: item.authorAvatarResourceId,
      lastActivityAt: item.lastActivityAt,
      title: item.title || 'Untitled post',
      body,
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

    const rooms = await this.chatCrypto.decryptRooms(roomsResponse.items ?? [], { crewId });
    const textRooms = rooms
      .filter(room => room.roomType === 'Text')
      .filter(room => this.adultContent.shouldShowEntry(room.isAdultContent))
      .sort((a, b) => new Date(b.lastActivityAt).getTime() - new Date(a.lastActivityAt).getTime());

    for (const room of textRooms) {
      const preview = await this.loadLatestMessagePreview(crewId, room);
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
    const units = await this.libraryCrypto.enrichUnitListItems(rawUnits, crewId);
    const tasks = await this.libraryCrypto.enrichTaskListItems(
      [...(deadlineTasks ?? []), ...(noDeadlineTasks ?? [])],
      crewId
    );

    const candidates: LibraryPreviewCandidate[] = [
      ...units
        .filter(unit => !!unit.title && unit.title !== 'Encrypted quest')
        .map(unit => ({ type: 'unit' as const, unit })),
      ...tasks
        .filter(task => !!task.title && task.title !== 'Encrypted quest')
        .map(task => ({ type: 'quest' as const, task }))
    ];

    const items = pickLibraryPreviewCarousel(candidates, LIBRARY_CAROUSEL_SIZE)
      .map(candidate => this.toLibraryPreviewItem(candidate));
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
}
