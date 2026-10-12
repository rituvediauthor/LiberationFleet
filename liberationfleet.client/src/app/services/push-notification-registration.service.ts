import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { PushNotifications, type Token, type ActionPerformed, type PushNotificationSchema } from '@capacitor/push-notifications';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { isNativeApp, isNativeAndroid, getAppRuntimePlatform } from '../utils/app-platform.util';
import { AppStorageService, StorageScope } from './storage/app-storage.service';

interface PushTokenApiResponse {
  success: boolean;
  message?: string;
}

const PUSH_DEVICE_ID_KEY = 'lf.push.deviceId';

/** Must match FCM android.notification.channel_id on the server. */
export const ANDROID_PUSH_CHANNEL_ID = 'liberation_fleet_default';

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
  private listenersAttached = false;
  private currentToken: string | null = null;

  async start(): Promise<void> {
    if (!isNativeApp()) {
      return;
    }

    // Allow a second pass after login if the first attempt got a native token
    // but could not upload it yet (or missed the registration event).
    if (this.started && this.currentToken) {
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

      if (isNativeAndroid()) {
        await this.ensureAndroidChannel();
      }

      // Listeners MUST be attached before register() — on Android the FCM token
      // is often delivered immediately and is otherwise lost forever.
      await this.ensureListeners();

      await PushNotifications.register();

      // If we already have a token from a prior session in this process, re-upload
      // after auth so DevicePushTokens is populated even when FCM does not re-fire.
      if (this.currentToken) {
        void this.registerToken(this.currentToken);
      }
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
    this.listenersAttached = false;

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

  private async ensureListeners(): Promise<void> {
    if (this.listenersAttached) {
      return;
    }

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

    this.listenersAttached = true;
  }

  private async ensureAndroidChannel(): Promise<void> {
    try {
      await PushNotifications.createChannel({
        id: ANDROID_PUSH_CHANNEL_ID,
        name: 'Liberation Fleet',
        description: 'Direct messages and other alerts',
        importance: 5,
        visibility: 1,
        sound: 'default',
        vibration: true
      });
    } catch (err) {
      console.warn('Failed to create Android notification channel', err);
    }
  }

  private async registerToken(token: string): Promise<void> {
    this.currentToken = token;
    const platform = getAppRuntimePlatform();
    if (platform !== 'ios' && platform !== 'android') {
      return;
    }

    const maxAttempts = 3;
    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        await firstValueFrom(
          this.http.put<PushTokenApiResponse>(this.apiUrl, {
            token,
            platform,
            deviceId: this.getOrCreateDeviceId(platform)
          })
        );
        return;
      } catch (err) {
        if (attempt === maxAttempts) {
          console.warn('Failed to register push token with API', err);
          // Keep started=true and currentToken so a later start() after login can retry.
          this.started = false;
          return;
        }
        await new Promise(resolve => setTimeout(resolve, 400 * attempt));
      }
    }
  }

  private getOrCreateDeviceId(platform: string): string {
    const existing = this.storage.get(StorageScope.Persistent, PUSH_DEVICE_ID_KEY);
    if (existing) {
      return existing;
    }
    const id = `${platform}-${crypto.randomUUID()}`;
    this.storage.set(StorageScope.Persistent, PUSH_DEVICE_ID_KEY, id);
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
