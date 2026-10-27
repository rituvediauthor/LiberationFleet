import { Injectable } from '@angular/core';
import { PreloadingStrategy, Route } from '@angular/router';
import { Observable, of, timer } from 'rxjs';
import { mergeMap } from 'rxjs/operators';

/** Tab roots and the public entry screens — not the whole 100-route catalog. */
const PRELOAD_PATHS = new Set([
  '',
  'sign-in',
  'app/crew',
  'app/fleet',
  'app/friends',
  'app/profile',
  'app/notifications',
  'app/crew/chats',
  'app/crew/forums',
  'app/crew/gift-log'
]);

const PRELOAD_DELAY_MS = 1_200;

@Injectable({ providedIn: 'root' })
export class IdlePreloadStrategy implements PreloadingStrategy {
  preload(route: Route, load: () => Observable<unknown>): Observable<unknown> {
    if (!PRELOAD_PATHS.has(route.path ?? '')) {
      return of(null);
    }

    return timer(PRELOAD_DELAY_MS).pipe(mergeMap(() => load()));
  }
}
