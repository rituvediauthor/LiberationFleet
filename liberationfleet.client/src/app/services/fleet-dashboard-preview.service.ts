import { Injectable, inject } from '@angular/core';
import { firstValueFrom, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  ChatDashboardPreview,
  ChatDashboardPreviewItem,
  CrewDashboardPreviews,
  ForumDashboardPreview,
  ForumDashboardPreviewItem,
  GiftDashboardPreview,
  GiftDashboardPreviewItem,
  LibraryDashboardPreview,
  LibraryDashboardPreviewItem
} from '../models/crew-dashboard-preview.model';
import { ChatMessage, ChatRoomListItem } from '../models/chat.model';
import { FleetForumListItem } from '../models/fleet-forum.model';
import { GiftLogEntry } from '../models/gift.model';
import { ProposalListItem } from '../models/proposal.model';
import { AdultContentService } from './adult-content.service';
import { ChatService } from './chat.service';
import { ChatCryptoService } from './crypto/chat-crypto.service';
import { CryptoSessionService } from './crypto/crypto-session.service';
import { GiftLogCryptoService } from './crypto/gift-log-crypto.service';
import { LibraryCryptoService } from './crypto/library-crypto.service';
import { ProposalCryptoService } from './crypto/proposal-crypto.service';
import { CrewService } from './crew.service';
import { FleetService } from './fleet.service';
import { ForumListPrefetchService } from './forum-list-prefetch.service';
import { AppStorageService, StorageScope } from './storage/app-storage.service';
import { AUTH_TOKEN_STORAGE_KEY, FLEET_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY } from './storage/storage-keys';
import { getUserIdFromToken } from '../utils/jwt.util';
import {
  giftCategoryLabel,
  hasUsableLibraryPreviewTitle,
  isLibraryPreviewCandidateEligible,
  LibraryPreviewCandidate,
  shuffleArray,
  truncateDashboardPreview
} from '../utils/dashboard-preview.util';

const INITIAL_PREVIEW_COUNT = 4;
const LIBRARY_UNIT_SAMPLE_LIMIT = 40;
const CHAT_PREVIEW_ROOM_BATCH = 3;
const PREVIEW_CACHE_TTL_MS = 90_000;
type FleetPreviewKey = keyof CrewDashboardPreviews;

interface PreviewCacheEntry {
  preview: CrewDashboardPreviews[FleetPreviewKey];
  fetchedAt: number;
}

interface PersistedPreviewCache {
  userId: number;
  entries: [string, PreviewCacheEntry][];
}

interface ForumLazyState {
  offset: number;
  exhausted: boolean;
}

interface ChatLazyState {
  roomId: number;
  beforeMessageId: number | null;
  exhausted: boolean;
}

interface GiftLazyState {
  beforeId: number | null;
  beforeCreatedAt: string | null;
  exhausted: boolean;
}

