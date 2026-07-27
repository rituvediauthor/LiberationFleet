import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, catchError, map, of, shareReplay, tap } from 'rxjs';

export interface ClientConfig {
  showFallibleAttribution: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ClientConfigService {
  private http = inject(HttpClient);

  /** Optimistic: hide immediately on staging hostnames before the API responds. */
  private readonly initialShow =
    typeof location === 'undefined' || !/staging/i.test(location.hostname);

  private readonly showFallibleAttributionSubject = new BehaviorSubject<boolean>(this.initialShow);
  readonly showFallibleAttribution$ = this.showFallibleAttributionSubject.asObservable();

  private readonly load$ = this.http.get<ClientConfig>('/api/client-config').pipe(
    map((config) => config.showFallibleAttribution !== false),
    catchError(() => of(this.initialShow)),
    tap((show) => this.showFallibleAttributionSubject.next(show)),
    shareReplay(1)
  );

  constructor() {
    this.load$.subscribe();
  }

  get showFallibleAttribution(): boolean {
    return this.showFallibleAttributionSubject.value;
  }
}
