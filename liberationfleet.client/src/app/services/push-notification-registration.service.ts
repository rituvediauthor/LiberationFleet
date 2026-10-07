import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { PushNotifications, type Token, type ActionPerformed, type PushNotificationSchema } from '@capacitor/push-notifications';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { isNativeApp, getAppRuntimePlatform } from '../utils/app-platform.util';
import { AppStorageService, StorageScope } from './storage/app-storage.service';

interface PushTokenApiResponse {
  success: boolean;
  message?: string;
}

const PUSH_DEVICE_ID_KEY = 'lf.push.deviceId';

/**
 * Registers FCM/APNs tokens with the API and opens deep links from notification taps.
 * No-ops on web.
 */
@Injectable({ providedIn: 'root' })
export class PushNotificationRegistrationService {
  private readonly apiUrl = '/api/notifications/push-tokens';
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly storage = inject(AppStorageService);
  private started = false;
  private currentToken: string | null = null;

  async start(): Promise<void> {
    if (!isNativeApp() || this.started) {
      return;
    }
    this.started = true;

    try {
      let perm = await PushNotifications.checkPermissions();
      if (perm.receive === 'prompt' || perm.receive === 'prompt-with-rationale') {
        perm = await PushNotifications.requestPermissions();
      }
      if (perm.receive !== 'granted') {
        this.started = false;
        return;
      }

      await PushNotifications.register();

      await PushNotifications.addListener('registration', (token: Token) => {
        void this.registerToken(token.value);
      });

      await PushNotifications.addListener('registrationError', err => {
        console.warn('Push registration failed', err);
      });

      await PushNotifications.addListener(
        'pushNotificationActionPerformed',
        (action: ActionPerformed) => {
          this.openFromNotification(action.notification);
        }
      );

      await PushNotifications.addListener(
        'pushNotificationReceived',
        (_notification: PushNotificationSchema) => {
          // Foreground delivery — SignalR already updates the in-app feed.
        }
      );
    } catch (err) {
      console.warn('Push notifications unavailable', err);
      this.started = false;
    }
  }

  async stopAndUnregister(): Promise<void> {
    if (!isNativeApp()) {
      return;
    }

    const token = this.currentToken;
    this.currentToken = null;
    this.started = false;

    try {
      await PushNotifications.removeAllListeners();
    } catch {
      // ignore
    }

    if (!token) {
      return;
    }

    try {
      await firstValueFrom(
        this.http.delete<PushTokenApiResponse>(this.apiUrl, {
          body: { token, allDevices: false }
        })
      );
    } catch {
      // Best-effort logout cleanup.
    }
  }

  private async registerToken(token: string): Promise<void> {
    this.currentToken = token;
    const platform = getAppRuntimePlatform();
    if (platform !== 'ios' && platform !== 'android') {
      return;
    }

    try {
      await firstValueFrom(
        this.http.put<PushTokenApiResponse>(this.apiUrl, {
          token,
          platform,
          deviceId: this.getOrCreateDeviceId(platform)
        })
      );
    } catch (err) {
      console.warn('Failed to register push token with API', err);
    }
  }

  private getOrCreateDeviceId(platform: string): string {
    const existing = this.storage.get(StorageScope.Local, PUSH_DEVICE_ID_KEY);
    if (existing) {
      return existing;
    }
    const id = `${platform}-${crypto.randomUUID()}`;
    this.storage.set(StorageScope.Local, PUSH_DEVICE_ID_KEY, id);
    return id;
  }

  private openFromNotification(notification: PushNotificationSchema): void {
    const data = notification.data as Record<string, unknown> | undefined;
    const actionUrl =
      (typeof data?.['actionUrl'] === 'string' && data['actionUrl']) ||
      (typeof data?.['actionurl'] === 'string' && data['actionurl']) ||
      '';

    if (!actionUrl) {
      void this.router.navigate(['/app/notifications']);
      return;
    }

    if (actionUrl.startsWith('/')) {
      void this.router.navigateByUrl(actionUrl);
      return;
    }

    void this.router.navigate(['/app/notifications']);
  }
}
