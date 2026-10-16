import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { isAndroidMobileBrowser } from '../../utils/app-platform.util';

const DISMISS_KEY = 'lf-play-store-banner-dismissed';

export const GOOGLE_PLAY_STORE_URL =
  'https://play.google.com/store/apps/details?id=com.liberationfleet.app';

@Component({
  selector: 'app-play-store-banner',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './play-store-banner.component.html',
  styleUrl: './play-store-banner.component.css'
})
export class PlayStoreBannerComponent {
  readonly storeUrl = GOOGLE_PLAY_STORE_URL;
  visible = false;

  constructor() {
    this.visible = this.shouldShow();
  }

  dismiss(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.visible = false;
    try {
      localStorage.setItem(DISMISS_KEY, '1');
    } catch {
      // Ignore storage failures — banner just hides for this session.
    }
  }

  private shouldShow(): boolean {
    if (!isAndroidMobileBrowser()) {
      return false;
    }
    try {
      return localStorage.getItem(DISMISS_KEY) !== '1';
    } catch {
      return true;
    }
  }
}
