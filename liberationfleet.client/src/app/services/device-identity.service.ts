import { Injectable } from '@angular/core';
import { AppStorageService, StorageScope } from './storage/app-storage.service';
import { DEVICE_ID_STORAGE_KEY } from './storage/storage-keys';

@Injectable({
  providedIn: 'root'
})
export class DeviceIdentityService {
  constructor(private storage: AppStorageService) {}

  getDeviceId(): string {
    const existing = this.storage.get(StorageScope.Persistent, DEVICE_ID_STORAGE_KEY);
    if (existing) {
      return existing;
    }

    const deviceId = this.createDeviceId();
    this.storage.set(StorageScope.Persistent, DEVICE_ID_STORAGE_KEY, deviceId);
    return deviceId;
  }

  getDeviceName(): string {
    if (typeof navigator === 'undefined') {
      return 'Unknown device';
    }

    const platform = navigator.platform?.trim();
    const userAgent = navigator.userAgent ?? '';
    if (/iPhone|iPad|iPod/i.test(userAgent)) {
      return 'Apple mobile device';
    }

    if (/Android/i.test(userAgent)) {
      return 'Android device';
    }

    if (/Windows/i.test(userAgent)) {
      return platform ? `Windows (${platform})` : 'Windows device';
    }

    if (/Mac/i.test(userAgent)) {
      return platform ? `Mac (${platform})` : 'Mac device';
    }

    if (/Linux/i.test(userAgent)) {
      return 'Linux device';
    }

    return platform || 'Web browser';
  }

  getUserAgent(): string {
    return typeof navigator !== 'undefined' ? navigator.userAgent : '';
  }

  private createDeviceId(): string {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
      return crypto.randomUUID();
    }

    return `lf-${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
  }
}
