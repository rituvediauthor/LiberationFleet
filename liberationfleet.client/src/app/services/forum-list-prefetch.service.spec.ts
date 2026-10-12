import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ForumListPrefetchService } from './forum-list-prefetch.service';
import { CrewDiscussionService } from './crew-discussion.service';
import { FleetService } from './fleet.service';
import { ProposalCryptoService } from './crypto/proposal-crypto.service';
import { CryptoSessionService } from './crypto/crypto-session.service';
import { DiscussionListItem } from '../models/crew-discussion.model';

describe('ForumListPrefetchService', () => {
  let service: ForumListPrefetchService;
  let discussionService: jasmine.SpyObj<CrewDiscussionService>;
  let proposalCrypto: jasmine.SpyObj<ProposalCryptoService>;
  let cryptoSession: jasmine.SpyObj<CryptoSessionService>;
  let decryptGate: Promise<DiscussionListItem[]> | null = null;
  let resolveDecryptGate: ((value: DiscussionListItem[]) => void) | null = null;

  const sampleItem = { id: 1, title: 'Secret post from crew A' } as DiscussionListItem;

  beforeEach(() => {
    decryptGate = null;
    resolveDecryptGate = null;
    discussionService = jasmine.createSpyObj<CrewDiscussionService>('CrewDiscussionService', ['getPosts']);
    proposalCrypto = jasmine.createSpyObj<ProposalCryptoService>('ProposalCryptoService', ['decryptListItems']);
    cryptoSession = jasmine.createSpyObj<CryptoSessionService>('CryptoSessionService', ['isUnlocked']);
    cryptoSession.isUnlocked.and.returnValue(true);
    discussionService.getPosts.and.returnValue(of({ items: [sampleItem], hasMore: false }));
    proposalCrypto.decryptListItems.and.callFake(async () => {
      if (decryptGate) {
        return decryptGate as never;
      }
      return [sampleItem] as never;
    });

    TestBed.configureTestingModule({
      providers: [
        ForumListPrefetchService,
        { provide: CrewDiscussionService, useValue: discussionService },
        { provide: FleetService, useValue: jasmine.createSpyObj('FleetService', ['getForums']) },
        { provide: ProposalCryptoService, useValue: proposalCrypto },
        { provide: CryptoSessionService, useValue: cryptoSession }
      ]
    });

    service = TestBed.inject(ForumListPrefetchService);
  });

  it('should return prefetched page only for the matching crew', async () => {
    service.prefetchCrewSpace(10);
    await settle();

    const page = service.takeCrewSpacePage(10);
    expect(page).not.toBeNull();
    expect(page!.scopeId).toBe(10);
    expect(page!.items[0].title).toBe('Secret post from crew A');
    expect(service.takeCrewSpacePage(10)).toBeNull();
  });

  it('should not hand another crew the prefetched plaintext page', async () => {
    service.prefetchCrewSpace(10);
    await settle();

    expect(service.takeCrewSpacePage(99)).toBeNull();
    // Mismatched take must wipe the cached plaintext
    expect(service.takeCrewSpacePage(10)).toBeNull();
  });

  it('clearSessionCache should drop plaintext so a later account cannot see it', async () => {
    service.prefetchCrewSpace(10);
    await settle();

    service.clearSessionCache();

    expect(service.takeCrewSpacePage(10)).toBeNull();
  });

  it('invalidate should prevent in-flight prefetch from re-seeding after logout', async () => {
    decryptGate = new Promise<DiscussionListItem[]>(resolve => {
      resolveDecryptGate = resolve;
    });

    service.prefetchCrewSpace(10);
    await settle();

    service.invalidate();
    resolveDecryptGate!([sampleItem]);
    await settle();

    expect(service.takeCrewSpacePage(10)).toBeNull();
  });

  async function settle(): Promise<void> {
    for (let i = 0; i < 25; i++) {
      await Promise.resolve();
    }
  }
});
