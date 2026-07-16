import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRouteSnapshot, RouterStateSnapshot, TitleStrategy } from '@angular/router';

@Injectable()
export class LiberationFleetTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const routeTitle = this.buildTitle(snapshot) ?? this.titleFromData(snapshot.root);
    if (routeTitle) {
      this.title.setTitle(`${routeTitle} · Liberation Fleet`);
      return;
    }

    const path = snapshot.url.split('?')[0];
    const segment = path.split('/').filter(Boolean).pop();
    if (!segment || segment === 'app') {
      this.title.setTitle('Liberation Fleet');
      return;
    }

    const pretty = segment
      .replace(/-/g, ' ')
      .replace(/\b\w/g, c => c.toUpperCase());
    this.title.setTitle(`${pretty} · Liberation Fleet`);
  }

  private titleFromData(route: ActivatedRouteSnapshot): string | undefined {
    let current: ActivatedRouteSnapshot | null = route;
    let found: string | undefined;
    while (current) {
      const dataTitle = current.data?.['title'];
      if (typeof dataTitle === 'string' && dataTitle.trim()) {
        found = dataTitle.trim();
      }
      current = current.firstChild;
    }
    return found;
  }
}
