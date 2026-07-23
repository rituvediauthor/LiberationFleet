import { Injectable, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { ActionBarButton } from '../components/page-layout/page-layout.component';

/** In-app notifications list (not notification preference settings). */
const NOTIFICATIONS_ROUTE = '/app/notifications';

@Injectable({
  providedIn: 'root'
})
export class NavigationService {
  private router = inject(Router);

  /** URL before the current navigation (path + query, no hash). */
  private previousUrl: string | null = null;
  private currentUrl: string;

  constructor() {
    this.currentUrl = this.normalizeUrl(this.router.url);

    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => {
        const nextUrl = this.normalizeUrl(event.urlAfterRedirects);
        if (nextUrl === this.currentUrl) {
          return;
        }
        this.previousUrl = this.currentUrl;
        this.currentUrl = nextUrl;
      });
  }

  /**
   * UI back: go to the page's canonical parent (`fallback`), unless the user
   * arrived here directly from the notifications list — then return there.
   * Does not use browser history (avoids bouncing through create/submit stacks).
   */
  back(fallback: string | string[] = '/app/crew'): void {
    if (this.isNotificationsList(this.previousUrl)) {
      void this.router.navigate([NOTIFICATIONS_ROUTE]);
      return;
    }

    void this.router.navigate(this.toCommands(fallback));
  }

  createBackButton(fallback: string | string[] = '/app/crew'): ActionBarButton {
    return {
      label: '←',
      type: 'back',
      onClick: () => this.back(fallback)
    };
  }

  private isNotificationsList(url: string | null): boolean {
    if (!url) {
      return false;
    }
    const path = this.stripQuery(url);
    return path === NOTIFICATIONS_ROUTE;
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
