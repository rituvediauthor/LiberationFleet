/** Fired after --lf-keyboard-inset is written, including native iOS keyboard events. */
export const keyboardInsetChangeEventName = 'lf-keyboard-inset-change';

/** Pixels of the layout viewport covered by the software keyboard. */
export function computeKeyboardInset(
  innerHeight: number,
  visualHeight: number,
  visualOffsetTop: number
): number {
  return Math.max(0, Math.round(innerHeight - visualHeight - visualOffsetTop));
}

export function readKeyboardInset(): number {
  const viewport = window.visualViewport;
  if (!viewport) {
    return 0;
  }
  return computeKeyboardInset(window.innerHeight, viewport.height, viewport.offsetTop);
}

/**
 * Inset already applied to the document. iOS WKWebView often reports a zero
 * visualViewport inset while the native keyboard height is stored here.
 */
export function readAppliedKeyboardInset(): number {
  if (typeof document === 'undefined') {
    return 0;
  }
  const parsed = Number.parseFloat(
    document.documentElement.style.getPropertyValue('--lf-keyboard-inset')
  );
  if (Number.isFinite(parsed)) {
    return Math.max(0, parsed);
  }
  return readKeyboardInset();
}

/** Distance from the layout bottom to the top of a fixed composer sitting on the keyboard. */
export function composerClearance(footerHeight: number, keyboardInset: number): number {
  const measured = Math.ceil(footerHeight);
  const footer = measured > 0 ? measured : 72;
  return footer + Math.max(0, Math.round(keyboardInset));
}

export function notifyKeyboardInsetChange(): void {
  if (typeof window === 'undefined') {
    return;
  }
  window.dispatchEvent(new Event(keyboardInsetChangeEventName));
}

export function readVisualOffsetTop(): number {
  return Math.max(0, Math.round(window.visualViewport?.offsetTop ?? 0));
}

export function applyKeyboardInsetToDocument(
  root: CSSStyleDeclaration,
  classList: DOMTokenList,
  inset: number,
  visualTop: number
): void {
  root.setProperty('--lf-keyboard-inset', `${inset}px`);
  root.setProperty('--lf-visual-top', `${visualTop}px`);
  classList.toggle('lf-keyboard-open', inset > 40);
}
