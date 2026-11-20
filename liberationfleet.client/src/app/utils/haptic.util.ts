import { Haptics, ImpactStyle, NotificationType } from '@capacitor/haptics';
import { isConstrainedMobileRuntime, isNativeApp } from './app-platform.util';

/** True when haptics / vibrate should run (native or mobile browser). Desktop: no-op. */
export function hapticsSupported(): boolean {
  return isNativeApp() || isConstrainedMobileRuntime();
}

async function impact(style: ImpactStyle): Promise<void> {
  if (!hapticsSupported()) {
    return;
  }
  try {
    if (isNativeApp()) {
      await Haptics.impact({ style });
      return;
    }
    vibrateFallback(style === ImpactStyle.Heavy ? 40 : style === ImpactStyle.Medium ? 24 : 12);
  } catch {
    // Best-effort — never block UI on haptic failures.
  }
}

function vibrateFallback(pattern: number | number[]): void {
  if (typeof navigator === 'undefined' || typeof navigator.vibrate !== 'function') {
    return;
  }
  try {
    navigator.vibrate(pattern);
  } catch {
    // ignore
  }
}

/** Celebratory feedback after a successful gift record. */
export async function hapticCelebration(): Promise<void> {
  if (!hapticsSupported()) {
    return;
  }
  try {
    if (isNativeApp()) {
      await Haptics.notification({ type: NotificationType.Success });
      await Haptics.impact({ style: ImpactStyle.Medium });
      return;
    }
    vibrateFallback([18, 40, 28, 40, 45]);
  } catch {
    // ignore
  }
}

/** Low rumble while the user is in the pull-to-refresh “almost” zone. */
export async function hapticPullRumble(): Promise<void> {
  await impact(ImpactStyle.Light);
}

/** Single tap when pull-to-refresh arms / starts. */
export async function hapticPullArmed(): Promise<void> {
  await impact(ImpactStyle.Medium);
}

/** Desktop / fine-pointer: no pull-to-refresh. */
export function isPullRefreshDesktop(): boolean {
  if (typeof window === 'undefined') {
    return true;
  }
  if (isConstrainedMobileRuntime() || isNativeApp()) {
    return false;
  }
  const coarse = window.matchMedia?.('(pointer: coarse)')?.matches;
  return !coarse;
}

/** Mobile / coarse pointer: swipe carousels instead of arrows. */
export function preferSwipeCarousel(): boolean {
  return !isPullRefreshDesktop();
}
