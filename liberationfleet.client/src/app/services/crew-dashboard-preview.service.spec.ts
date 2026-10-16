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
  let adultContent: jasmine.SpyObj<AdultContentService>;

  beforeEach(() => {
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
    adultContent = jasmine.createSpyObj('AdultContentService', ['shouldShowEntry']);
    adultContent.shouldShowEntry.and.callFake((isAdult?: boolean) => !isAdult);

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
    expect(result.library?.libraryKind).toBe('quest');
    if (result.library?.libraryKind === 'quest') {
      expect(result.library.task.taskId).toBe(44);
    }
  });

  it('omits chat preview when crypto is locked', async () => {
    cryptoSession.isUnlocked.and.returnValue(false);
    const result = await service.loadPreviews(7);
    expect(result.chat).toBeNull();
    expect(result.gift).toBeNull();
  });
});
