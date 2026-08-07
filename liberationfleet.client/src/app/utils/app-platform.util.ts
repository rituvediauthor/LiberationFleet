import { Capacitor } from '@capacitor/core';

/** Capacitor shell or plain browser. */
export type AppRuntimePlatform = 'web' | 'ios' | 'android';

/**
 * Runtime surface: Capacitor `getPlatform()` (`ios` / `android` / `web`).
 * Home-screen PWAs and desktop browsers are `web`.
 */
export function getAppRuntimePlatform(): AppRuntimePlatform {
  const platform = Capacitor.getPlatform();
  if (platform === 'ios' || platform === 'android') {
    return platform;
  }
  return 'web';
}

/** True inside a Capacitor iOS / Android binary (not Safari/Chrome PWA). */
export function isNativeApp(): boolean {
  return Capacitor.isNativePlatform();
}

export function isNativeIos(): boolean {
  return isNativeApp() && getAppRuntimePlatform() === 'ios';
}

export function isNativeAndroid(): boolean {
  return isNativeApp() && getAppRuntimePlatform() === 'android';
}

/** Safari / Chrome on iPhone/iPad (including home-screen PWA), not Cap iOS. */
export function isAppleMobileBrowser(): boolean {
  if (typeof navigator === 'undefined' || isNativeApp()) {
    return false;
  }
  const ua = navigator.userAgent || '';
  if (/iPhone|iPad|iPod/i.test(ua)) {
    return true;
  }
  return navigator.platform === 'MacIntel' && (navigator.maxTouchPoints || 0) > 1;
}

/** Chrome/Firefox/Samsung Internet on Android phones (browser or PWA), not Cap Android. */
export function isAndroidMobileBrowser(): boolean {
  if (typeof navigator === 'undefined' || isNativeApp()) {
    return false;
  }
  return /Android/i.test(navigator.userAgent || '');
}

/**
 * Constrained mobile runtimes where huge passthrough uploads often OOM
 * (native shells + mobile browsers). Desktop web is not constrained.
 */
export function isConstrainedMobileRuntime(): boolean {
  if (isNativeIos() || isNativeAndroid()) {
    return true;
  }
  return isAppleMobileBrowser() || isAndroidMobileBrowser();
}
