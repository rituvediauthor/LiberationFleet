import { Capacitor } from '@capacitor/core';
import { StatusBar, Style } from '@capacitor/status-bar';
import { SplashScreen } from '@capacitor/splash-screen';

/** Platform polish for Capacitor iOS / Android shells. No-ops on web. */
export async function initializeNativeShell(): Promise<void> {
  if (!Capacitor.isNativePlatform()) {
    return;
  }

  try {
    await StatusBar.setStyle({ style: Style.Dark });
    if (Capacitor.getPlatform() === 'android') {
      await StatusBar.setBackgroundColor({ color: '#0b1220' });
    }
  } catch {
    // StatusBar may be unavailable on some emulators.
  }

  try {
    await SplashScreen.hide();
  } catch {
    // Ignore if splash already hidden.
  }
}
