import { Injectable, inject } from '@angular/core';
import { Location } from '@angular/common';
import { NavigationEnd, Router } from '@angular/router';
import { filter, takeUntil, timer } from 'rxjs';
import { ActionBarButton } from '../components/page-layout/page-layout.component';

/**
 * Routes that often auto-redirect based on membership/season state.
 * Landing on these via history.back() can bounce the user back to the page they left.
 */
const HISTORY_REDIRECT_TRAPS = [
  '/app/crew/season-setup',
  '/app/crew/join-season',
  '/app/crew/library-of-things/unlock'
] as const;

@Injectable({
  providedIn: 'root'
})
export class NavigationService {
  private location = inject(Location);
  private router = inject(Router);

  back(fallback: string | string[] = '/app/crew'): void {
    const currentUrl = this.normalizeUrl(this.router.url);
    const fallbackCommands = this.toCommands(fallback);
    let leftCurrent = false;
    let settled = false;

    const finishWithFallback = () => {
      if (settled) {
        return;
      }
      settled = true;
      void this.router.navigate(fallbackCommands, { replaceUrl: true });
    };

    const navigationWatcher = this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntil(timer(400))
      )
      .subscribe(event => {
        const nextUrl = this.normalizeUrl(event.urlAfterRedirects);

        if (nextUrl === currentUrl) {
          if (leftCurrent) {
            // history.back() briefly left, then a redirect bounced us back.
            finishWithFallback();
          }
          return;
        }

        leftCurrent = true;

        if (this.isRedirectTrap(nextUrl) || this.isSameLogicalRoute(nextUrl, currentUrl)) {
          finishWithFallback();
        } else {
          settled = true;
        }
      });

    this.location.back();

    // If history cannot go back (or back is a no-op), use the fallback.
    setTimeout(() => {
      if (!settled && !leftCurrent && this.normalizeUrl(this.router.url) === currentUrl) {
        navigationWatcher.unsubscribe();
        finishWithFallback();
      }
    }, 50);
  }

  createBackButton(fallback: string | string[] = '/app/crew'): ActionBarButton {
    return {
      label: '←',
      type: 'back',
      onClick: () => this.back(fallback)
    };
  }

  private isRedirectTrap(url: string): boolean {
    return HISTORY_REDIRECT_TRAPS.some(trap => url === trap || url.startsWith(`${trap}?`));
  }

  private isSameLogicalRoute(a: string, b: string): boolean {
    return this.stripQuery(a) === this.stripQuery(b);
  }

  private normalizeUrl(url: string): string {
    const [pathAndQuery] = url.split('#');
    return pathAndQuery || '/';
  }

  private stripQuery(url: string): string {
    return this.normalizeUrl(url).split('?')[0];
  }

  private toCommands(route: string | string[]): string[] {
    return typeof route === 'string' ? [route] : route;
  }
}
