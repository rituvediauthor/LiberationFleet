import { Injectable } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { SplashScreen } from '@capacitor/splash-screen';

const SPLASH_FALLBACK_MS = 6_000;
const SPLASH_FADE_MS = 180;

/**
 * Holds the Capacitor splash until the first routed shell can paint, then
 * fades it. Web is a no-op. A timeout keeps the splash from sticking forever.
 */
@Injectable({ providedIn: 'root' })
export class NativeSplashService {
  private hidden = false;
  private scheduled = false;
  private fallbackHandle: ReturnType<typeof setTimeout> | null = null;

  startFallbackTimer(): void {
    if (!Capacitor.isNativePlatform() || this.fallbackHandle || this.hidden) {
      return;
    }

    this.fallbackHandle = setTimeout(() => {
      this.fallbackHandle = null;
      void this.hide();
    }, SPLASH_FALLBACK_MS);
  }

  hideAfterFirstPaint(): void {
    if (this.scheduled || this.hidden) {
      return;
    }

    this.scheduled = true;
    const paint = typeof requestAnimationFrame === 'function'
      ? (cb: FrameRequestCallback) => requestAnimationFrame(cb)
      : (cb: FrameRequestCallback) => setTimeout(cb, 0) as unknown as number;
    paint(() => {
      paint(() => {
        void this.hide();
      });
    });
  }

  async hide(): Promise<void> {
    if (this.hidden) {
      return;
    }

    this.hidden = true;
    this.clearFallback();

    if (!Capacitor.isNativePlatform()) {
      return;
    }

    try {
      await SplashScreen.hide({ fadeOutDuration: SPLASH_FADE_MS });
    } catch {
      // Splash may already be gone on some emulators.
    }
  }

  private clearFallback(): void {
    if (this.fallbackHandle) {
      clearTimeout(this.fallbackHandle);
      this.fallbackHandle = null;
    }
  }
}
