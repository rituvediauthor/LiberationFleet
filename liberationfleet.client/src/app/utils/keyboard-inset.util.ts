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
