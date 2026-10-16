import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FORUM_DISCUSSION_CONFIG } from '../config/discussion.config';
import { DiscussionListItem } from '../models/crew-discussion.model';
import { FleetForumListItem } from '../models/fleet-forum.model';
import { ProposalListItem } from '../models/proposal.model';
import { CrewDiscussionService } from './crew-discussion.service';
import { FleetService } from './fleet.service';
import { ProposalCryptoService } from './crypto/proposal-crypto.service';
import { CryptoSessionService } from './crypto/crypto-session.service';

interface PrefetchPage<T> {
  /** Crew or fleet id that owns these decrypted items — must match before consume. */
  scopeId: number;
  items: T[];
  hasMore: boolean;
  fetchedAt: number;
}

const PREFETCH_TTL_MS = 90_000;
const PREFETCH_LIMIT = 20;

/**
 * Eagerly warms the first page of crew/fleet space lists from hub screens
 * so opening those routes feels instantaneous when crypto is unlocked.
 *
 * Holds decrypted plaintext — must be cleared on logout / account switch.
 */
@Injectable({
  providedIn: 'root'
})
export class ForumListPrefetchService {
  private crewPage: PrefetchPage<DiscussionListItem> | null = null;
  private fleetPage: PrefetchPage<FleetForumListItem> | null = null;
  private crewInFlight: Promise<void> | null = null;
  private fleetInFlight: Promise<void> | null = null;
  /** Bumped on invalidate so in-flight prefetches cannot re-seed after logout. */
  private generation = 0;

  private discussionService = inject(CrewDiscussionService);
  private fleetService = inject(FleetService);
  private proposalCrypto = inject(ProposalCryptoService);
  private cryptoSession = inject(CryptoSessionService);

  prefetchCrewSpace(crewId: number): void {
    if (crewId <= 0 || !this.cryptoSession.isUnlocked()) {
      return;
    }
    if (this.isFreshForScope(this.crewPage, crewId) || this.crewInFlight) {
      return;
    }

    const generation = this.generation;
    this.crewInFlight = (async () => {
      try {
        const page = await firstValueFrom(
          this.discussionService.getPosts(FORUM_DISCUSSION_CONFIG, { offset: 0, limit: PREFETCH_LIMIT })
        );
        if (generation !== this.generation) {
          return;
        }
        const items = await this.proposalCrypto.decryptListItems(
          page.items as ProposalListItem[],
          crewId
        ) as DiscussionListItem[];
        if (generation !== this.generation) {
          return;
        }
        this.crewPage = { scopeId: crewId, items, hasMore: page.hasMore, fetchedAt: Date.now() };
      } catch {
        // Prefetch is best-effort.
      } finally {
        if (generation === this.generation) {
          this.crewInFlight = null;
        }
      }
    })();
  }

  prefetchFleetSpace(fleetId: number): void {
    if (fleetId <= 0 || !this.cryptoSession.isUnlocked()) {
      return;
    }
    if (this.isFreshForScope(this.fleetPage, fleetId) || this.fleetInFlight) {
      return;
    }

    const generation = this.generation;
    this.fleetInFlight = (async () => {
      try {
        const response = await firstValueFrom(
          this.fleetService.getForums({ offset: 0, limit: PREFETCH_LIMIT })
        );
        if (generation !== this.generation || !response.success) {
          return;
        }
        const rawItems = response.items ?? [];
        const items = await this.proposalCrypto.decryptListItems(
          rawItems as unknown as ProposalListItem[],
          { fleetId }
        ) as unknown as FleetForumListItem[];
        if (generation !== this.generation) {
          return;
        }
        this.fleetPage = {
          scopeId: fleetId,
          items,
          hasMore: !!response.hasMore,
          fetchedAt: Date.now()
        };
      } catch {
        // Prefetch is best-effort.
      } finally {
        if (generation === this.generation) {
          this.fleetInFlight = null;
        }
      }
    })();
  }

  /**
   * Peek a fresh crew space prefetch without consuming it (dashboard previews).
   */
  peekCrewSpacePage(crewId: number): PrefetchPage<DiscussionListItem> | null {
    if (crewId <= 0 || !this.isFreshForScope(this.crewPage, crewId)) {
      if (this.crewPage && this.crewPage.scopeId !== crewId) {
        this.crewPage = null;
      }
      return null;
    }
    return this.crewPage;
  }

  /**
   * Consume a fresh crew space prefetch for this crew only.
   * Clears the cache so the list owns the data.
   */
  takeCrewSpacePage(crewId: number): PrefetchPage<DiscussionListItem> | null {
    if (crewId <= 0 || !this.isFreshForScope(this.crewPage, crewId)) {
      // Drop mismatched / stale plaintext so it cannot leak on a later take.
      if (this.crewPage && this.crewPage.scopeId !== crewId) {
        this.crewPage = null;
      }
      return null;
    }
    const page = this.crewPage;
    this.crewPage = null;
    return page;
  }

  takeFleetSpacePage(fleetId: number): PrefetchPage<FleetForumListItem> | null {
    if (fleetId <= 0 || !this.isFreshForScope(this.fleetPage, fleetId)) {
      if (this.fleetPage && this.fleetPage.scopeId !== fleetId) {
        this.fleetPage = null;
      }
      return null;
    }
    const page = this.fleetPage;
    this.fleetPage = null;
    return page;
  }

  /** Drop all decrypted list pages (logout, account switch, content mutations). */
  invalidate(): void {
    this.generation++;
    this.crewPage = null;
    this.fleetPage = null;
    this.crewInFlight = null;
    this.fleetInFlight = null;
  }

  /** Alias used by auth session cleanup. */
  clearSessionCache(): void {
    this.invalidate();
  }

  private isFreshForScope(page: PrefetchPage<unknown> | null, scopeId: number): boolean {
    return !!page
      && page.scopeId === scopeId
      && Date.now() - page.fetchedAt < PREFETCH_TTL_MS;
  }
}
