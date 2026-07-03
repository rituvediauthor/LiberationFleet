import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, map, Observable } from 'rxjs';
import { CrewService } from './crew.service';
import { GiftService } from './gift.service';

export type LibraryAccessBlockReason = 'disabled' | 'season-not-started' | 'not-in-season';

export type LibraryAccessResult =
  | { allowed: true }
  | { allowed: false; reason: LibraryAccessBlockReason };

@Injectable({
  providedIn: 'root'
})
export class LibraryAccessService {
  private crewService = inject(CrewService);
  private giftService = inject(GiftService);

  navigateToLibrary(router: Router): void {
    this.resolveAccess().subscribe({
      next: access => {
        if (access.allowed) {
          router.navigate(['/app/crew/library-of-things']);
          return;
        }

        router.navigate(['/app/crew/library-of-things/unlock'], {
          queryParams: { reason: access.reason }
        });
      },
      error: () => router.navigate(['/app/crew/library-of-things/unlock'])
    });
  }

  resolveAccess(): Observable<LibraryAccessResult> {
    return forkJoin({
      membership: this.crewService.getMembership(),
      season: this.giftService.getSeasonStatus()
    }).pipe(
      map(({ membership, season }) =>
        this.checkAccess(
          membership.libraryOfThingsEnabled !== false,
          season.seasonStarted,
          season.userInSeason
        ))
    );
  }

  checkAccess(
    libraryEnabled: boolean,
    seasonStarted: boolean,
    userInSeason: boolean
  ): LibraryAccessResult {
    if (!libraryEnabled) {
      return { allowed: false, reason: 'disabled' };
    }

    if (!seasonStarted) {
      return { allowed: false, reason: 'season-not-started' };
    }

    if (!userInSeason) {
      return { allowed: false, reason: 'not-in-season' };
    }

    return { allowed: true };
  }
}
