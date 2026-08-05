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
  items: T[];
  hasMore: boolean;
  fetchedAt: number;
}

const PREFETCH_TTL_MS = 90_000;
const PREFETCH_LIMIT = 20;

/**
 * Eagerly warms the first page of crew/fleet space lists from hub screens
 * so opening those routes feels instantaneous when crypto is unlocked.
 */
@Injectable({
  providedIn: 'root'
})
export class ForumListPrefetchService {
  private crewPage: PrefetchPage<DiscussionListItem> | null = null;
  private fleetPage: PrefetchPage<FleetForumListItem> | null = null;
  private crewInFlight: Promise<void> | null = null;
  private fleetInFlight: Promise<void> | null = null;

  private discussionService = inject(CrewDiscussionService);
  private fleetService = inject(FleetService);
  private proposalCrypto = inject(ProposalCryptoService);
  private cryptoSession = inject(CryptoSessionService);

  prefetchCrewSpace(crewId: number): void {
    if (crewId <= 0 || !this.cryptoSession.isUnlocked()) {
      return;
    }
    if (this.isFresh(this.crewPage) || this.crewInFlight) {
      return;
    }

    this.crewInFlight = (async () => {
      try {
        const page = await firstValueFrom(
          this.discussionService.getPosts(FORUM_DISCUSSION_CONFIG, { offset: 0, limit: PREFETCH_LIMIT })
        );
        const items = await this.proposalCrypto.decryptListItems(
          page.items as ProposalListItem[],
          crewId
        ) as DiscussionListItem[];
        this.crewPage = { items, hasMore: page.hasMore, fetchedAt: Date.now() };
      } catch {
        // Prefetch is best-effort.
      } finally {
        this.crewInFlight = null;
      }
    })();
  }

  prefetchFleetSpace(fleetId: number): void {
    if (fleetId <= 0 || !this.cryptoSession.isUnlocked()) {
      return;
    }
    if (this.isFresh(this.fleetPage) || this.fleetInFlight) {
      return;
    }

    this.fleetInFlight = (async () => {
      try {
        const response = await firstValueFrom(
          this.fleetService.getForums({ offset: 0, limit: PREFETCH_LIMIT })
        );
        if (!response.success) {
          return;
        }
        const rawItems = response.items ?? [];
        const items = await this.proposalCrypto.decryptListItems(
          rawItems as unknown as ProposalListItem[],
          { fleetId }
        ) as unknown as FleetForumListItem[];
        this.fleetPage = {
          items,
          hasMore: !!response.hasMore,
          fetchedAt: Date.now()
        };
      } catch {
        // Prefetch is best-effort.
      } finally {
        this.fleetInFlight = null;
      }
    })();
  }

  /** Consume a fresh crew space prefetch (clears cache so list owns the data). */
  takeCrewSpacePage(): PrefetchPage<DiscussionListItem> | null {
    if (!this.isFresh(this.crewPage)) {
      return null;
    }
    const page = this.crewPage;
    this.crewPage = null;
    return page;
  }

  takeFleetSpacePage(): PrefetchPage<FleetForumListItem> | null {
    if (!this.isFresh(this.fleetPage)) {
      return null;
    }
    const page = this.fleetPage;
    this.fleetPage = null;
    return page;
  }

  invalidate(): void {
    this.crewPage = null;
    this.fleetPage = null;
  }

  private isFresh(page: PrefetchPage<unknown> | null): boolean {
    return !!page && Date.now() - page.fetchedAt < PREFETCH_TTL_MS;
  }
}
