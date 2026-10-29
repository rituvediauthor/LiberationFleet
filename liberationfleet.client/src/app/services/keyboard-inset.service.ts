import { Injectable, OnDestroy } from '@angular/core';
import { Keyboard, KeyboardInfo } from '@capacitor/keyboard';
import type { PluginListenerHandle } from '@capacitor/core';
import { isNativeIos } from '../utils/app-platform.util';
import {
  applyKeyboardInsetToDocument,
  notifyKeyboardInsetChange,
  readKeyboardInset,
  readVisualOffsetTop
} from '../utils/keyboard-inset.util';

const COMPOSER_SELECTOR = '.chat-action-bar, .detail-action-bar, .gift-action-bar, .dialog-backdrop';

/**
 * Keeps fixed composers above the software keyboard.
 * iOS WKWebView often reports a zero visualViewport inset, so native builds
 * use the Capacitor keyboard height and pin the composer bars directly.
 */
@Injectable({ providedIn: 'root' })
export class KeyboardInsetService implements OnDestroy {
  private started = false;
  private nativeInset = 0;
  private pluginReported = false;
  private readonly listeners: PluginListenerHandle[] = [];
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
    for (const listener of this.listeners) {
      void listener.remove();
    }
    this.listeners.length = 0;
    this.started = false;
  }

  private sync(): void {
    if (typeof document === 'undefined') {
      return;
    }
    const inset = this.pluginReported ? this.nativeInset : readKeyboardInset();
    applyKeyboardInsetToDocument(
      document.documentElement.style,
      document.documentElement.classList,
      inset,
      readVisualOffsetTop()
    );
    this.pinComposers(inset);
    notifyKeyboardInsetChange();
  }

  private pinComposers(inset: number): void {
    const nodes = document.querySelectorAll<HTMLElement>(COMPOSER_SELECTOR);
    nodes.forEach(node => {
      if (inset > 0) {
        node.style.setProperty('bottom', `${inset}px`, 'important');
      } else {
        node.style.removeProperty('bottom');
      }
    });
  }

  private onKeyboardShow = (info: KeyboardInfo): void => {
    this.pluginReported = true;
    this.nativeInset = Math.max(0, Math.round(info.keyboardHeight));
    this.sync();
  };

  private onKeyboardHide = (): void => {
    this.pluginReported = false;
    this.nativeInset = 0;
    this.sync();
  };

  private async configureNativeKeyboard(): Promise<void> {
    if (!isNativeIos()) {
      return;
    }
    try {
      await Keyboard.setAccessoryBarVisible({ isVisible: false });
      await Keyboard.setScroll({ isDisabled: true });
      this.listeners.push(
        await Keyboard.addListener('keyboardWillShow', this.onKeyboardShow),
        await Keyboard.addListener('keyboardDidShow', this.onKeyboardShow),
        await Keyboard.addListener('keyboardWillHide', this.onKeyboardHide)
      );
    } catch {
      // Plugin unavailable in some test / simulator shells.
    }
  }
}
