import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of } from 'rxjs';

export interface CommunityActivityStats {
  activeCrewCount: number;
  activeCrewmateCount: number;
  activeWindowDays: number;
}

@Injectable({ providedIn: 'root' })
export class CommunityStatsService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/stats';

  getCommunityActivity(): Observable<CommunityActivityStats> {
    return this.http.get<CommunityActivityStats>(`${this.apiUrl}/community-activity`).pipe(
      catchError(() =>
        of({
          activeCrewCount: 0,
          activeCrewmateCount: 0,
          activeWindowDays: 30
        })
      )
    );
  }
}
