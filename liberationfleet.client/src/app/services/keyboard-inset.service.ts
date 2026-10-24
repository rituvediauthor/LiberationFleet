import { Injectable, OnDestroy } from '@angular/core';
import { Keyboard } from '@capacitor/keyboard';
import { isNativeIos } from '../utils/app-platform.util';
import {
  applyKeyboardInsetToDocument,
  readKeyboardInset,
  readVisualOffsetTop
} from '../utils/keyboard-inset.util';

/**
 * Keeps --lf-keyboard-inset in sync with the visual viewport so fixed
 * composers sit above the iOS/Android software keyboard.
 */
@Injectable({ providedIn: 'root' })
export class KeyboardInsetService implements OnDestroy {
  private started = false;
  private readonly onViewportChange = () => this.sync();

  start(): void {
    if (this.started || typeof window === 'undefined') {
      return;
    }
    this.started = true;
    this.sync();
    window.visualViewport?.addEventListener('resize', this.onViewportChange);
    window.visualViewport?.addEventListener('scroll', this.onViewportChange);
    window.addEventListener('resize', this.onViewportChange);
    void this.configureNativeKeyboard();
  }

  ngOnDestroy(): void {
    if (!this.started || typeof window === 'undefined') {
      return;
    }
    window.visualViewport?.removeEventListener('resize', this.onViewportChange);
    window.visualViewport?.removeEventListener('scroll', this.onViewportChange);
    window.removeEventListener('resize', this.onViewportChange);
    this.started = false;
  }

  private sync(): void {
    if (typeof document === 'undefined') {
      return;
    }
    applyKeyboardInsetToDocument(
      document.documentElement.style,
      document.documentElement.classList,
      readKeyboardInset(),
      readVisualOffsetTop()
    );
  }

  private async configureNativeKeyboard(): Promise<void> {
    if (!isNativeIos()) {
      return;
    }
    try {
      await Keyboard.setAccessoryBarVisible({ isVisible: true });
      await Keyboard.setScroll({ isDisabled: true });
    } catch {
      // Plugin unavailable in some test / simulator shells.
    }
  }
}
