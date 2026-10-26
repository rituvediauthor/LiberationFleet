import { Injectable } from '@angular/core';
import { registerPlugin } from '@capacitor/core';
import { PushNotifications } from '@capacitor/push-notifications';
import { isNativeApp } from '../utils/app-platform.util';

interface AppIconBadgePlugin {
  set(options: { count: number }): Promise<void>;
}

const AppIconBadge = registerPlugin<AppIconBadgePlugin>('AppIconBadge', {
  web: {
    set: async () => undefined
  }
});

/**
 * Home-screen icon badge. APNs/FCM set it when a push arrives; marking
 * notifications read in-app must write the same number (or 0) locally.
 */
@Injectable({ providedIn: 'root' })
export class AppIconBadgeService {
  private lastSent: number | null = null;

  async setCount(count: number, options?: { force?: boolean }): Promise<void> {
    if (!isNativeApp()) {
      return;
    }

    const next = Number.isFinite(count) ? Math.max(0, Math.floor(count)) : 0;
    // Never skip 0 — leftover OS badges from a previous account stay until we
    // write zero even when in-app unread is already 0.
    const force = options?.force === true || next === 0;
    if (!force && this.lastSent === next) {
      return;
    }
    this.lastSent = next;

    try {
      await AppIconBadge.set({ count: next });
    } catch {
      // Older binaries without AppIconBadgePlugin still clear the shade below.
    }

    if (next === 0) {
      try {
        await PushNotifications.removeAllDeliveredNotifications();
      } catch {
        // ignore
      }
    }
  }
}