interface LibraryLazyState {
  pool: LibraryPreviewCandidate[];
  cursor: number;
  exhausted: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class FleetDashboardPreviewService {
  private fleetService = inject(FleetService);
  private crewService = inject(CrewService);
  private forumPrefetch = inject(ForumListPrefetchService);
  private proposalCrypto = inject(ProposalCryptoService);
  private chatService = inject(ChatService);
  private chatCrypto = inject(ChatCryptoService);
  private giftLogCrypto = inject(GiftLogCryptoService);
  private libraryCrypto = inject(LibraryCryptoService);
  private cryptoSession = inject(CryptoSessionService);
  private adultContent = inject(AdultContentService);
  private storage = inject(AppStorageService);
  private cache = new Map<string, PreviewCacheEntry>();
  private inFlight = new Map<string, Promise<CrewDashboardPreviews[FleetPreviewKey]>>();
  private appendInFlight = new Map<string, Promise<CrewDashboardPreviews[FleetPreviewKey]>>();
  private hydratedUserId: number | null = null;
  private forumLazy = new Map<number, ForumLazyState>();
  private chatLazy = new Map<number, ChatLazyState>();
  private giftLazy = new Map<number, GiftLazyState>();
  private libraryLazy = new Map<number, LibraryLazyState>();

  async loadPreviews(
    fleetId: number,
    options?: { force?: boolean }
  ): Promise<CrewDashboardPreviews> {
    if (fleetId <= 0) {
      return { forum: null, chat: null, gift: null, library: null };
    }

    const [forum, chat, gift, library] = await Promise.all([
      this.loadPreview(fleetId, 'forum', options),
      this.loadPreview(fleetId, 'chat', options),
      this.loadPreview(fleetId, 'gift', options),
      this.loadPreview(fleetId, 'library', options)
    ]);

    return { forum, chat, gift, library };
  }

  async loadPreview<K extends FleetPreviewKey>(
    fleetId: number,
    previewKey: K,
    options?: { force?: boolean }
  ): Promise<CrewDashboardPreviews[K]> {
    if (fleetId <= 0) {
      return null as CrewDashboardPreviews[K];
    }

    this.ensureHydrated();
    if (!this.cryptoSession.isUnlocked()) {
      const peeked = this.peekCachedPreview(fleetId, previewKey);
      return (peeked === undefined ? null : peeked) as CrewDashboardPreviews[K];
    }

    const cacheKey = this.getCacheKey(fleetId, previewKey);
    if (options?.force) {
      this.cache.delete(cacheKey);
      this.clearLazyState(fleetId, previewKey);
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

    const pending = this.fetchPreview(fleetId, previewKey)
      .then(preview => {
        this.cache.set(cacheKey, { preview, fetchedAt: Date.now() });
        this.persistCache();
        return preview;
      })
      .finally(() => {
        this.inFlight.delete(cacheKey);
      });

    this.inFlight.set(cacheKey, pending);
    return pending as Promise<CrewDashboardPreviews[K]>;
  }

  async appendPreviewItem<K extends FleetPreviewKey>(
    fleetId: number,
    previewKey: K
  ): Promise<CrewDashboardPreviews[K]> {
    if (fleetId <= 0 || !this.cryptoSession.isUnlocked()) {
      return this.peekCachedPreview(fleetId, previewKey) ?? null as CrewDashboardPreviews[K];
    }

    const cacheKey = this.getCacheKey(fleetId, previewKey);
    const existingAppend = this.appendInFlight.get(cacheKey);
    if (existingAppend) {
      return existingAppend as Promise<CrewDashboardPreviews[K]>;
    }

    const pending = this.appendOne(fleetId, previewKey)
      .then(preview => {
        if (preview) {
          this.cache.set(cacheKey, { preview, fetchedAt: Date.now() });
          this.persistCache();
        }
        return (preview ?? this.cache.get(cacheKey)?.preview ?? null) as CrewDashboardPreviews[K];
      })
      .finally(() => {
        this.appendInFlight.delete(cacheKey);
      });

    this.appendInFlight.set(cacheKey, pending);
    return pending;
  }

  peekCachedPreview<K extends FleetPreviewKey>(
    fleetId: number,
    previewKey: K
  ): CrewDashboardPreviews[K] | undefined {
    if (fleetId <= 0) {
      return undefined;
    }

    this.ensureHydrated();
    if (!this.cryptoSession.isUnlocked()) {
      return undefined;
    }
    const current = this.cache.get(this.getCacheKey(fleetId, previewKey));
    if (current) {
      return current.preview as CrewDashboardPreviews[K];
    }
    return undefined;
  }

  clearSessionCache(): void {
    this.cache.clear();
    this.inFlight.clear();
    this.appendInFlight.clear();
    this.forumLazy.clear();
    this.chatLazy.clear();
    this.giftLazy.clear();
    this.libraryLazy.clear();
    this.hydratedUserId = null;
    this.storage.remove(StorageScope.Persistent, FLEET_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY);
  }

  private clearLazyState(fleetId: number, previewKey: FleetPreviewKey): void {
    if (previewKey === 'forum') this.forumLazy.delete(fleetId);
    if (previewKey === 'chat') this.chatLazy.delete(fleetId);
    if (previewKey === 'gift') this.giftLazy.delete(fleetId);
    if (previewKey === 'library') this.libraryLazy.delete(fleetId);
  }

  private async appendOne(
    fleetId: number,
    previewKey: FleetPreviewKey
  ): Promise<CrewDashboardPreviews[FleetPreviewKey]> {
    const current = this.cache.get(this.getCacheKey(fleetId, previewKey))?.preview ?? null;
    if (previewKey === 'forum') {
      return this.appendForumItem(fleetId, current as ForumDashboardPreview | null);
    }
    if (previewKey === 'chat') {
      return this.appendChatItem(fleetId, current as ChatDashboardPreview | null);
    }
    if (previewKey === 'gift') {
      return this.appendGiftItem(fleetId, current as GiftDashboardPreview | null);
    }
    return this.appendLibraryItem(fleetId, current as LibraryDashboardPreview | null);
  }

  private async loadForumPreview(fleetId: number): Promise<ForumDashboardPreview | null> {
    this.forumLazy.set(fleetId, { offset: 0, exhausted: false });
    const items: ForumDashboardPreviewItem[] = [];
    while (items.length < INITIAL_PREVIEW_COUNT) {
      const next = await this.fetchNextForumItem(fleetId);
      if (!next) break;
      items.push(next);
    }
    if (!items.length) return null;
    const state = this.forumLazy.get(fleetId);
    return { kind: 'forum', items, hasMore: !(state?.exhausted ?? true) };
  }

  private async appendForumItem(
    fleetId: number,
    current: ForumDashboardPreview | null
  ): Promise<ForumDashboardPreview | null> {
    if (current && !current.hasMore) return current;
    const next = await this.fetchNextForumItem(fleetId);
    if (!next) {
      if (!current) return null;
      return { ...current, hasMore: false };
    }
    const items = [...(current?.items ?? []), next];
    const state = this.forumLazy.get(fleetId);
    return { kind: 'forum', items, hasMore: !(state?.exhausted ?? true) };
  }

  private async fetchNextForumItem(fleetId: number): Promise<ForumDashboardPreviewItem | null> {
    const state = this.forumLazy.get(fleetId) ?? { offset: 0, exhausted: false };
    if (state.exhausted) return null;

    const peeked = this.forumPrefetch.peekFleetSpacePage(fleetId)?.items ?? [];
    if (peeked.length && state.offset < peeked.length) {
      while (state.offset < peeked.length) {
        const candidate = peeked[state.offset++];
        this.forumLazy.set(fleetId, state);
        if (!this.adultContent.shouldShowEntry(candidate.isAdultContent)) continue;
        const preview = this.toForumItem(candidate);
        if (preview) return preview;
      }
    }

    if (!this.cryptoSession.isUnlocked()) {
      state.exhausted = true;
      this.forumLazy.set(fleetId, state);
      return null;
    }

    const response = await firstValueFrom(
      this.fleetService.getForums({ offset: state.offset, limit: 5 })
    );
    if (!response.success) {
      state.exhausted = true;
      this.forumLazy.set(fleetId, state);
      return null;
    }

    const rawItems = response.items ?? [];
    if (!rawItems.length) {
      state.exhausted = true;
      this.forumLazy.set(fleetId, state);
      return null;
    }

    for (const candidate of rawItems) {
      state.offset += 1;
      this.forumLazy.set(fleetId, state);
      if (!this.adultContent.shouldShowEntry(candidate.isAdultContent)) continue;
      const [item] = await this.proposalCrypto.decryptListItems(
        [candidate as unknown as ProposalListItem],
        { fleetId }
      ) as unknown as FleetForumListItem[];
      const preview = this.toForumItem(item);
      if (preview) return preview;
    }

    if (rawItems.length < 5 || !response.hasMore) {
      state.exhausted = true;
      this.forumLazy.set(fleetId, state);
      return null;
    }
    return this.fetchNextForumItem(fleetId);
  }

  private toForumItem(item: FleetForumListItem | null | undefined): ForumDashboardPreviewItem | null {
    if (!item) return null;
    if (item.hasEncryptedContent && !this.cryptoSession.isUnlocked()) return null;
    if (item.title === '[Encrypted]' || item.title === '[Unable to decrypt]') return null;

    return {
      id: item.id,
      authorUserId: item.authorUserId,
      authorUsername: item.authorUsername,
      authorAvatarResourceId: item.authorAvatarResourceId,
      lastActivityAt: item.lastActivityAt,
      title: item.title || 'Untitled post',
      body: truncateDashboardPreview(item.descriptionPreview ?? item.body),
      thumbnailUrl: item.thumbnailUrl ?? item.previewImageUrls?.[0] ?? null
    };
  }

  private async loadChatPreview(fleetId: number): Promise<ChatDashboardPreview | null> {
    if (!this.cryptoSession.isUnlocked()) return null;

    const roomsResponse = await firstValueFrom(this.fleetService.getChats());
    if (!roomsResponse.success) return null;

    const textRooms = (roomsResponse.items ?? [])
      .filter(room => room.roomType === 'Text')
      .filter(room => this.adultContent.shouldShowEntry(room.isAdultContent))
      .sort((a, b) => new Date(b.lastActivityAt).getTime() - new Date(a.lastActivityAt).getTime());

    let seed: ChatDashboardPreviewItem | null = null;
    let seedRoom: ChatRoomListItem | null = null;
    for (let index = 0; index < textRooms.length; index += CHAT_PREVIEW_ROOM_BATCH) {
      const batch = textRooms.slice(index, index + CHAT_PREVIEW_ROOM_BATCH);
      for (const room of batch) {
        const preview = await this.loadMessagePreview(fleetId, room, undefined);
        if (preview) {
          seed = preview;
          seedRoom = room;
          break;
        }
      }
      if (seed) break;
    }

    if (!seed || !seedRoom) return null;

    this.chatLazy.set(fleetId, {
      roomId: seedRoom.id,
      beforeMessageId: seed.messageId,
      exhausted: false
    });

    const items: ChatDashboardPreviewItem[] = [seed];
    while (items.length < INITIAL_PREVIEW_COUNT) {
      const older = await this.fetchOlderChatMessage(fleetId);
      if (!older) break;
      items.push(older);
    }

    const state = this.chatLazy.get(fleetId);
    return { kind: 'chat', items, hasMore: !(state?.exhausted ?? true) };
  }

  private async appendChatItem(
    fleetId: number,
    current: ChatDashboardPreview | null
  ): Promise<ChatDashboardPreview | null> {
    if (current && !current.hasMore) return current;
    if (!this.chatLazy.has(fleetId) && current?.items?.length) {
      const last = current.items[current.items.length - 1];
      this.chatLazy.set(fleetId, {
        roomId: last.roomId,
        beforeMessageId: last.messageId,
        exhausted: false
      });
    }
    const next = await this.fetchOlderChatMessage(fleetId);
    if (!next) {
      if (!current) return null;
      return { ...current, hasMore: false };
    }
    const items = [...(current?.items ?? []), next];
    const state = this.chatLazy.get(fleetId);
    return { kind: 'chat', items, hasMore: !(state?.exhausted ?? true) };
  }

  private async fetchOlderChatMessage(fleetId: number): Promise<ChatDashboardPreviewItem | null> {
    const state = this.chatLazy.get(fleetId);
    if (!state || state.exhausted || state.beforeMessageId == null) {
      if (state) {
        state.exhausted = true;
        this.chatLazy.set(fleetId, state);
      }
      return null;
    }

    const roomsResponse = await firstValueFrom(this.fleetService.getChats());
    const room = (roomsResponse.items ?? []).find(r => r.id === state.roomId);
    if (!room) {
      state.exhausted = true;
      this.chatLazy.set(fleetId, state);
      return null;
    }

    const preview = await this.loadMessagePreview(fleetId, room, state.beforeMessageId);
    if (!preview) {
      state.exhausted = true;
      this.chatLazy.set(fleetId, state);
      return null;
    }

    state.beforeMessageId = preview.messageId;
    this.chatLazy.set(fleetId, state);
    return preview;
  }

  private async loadMessagePreview(
    fleetId: number,
    room: ChatRoomListItem,
    beforeMessageId?: number
  ): Promise<ChatDashboardPreviewItem | null> {
    const response = await firstValueFrom(
      this.chatService.getMessages(room.id, 1, beforeMessageId)
    );
    if (!response.success) return null;
    const raw = response.items ?? [];
    if (!raw.length) return null;

    const messages = await this.chatCrypto.decryptMessages(raw, { fleetId });
    const message = messages[messages.length - 1] as ChatMessage | undefined;
    if (!message) return null;

    if (message.isRemovedByModerator) {
      return {
        roomId: room.id,
        messageId: message.id,
        authorUserId: message.authorUserId,
        authorUsername: message.isAnonymous ? 'Anonymous' : (message.authorUsername || 'Unknown'),
        authorAvatarResourceId: message.isAnonymous ? null : message.authorAvatarResourceId,
        createdAt: message.createdAt,
        body: 'Post removed by a moderator',
        hasAttachments: false,
        isAnonymous: !!message.isAnonymous
      };
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
    if (!body) return null;

    return {
      roomId: room.id,
      messageId: message.id,
      authorUserId: message.authorUserId,
      authorUsername: message.isAnonymous ? 'Anonymous' : (message.authorUsername || 'Unknown'),
      authorAvatarResourceId: message.isAnonymous ? null : message.authorAvatarResourceId,
      createdAt: message.createdAt,
      body,
      hasAttachments,
      isAnonymous: !!message.isAnonymous
    };
  }

  private async loadGiftPreview(fleetId: number): Promise<GiftDashboardPreview | null> {
    if (!this.cryptoSession.isUnlocked()) return null;

    this.giftLazy.set(fleetId, { beforeId: null, beforeCreatedAt: null, exhausted: false });
    const items: GiftDashboardPreviewItem[] = [];
    while (items.length < INITIAL_PREVIEW_COUNT) {
      const next = await this.fetchNextGiftItem(fleetId);
      if (!next) break;
      items.push(next);
    }
    if (!items.length) return null;
    const state = this.giftLazy.get(fleetId);
    return { kind: 'gift', items, hasMore: !(state?.exhausted ?? true) };
  }

  private async appendGiftItem(
    fleetId: number,
    current: GiftDashboardPreview | null
  ): Promise<GiftDashboardPreview | null> {
    if (current && !current.hasMore) return current;
    if (!this.giftLazy.has(fleetId) && current?.items?.length) {
      const last = current.items[current.items.length - 1];
      this.giftLazy.set(fleetId, {
        beforeId: last.id,
        beforeCreatedAt: last.timestamp instanceof Date
          ? last.timestamp.toISOString()
          : String(last.timestamp),
        exhausted: false
      });
    }
    const next = await this.fetchNextGiftItem(fleetId);
    if (!next) {
      if (!current) return null;
      return { ...current, hasMore: false };
    }
    const items = [...(current?.items ?? []), next];
    const state = this.giftLazy.get(fleetId);
    return { kind: 'gift', items, hasMore: !(state?.exhausted ?? true) };
  }

  private async fetchNextGiftItem(fleetId: number): Promise<GiftDashboardPreviewItem | null> {
    const state = this.giftLazy.get(fleetId) ?? {
      beforeId: null,
      beforeCreatedAt: null,
      exhausted: false
    };
    if (state.exhausted) return null;

    const result = await firstValueFrom(
      this.fleetService.getGiftLog({
        limit: 1,
        beforeId: state.beforeId ?? undefined,
        beforeCreatedAt: state.beforeCreatedAt ?? undefined
      })
    );
    if (!result.success) {
      state.exhausted = true;
      this.giftLazy.set(fleetId, state);
      return null;
    }

    const raw = (result.items ?? []).map(entry => ({
      ...entry,
      timestamp: entry.timestamp instanceof Date ? entry.timestamp : new Date(entry.timestamp)
    }));
    if (!raw.length) {
      state.exhausted = true;
      this.giftLazy.set(fleetId, state);
      return null;
    }

    const entry = (await this.decryptGiftEntries(raw))[0];
    if (!entry) {
      state.exhausted = true;
      this.giftLazy.set(fleetId, state);
      return null;
    }

    state.beforeId = entry.id;
    state.beforeCreatedAt = entry.timestamp instanceof Date
      ? entry.timestamp.toISOString()
      : String(entry.timestamp);
    this.giftLazy.set(fleetId, state);

    if (entry.hasEncryptedContent && (
      !entry.message
      || entry.message.startsWith('[Unable to decrypt')
      || entry.message.startsWith('[Encrypted')
    )) {
      return this.fetchNextGiftItem(fleetId);
    }

    const message = truncateDashboardPreview(entry.message);
    if (!message) return this.fetchNextGiftItem(fleetId);

    return {
      id: entry.id,
      message,
      categoryLabel: giftCategoryLabel(entry.customGiftCategory),
      timestamp: entry.timestamp,
      unverified: entry.displayFlag === 'unverified' || entry.status === 'unverified'
    };
  }

  private async decryptGiftEntries(entries: GiftLogEntry[]): Promise<GiftLogEntry[]> {
    const membership = await firstValueFrom(this.crewService.getMembership());
    const crewId = membership.crewId ?? 0;
    if (crewId <= 0) return entries;

    const homeCrewItems = entries.filter(entry => !entry.crewId || entry.crewId === crewId);
    const otherCrewItems = entries.filter(entry => entry.crewId && entry.crewId !== crewId);
    const decryptedHome = await this.giftLogCrypto.decryptEntries(homeCrewItems, crewId);
    if (!otherCrewItems.length) return decryptedHome;

    const byId = new Map<number, GiftLogEntry>();
    for (const entry of decryptedHome) byId.set(entry.id, entry);
    for (const entry of otherCrewItems) byId.set(entry.id, entry);
    return entries.map(entry => byId.get(entry.id) ?? entry);
  }

  private async loadLibraryPreview(fleetId: number): Promise<LibraryDashboardPreview | null> {
    const emptyPage = { items: [] as never[], hasMore: false };
    const [durable, consumable, services] = await Promise.all([
      firstValueFrom(
        this.fleetService.getLibraryDurableUnits({ limit: LIBRARY_UNIT_SAMPLE_LIMIT, offset: 0 }).pipe(
          catchError(() => of(emptyPage))
        )
      ),
      firstValueFrom(
        this.fleetService.getLibraryStockUnits('Consumable', { limit: LIBRARY_UNIT_SAMPLE_LIMIT, offset: 0 }).pipe(
          catchError(() => of(emptyPage))
        )
      ),
      firstValueFrom(
        this.fleetService.getLibraryStockUnits('Service', { limit: LIBRARY_UNIT_SAMPLE_LIMIT, offset: 0 }).pipe(
          catchError(() => of(emptyPage))
        )
      )
    ]);

    const rawUnits = [
      ...(durable.items ?? []),
      ...(consumable.items ?? []),
      ...(services.items ?? [])
    ];

    const candidates: LibraryPreviewCandidate[] = shuffleArray(
      rawUnits.map(unit => ({ type: 'unit' as const, unit }))
        .filter(isLibraryPreviewCandidateEligible)
    );

    this.libraryLazy.set(fleetId, {
      pool: candidates,
      cursor: 0,
      exhausted: candidates.length === 0
    });

    const items: LibraryDashboardPreviewItem[] = [];
    while (items.length < INITIAL_PREVIEW_COUNT) {
      const next = await this.fetchNextLibraryItem(fleetId);
      if (!next) break;
      items.push(next);
    }
    if (!items.length) return null;
    const state = this.libraryLazy.get(fleetId);
    return { kind: 'library', items, hasMore: !(state?.exhausted ?? true) };
  }

  private async appendLibraryItem(
    fleetId: number,
    current: LibraryDashboardPreview | null
  ): Promise<LibraryDashboardPreview | null> {
    if (current && !current.hasMore) return current;
    const next = await this.fetchNextLibraryItem(fleetId);
    if (!next) {
      if (!current) return null;
      return { ...current, hasMore: false };
    }
    const items = [...(current?.items ?? []), next];
    const state = this.libraryLazy.get(fleetId);
    return { kind: 'library', items, hasMore: !(state?.exhausted ?? true) };
  }

  private async fetchNextLibraryItem(fleetId: number): Promise<LibraryDashboardPreviewItem | null> {
    const state = this.libraryLazy.get(fleetId);
    if (!state || state.exhausted) return null;

    while (state.cursor < state.pool.length) {
      const batch = state.pool.slice(state.cursor, state.cursor + 1);
      state.cursor += 1;
      this.libraryLazy.set(fleetId, state);
      const resolved = await this.resolveLibraryPreviewItems(batch);
      if (resolved[0]) {
        if (state.cursor >= state.pool.length) {
          state.exhausted = true;
          this.libraryLazy.set(fleetId, state);
        }
        return resolved[0];
      }
    }

    state.exhausted = true;
    this.libraryLazy.set(fleetId, state);
    return null;
  }

  private async resolveLibraryPreviewItems(
    picked: LibraryPreviewCandidate[]
  ): Promise<LibraryDashboardPreviewItem[]> {
    const membership = await firstValueFrom(this.crewService.getMembership());
    const crewId = membership.crewId ?? 0;
    const pickedUnits = picked
      .filter((candidate): candidate is Extract<LibraryPreviewCandidate, { type: 'unit' }> => candidate.type === 'unit')
      .map(candidate => candidate.unit);

    // Only decrypt offerings for the viewer's crew; other crews stay as list plaintext when available.
    const ownCrewItems = crewId > 0
      ? pickedUnits.filter(item => !item.crewId || item.crewId === crewId)
      : [];
    const enriched = crewId > 0 && ownCrewItems.length
      ? await this.libraryCrypto.enrichUnitListItems(ownCrewItems, crewId)
      : [];
    const unitById = new Map(enriched.map(unit => [unit.unitId, unit]));

    const items: LibraryDashboardPreviewItem[] = [];
    for (const candidate of picked) {
      if (candidate.type !== 'unit') continue;
      const unit = unitById.get(candidate.unit.unitId) ?? candidate.unit;
      if (!hasUsableLibraryPreviewTitle(unit.title)) continue;
      items.push({
        libraryKind: 'unit',
        unit: {
          ...unit,
          descriptionPreview: truncateDashboardPreview(unit.descriptionPreview)
        }
      });
    }
    return items;
  }

  private fetchPreview<K extends FleetPreviewKey>(
    fleetId: number,
    previewKey: K
  ): Promise<CrewDashboardPreviews[K]> {
    const loaders: Record<FleetPreviewKey, () => Promise<CrewDashboardPreviews[FleetPreviewKey]>> = {
      forum: () => this.loadForumPreview(fleetId),
      chat: () => this.loadChatPreview(fleetId),
      gift: () => this.loadGiftPreview(fleetId),
      library: () => this.loadLibraryPreview(fleetId)
    };
    return loaders[previewKey]().catch(() => null) as Promise<CrewDashboardPreviews[K]>;
  }

  private getCacheKey(fleetId: number, previewKey: FleetPreviewKey, unlocked = this.cryptoSession.isUnlocked()): string {
    const adultPreference = this.adultContent.preference;
    return `${fleetId}:${previewKey}:${unlocked ? 'unlocked' : 'locked'}:${adultPreference}`;
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
    if (this.hydratedUserId === userId) return;

    this.hydratedUserId = userId;
    this.cache = this.readPersistedCache(userId);
  }

  private persistCache(): void {
    const userId = this.readCurrentUserId();
    if (userId == null) return;

    const payload: PersistedPreviewCache = {
      userId,
      entries: Array.from(this.cache.entries())
    };
    this.storage.set(
      StorageScope.Persistent,
      FLEET_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY,
      JSON.stringify(payload)
    );
  }

  private readPersistedCache(userId: number): Map<string, PreviewCacheEntry> {
    const raw = this.storage.get(StorageScope.Persistent, FLEET_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY);
    if (!raw) return new Map();

    try {
      const parsed = JSON.parse(raw) as PersistedPreviewCache;
      if (parsed.userId !== userId || !Array.isArray(parsed.entries)) return new Map();
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
