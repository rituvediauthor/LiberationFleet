import { Injectable, inject } from '@angular/core';
import { Location } from '@angular/common';
import { Router } from '@angular/router';
import { ActionBarButton } from '../components/page-layout/page-layout.component';

@Injectable({
  providedIn: 'root'
})
export class NavigationService {
  private location = inject(Location);
  private router = inject(Router);

  back(fallback: string | string[] = '/app/crew'): void {
    const currentUrl = this.router.url;
    this.location.back();

    setTimeout(() => {
      if (this.router.url === currentUrl) {
        void this.router.navigate(this.toCommands(fallback));
      }
    }, 0);
  }

  createBackButton(fallback: string | string[] = '/app/crew'): ActionBarButton {
    return {
      label: '←',
      type: 'back',
      onClick: () => this.back(fallback)
    };
  }

  private toCommands(route: string | string[]): string[] {
    return typeof route === 'string' ? [route] : route;
  }
}
