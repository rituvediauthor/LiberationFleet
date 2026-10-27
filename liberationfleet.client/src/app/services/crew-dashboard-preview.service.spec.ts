import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CrewDashboardPreviewService } from './crew-dashboard-preview.service';
import { CrewDiscussionService } from './crew-discussion.service';
import { ForumListPrefetchService } from './forum-list-prefetch.service';
import { ProposalCryptoService } from './crypto/proposal-crypto.service';
import { ChatService } from './chat.service';
import { ChatCryptoService } from './crypto/chat-crypto.service';
import { GiftService } from './gift.service';
import { GiftLogCryptoService } from './crypto/gift-log-crypto.service';
import { LibraryService } from './library.service';
import { LibraryCryptoService } from './crypto/library-crypto.service';
import { CryptoSessionService } from './crypto/crypto-session.service';
import { AdultContentService } from './adult-content.service';
import { AUTH_TOKEN_STORAGE_KEY, CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY } from './storage/storage-keys';

function testJwt(userId = 1): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const payload = btoa(JSON.stringify({
    sub: String(userId),
    exp: Math.floor(Date.now() / 1000) + 3600
  }));
  return `${header}.${payload}.sig`;
}

describe('CrewDashboardPreviewService', () => {
  let service: CrewDashboardPreviewService;
  let forumPrefetch: jasmine.SpyObj<ForumListPrefetchService>;
  let discussionService: jasmine.SpyObj<CrewDiscussionService>;
  let proposalCrypto: jasmine.SpyObj<ProposalCryptoService>;
  let chatService: jasmine.SpyObj<ChatService>;
  let chatCrypto: jasmine.SpyObj<ChatCryptoService>;
  let giftService: jasmine.SpyObj<GiftService>;
  let giftLogCrypto: jasmine.SpyObj<GiftLogCryptoService>;
  let libraryService: jasmine.SpyObj<LibraryService>;
  let libraryCrypto: jasmine.SpyObj<LibraryCryptoService>;
  let cryptoSession: { isUnlocked: jasmine.Spy };
  let adultContent: AdultContentService & { shouldShowEntry: jasmine.Spy };

  beforeEach(() => {
    localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY);
    localStorage.removeItem(CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY);
    sessionStorage.removeItem(AUTH_TOKEN_STORAGE_KEY);
    forumPrefetch = jasmine.createSpyObj('ForumListPrefetchService', ['peekCrewSpacePage']);
    discussionService = jasmine.createSpyObj('CrewDiscussionService', ['getPosts']);
    proposalCrypto = jasmine.createSpyObj('ProposalCryptoService', ['decryptListItems']);
    chatService = jasmine.createSpyObj('ChatService', ['getRooms', 'getMessages']);
    chatCrypto = jasmine.createSpyObj('ChatCryptoService', ['decryptRooms', 'decryptMessages']);
    giftService = jasmine.createSpyObj('GiftService', ['getLogs']);
    giftLogCrypto = jasmine.createSpyObj('GiftLogCryptoService', ['decryptEntries']);
    libraryService = jasmine.createSpyObj('LibraryService', [
      'getDurableUnits',
      'getConsumableUnits',
      'getServicesUnits',
      'getDigitalUnits',
      'getTasks',
      'getNoDeadlineTasks'
    ]);
    libraryCrypto = jasmine.createSpyObj('LibraryCryptoService', [
      'enrichUnitListItems',
      'enrichTaskListItems'
    ]);
    cryptoSession = { isUnlocked: jasmine.createSpy('isUnlocked').and.returnValue(true) };
    adultContent = {
      get preference() {
        return 'Block';
      },
      shouldShowEntry: jasmine.createSpy('shouldShowEntry').and.callFake((isAdult?: boolean) => !isAdult)
    } as AdultContentService & { shouldShowEntry: jasmine.Spy };

    forumPrefetch.peekCrewSpacePage.and.returnValue(null);
    discussionService.getPosts.and.returnValue(of({ items: [], hasMore: false }));
    proposalCrypto.decryptListItems.and.callFake(async items => items as never);
    chatService.getRooms.and.returnValue(of({ success: true, message: '', items: [] }));
    chatCrypto.decryptRooms.and.callFake(async rooms => rooms);
    chatService.getMessages.and.returnValue(of({ success: true, message: '', items: [], hasMore: false }));
    chatCrypto.decryptMessages.and.callFake(async messages => messages);
    giftService.getLogs.and.returnValue(of({ items: [], hasMore: false }));
    giftLogCrypto.decryptEntries.and.callFake(async entries => entries);
    libraryService.getDurableUnits.and.returnValue(of({ items: [], hasMore: false }));
    libraryService.getConsumableUnits.and.returnValue(of({ items: [], hasMore: false }));
    libraryService.getServicesUnits.and.returnValue(of({ items: [], hasMore: false }));
    libraryService.getDigitalUnits.and.returnValue(of({ items: [], hasMore: false }));
    libraryService.getTasks.and.returnValue(of([]));
    libraryService.getNoDeadlineTasks.and.returnValue(of([]));
    libraryCrypto.enrichUnitListItems.and.callFake(async items => items);
    libraryCrypto.enrichTaskListItems.and.callFake(async items => items);

    TestBed.configureTestingModule({
      providers: [
        CrewDashboardPreviewService,
        { provide: ForumListPrefetchService, useValue: forumPrefetch },
        { provide: CrewDiscussionService, useValue: discussionService },
        { provide: ProposalCryptoService, useValue: proposalCrypto },
        { provide: ChatService, useValue: chatService },
        { provide: ChatCryptoService, useValue: chatCrypto },
        { provide: GiftService, useValue: giftService },
        { provide: GiftLogCryptoService, useValue: giftLogCrypto },
        { provide: LibraryService, useValue: libraryService },
        { provide: LibraryCryptoService, useValue: libraryCrypto },
        { provide: CryptoSessionService, useValue: cryptoSession },
        { provide: AdultContentService, useValue: adultContent }
      ]
    });

    service = TestBed.inject(CrewDashboardPreviewService);
  });

  afterEach(() => {
    localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY);
    localStorage.removeItem(CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY);
    sessionStorage.removeItem(AUTH_TOKEN_STORAGE_KEY);
  });

  function simulateAppRestart(): void {
    const internals = service as unknown as {
      cache: Map<string, unknown>;
      hydratedUserId: number | null;
    };
    internals.cache = new Map();
    internals.hydratedUserId = null;
  }

  it('returns empty previews when crewId is invalid', async () => {
    await expectAsync(service.loadPreviews(0)).toBeResolvedTo({
      forum: null,
      chat: null,
      gift: null,
      library: null
    });
  });

  it('skips adult forum posts when preference blocks them', async () => {
    forumPrefetch.peekCrewSpacePage.and.returnValue({
      scopeId: 7,
      items: [
        {
          id: 1,
          authorUserId: 1,
          authorUsername: 'AdultAuthor',
          lastActivityAt: new Date(),
          title: 'Adult',
          descriptionPreview: 'nsfw',
          isAdultContent: true
        },
        {
          id: 2,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Safe',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false,
      fetchedAt: Date.now()
    });

    const result = await service.loadPreviews(7);
    expect(result.forum?.id).toBe(2);
    expect(result.forum?.title).toBe('Safe');
  });

  it('picks a library quest from the combined pool when units are empty', async () => {
    libraryService.getTasks.and.returnValue(of([
      {
        taskId: 44,
        title: 'Mow lawn',
        creatorUsername: 'Kai',
        creatorUserId: 3,
        value: 12,
        scheduleSummary: 'Soon',
        hasDeadline: true,
        nextDueAt: '2026-03-01T00:00:00Z'
      }
    ]));

    const result = await service.loadPreviews(7);
    expect(result.library?.kind).toBe('library');
    const item = result.library?.items[0];
    expect(item?.libraryKind).toBe('quest');
    if (item?.libraryKind === 'quest') {
      expect(item.task.taskId).toBe(44);
    }
  });

  it('omits chat preview when crypto is locked', async () => {
    cryptoSession.isUnlocked.and.returnValue(false);
    const result = await service.loadPreviews(7);
    expect(result.chat).toBeNull();
    expect(result.gift).toBeNull();
  });

  it('reuses cached previews for repeated calls with the same session state', async () => {
    discussionService.getPosts.and.returnValue(of({
      items: [
        {
          id: 12,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Safe',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false
    }));

    const first = await service.loadPreviews(7);
    const second = await service.loadPreviews(7);

    expect(first).toEqual(second);
    expect(discussionService.getPosts).toHaveBeenCalledTimes(1);
  });

  it('force-refreshes all preview tiles even when the cache is still fresh', async () => {
    discussionService.getPosts.and.returnValue(of({
      items: [
        {
          id: 12,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Safe',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false
    }));

    await service.loadPreviews(7);
    await service.loadPreviews(7, { force: true });

    expect(discussionService.getPosts).toHaveBeenCalledTimes(2);
  });

  it('force-refreshes only the requested preview tile', async () => {
    discussionService.getPosts.and.returnValue(of({
      items: [
        {
          id: 14,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Forum',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false
    }));

    await service.loadPreview(7, 'forum');
    await service.loadPreview(7, 'forum', { force: true });

    expect(discussionService.getPosts).toHaveBeenCalledTimes(2);
    expect(chatService.getRooms).not.toHaveBeenCalled();
  });

  it('clears the preview cache when the session cache is reset', async () => {
    discussionService.getPosts.and.returnValue(of({
      items: [
        {
          id: 13,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Fresh',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false
    }));

    await service.loadPreviews(7);
    service.clearSessionCache();
    await service.loadPreviews(7);

    expect(discussionService.getPosts).toHaveBeenCalledTimes(2);
  });

  it('keeps decrypted preview chrome while encryption is locked', async () => {
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, testJwt(1));
    discussionService.getPosts.and.returnValue(of({
      items: [
        {
          id: 12,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Safe',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false
    }));

    await service.loadPreview(7, 'forum');
    expect(discussionService.getPosts).toHaveBeenCalledTimes(1);

    cryptoSession.isUnlocked.and.returnValue(false);
    discussionService.getPosts.calls.reset();

    expect(service.peekCachedPreview(7, 'forum')?.title).toBe('Safe');
    await service.loadPreview(7, 'forum', { force: true });
    expect(discussionService.getPosts).not.toHaveBeenCalled();
  });

  it('persists previews across a new session for the same user', async () => {
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, testJwt(1));
    discussionService.getPosts.and.returnValue(of({
      items: [
        {
          id: 12,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Safe',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false
    }));

    await service.loadPreviews(7);
    expect(discussionService.getPosts).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem(CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY)).toBeTruthy();

    simulateAppRestart();
    discussionService.getPosts.calls.reset();

    const restored = await service.loadPreviews(7);
    expect(restored.forum?.title).toBe('Safe');
    expect(discussionService.getPosts).not.toHaveBeenCalled();
    expect(service.peekCachedPreview(7, 'forum')?.title).toBe('Safe');
  });

  it('does not restore another user\'s persisted preview cache', async () => {
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, testJwt(1));
    discussionService.getPosts.and.returnValue(of({
      items: [
        {
          id: 12,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Safe',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false
    }));

    await service.loadPreviews(7);
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, testJwt(2));
    simulateAppRestart();
    discussionService.getPosts.calls.reset();

    await service.loadPreviews(7);
    expect(discussionService.getPosts).toHaveBeenCalledTimes(1);
  });

  it('clears persisted previews when the session cache is reset', async () => {
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, testJwt(1));
    discussionService.getPosts.and.returnValue(of({
      items: [
        {
          id: 13,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Fresh',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false
    }));

    await service.loadPreviews(7);
    expect(localStorage.getItem(CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY)).toBeTruthy();

    service.clearSessionCache();
    expect(localStorage.getItem(CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY)).toBeNull();
  });

  it('returns up to five library carousel items from the combined candidate pool', async () => {
    libraryService.getDurableUnits.and.returnValue(of({
      items: [
        {
          unitId: 1,
          offeringId: 1,
          holderUserId: 1,
          holderUsername: 'Ada',
          title: 'Tent',
          descriptionPreview: 'Large',
          categories: []
        },
        {
          unitId: 2,
          offeringId: 1,
          holderUserId: 1,
          holderUsername: 'Ada',
          title: 'Stove',
          descriptionPreview: 'Portable',
          categories: []
        },
        {
          unitId: 3,
          offeringId: 1,
          holderUserId: 1,
          holderUsername: 'Ada',
          title: 'Lamp',
          descriptionPreview: 'Bright',
          categories: []
        }
      ],
      hasMore: false
    }));
    libraryService.getTasks.and.returnValue(of([
      {
        taskId: 44,
        title: 'Mow lawn',
        creatorUsername: 'Kai',
        creatorUserId: 3,
        value: 12,
        scheduleSummary: 'Soon',
        hasDeadline: true,
        nextDueAt: '2026-03-01T00:00:00Z'
      },
      {
        taskId: 45,
        title: 'Paint',
        creatorUsername: 'Kai',
        creatorUserId: 3,
        value: 18,
        scheduleSummary: 'Later',
        hasDeadline: false
      },
      {
        taskId: 46,
        title: 'Errands',
        creatorUsername: 'Kai',
        creatorUserId: 3,
        value: 8,
        scheduleSummary: 'Today',
        hasDeadline: false
      }
    ]));

    const result = await service.loadPreview(7, 'library');
    expect(result?.items.length).toBe(5);
    expect(libraryCrypto.enrichUnitListItems.calls.mostRecent().args[0].length).toBeLessThanOrEqual(5);
    expect(libraryCrypto.enrichTaskListItems.calls.mostRecent().args[0].length).toBeLessThanOrEqual(5);
  });

  it('decrypts only the first allowed forum post', async () => {
    discussionService.getPosts.and.returnValue(of({
      items: [
        {
          id: 1,
          authorUserId: 1,
          authorUsername: 'AdultAuthor',
          lastActivityAt: new Date(),
          title: 'Adult',
          descriptionPreview: 'nsfw',
          isAdultContent: true
        },
        {
          id: 2,
          authorUserId: 2,
          authorUsername: 'SafeAuthor',
          lastActivityAt: new Date(),
          title: 'Safe',
          descriptionPreview: 'hello',
          isAdultContent: false
        }
      ],
      hasMore: false
    }));

    const result = await service.loadPreview(7, 'forum');

    expect(result?.id).toBe(2);
    expect(proposalCrypto.decryptListItems).toHaveBeenCalledTimes(1);
    expect(proposalCrypto.decryptListItems.calls.mostRecent().args[0].length).toBe(1);
    expect(proposalCrypto.decryptListItems.calls.mostRecent().args[0][0].id).toBe(2);
  });

  it('loads the latest chat message without decrypting room names', async () => {
    chatService.getRooms.and.returnValue(of({
      success: true,
      message: '',
      items: [
        {
          id: 1,
          name: 'Old',
          roomType: 'Text',
          createdByUserId: 1,
          createdByUsername: 'Ada',
          createdAt: '2026-01-01T00:00:00Z',
          lastActivityAt: '2026-01-01T00:00:00Z'
        },
        {
          id: 2,
          name: 'Voice',
          roomType: 'Voice',
          createdByUserId: 1,
          createdByUsername: 'Ada',
          createdAt: '2026-03-01T00:00:00Z',
          lastActivityAt: '2026-03-02T00:00:00Z'
        },
        {
          id: 3,
          name: 'Fresh',
          roomType: 'Text',
          createdByUserId: 1,
          createdByUsername: 'Ada',
          createdAt: '2026-02-01T00:00:00Z',
          lastActivityAt: '2026-03-01T00:00:00Z'
        }
      ]
    }));
    chatService.getMessages.and.callFake((roomId: number) => of({
      success: true,
      message: '',
      items: roomId === 3
        ? [{
            id: 99,
            authorUserId: 4,
            authorUsername: 'Bea',
            createdAt: '2026-03-01T00:00:00Z',
            hasEncryptedContent: false,
            body: 'Latest hello'
          }]
        : [],
      hasMore: false
    }));

    const result = await service.loadPreview(7, 'chat');

    expect(result?.messageId).toBe(99);
    expect(result?.body).toBe('Latest hello');
    expect(chatCrypto.decryptRooms).not.toHaveBeenCalled();
    expect(chatService.getMessages).toHaveBeenCalled();
    expect(chatService.getMessages.calls.count()).toBeLessThanOrEqual(3);
  });
});
