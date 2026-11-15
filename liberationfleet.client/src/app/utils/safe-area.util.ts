/**
 * Safe-area helpers for edge-to-edge Capacitor shells.
 *
 * On iPhone Duo's outer display, WebKit often reports env(safe-area-inset-right)
 * as 0 while the camera/control rail still covers the trailing edge. When the
 * reported trailing inset is missing and the viewport matches Duo outer geometry,
 * fall back to Apple's ~84pt rail so fixed chrome (toasts) clears the camera.
 */

/** Documented Duo outer rail width (pt / CSS px). */
export const DUO_OUTER_RAIL_CSS_PX = 84;

/**
 * Extra trailing inset when CSS/env omitted the Duo outer camera rail.
 * Returns 0 for every other device / pose so existing layouts stay unchanged.
 */
export function estimateMissingTrailingSafeInsetCssPx(
  viewportWidth: number,
  viewportHeight: number,
  devicePixelRatio: number,
  reportedRightInsetCssPx: number
): number {
  if (reportedRightInsetCssPx >= 24) {
    return 0;
  }

  const short = Math.min(viewportWidth, viewportHeight);
  const long = Math.max(viewportWidth, viewportHeight);
  if (devicePixelRatio < 2.5 || short < 430 || short > 500) {
    return 0;
  }

  // Duo outer ~678/466 ≈ 1.455 (portrait or landscape).
  const ratio = long / short;
  if (ratio < 1.4 || ratio > 1.52) {
    return 0;
  }

  return DUO_OUTER_RAIL_CSS_PX;
}

/** Parse a CSS pixel length such as "47px" or "0px". */
export function parseCssPx(value: string | null | undefined): number {
  if (!value) {
    return 0;
  }
  const match = /^(-?[\d.]+)px$/i.exec(value.trim());
  if (!match) {
    return 0;
  }
  const parsed = Number(match[1]);
  return Number.isFinite(parsed) ? parsed : 0;
}

/**
 * Reads env(safe-area-inset-right) through a probe so our own --lf-inset-right
 * fallback is not mistaken for a system-reported value on re-sync.
 */
export function readEnvSafeAreaInsetRightCssPx(doc: Document = document): number {
  if (typeof doc === 'undefined' || !doc.body) {
    return 0;
  }

  const probe = doc.createElement('div');
  probe.setAttribute('data-lf-safe-area-probe', '1');
  probe.style.cssText =
    'position:absolute;visibility:hidden;pointer-events:none;padding:0;padding-right:env(safe-area-inset-right,0px);';
  doc.body.appendChild(probe);
  const value = parseCssPx(getComputedStyle(probe).paddingRight);
  probe.remove();
  return value;
}

/**
 * System-reported trailing inset: CSS env() plus native-injected --lf-inset-right,
 * excluding an active Duo rail fallback we published ourselves.
 */
export function readReportedRightSafeInsetCssPx(doc: Document = document): number {
  const root = doc.documentElement;
  const style = getComputedStyle(root);
  const envRight = readEnvSafeAreaInsetRightCssPx(doc);
  const usingFallback = root.dataset['lfDuoRailFallback'] === '1';
  const injectedRight = usingFallback
    ? 0
    : parseCssPx(style.getPropertyValue('--lf-inset-right'));
  return Math.max(envRight, injectedRight);
}

/**
 * When Duo outer geometry is detected and the trailing safe inset is missing,
 * publish --lf-inset-right so --lf-safe-right (and toast gutters) clear the camera.
 * No-ops when a real inset is already present or the device is not Duo-outer-like.
 */
export function syncDuoOuterTrailingInsetFallback(
  doc: Document = document,
  viewport: { width: number; height: number; devicePixelRatio: number } = {
    width: window.visualViewport?.width ?? window.innerWidth,
    height: window.visualViewport?.height ?? window.innerHeight,
    devicePixelRatio: window.devicePixelRatio || 1
  }
): number {
  const root = doc.documentElement;
  const reported = readReportedRightSafeInsetCssPx(doc);
  const missing = estimateMissingTrailingSafeInsetCssPx(
    viewport.width,
    viewport.height,
    viewport.devicePixelRatio,
    reported
  );

  if (missing <= 0) {
    // Clear only our duo fallback marker; leave native/Android-injected insets alone.
    if (root.dataset['lfDuoRailFallback'] === '1') {
      root.style.removeProperty('--lf-inset-right');
      delete root.dataset['lfDuoRailFallback'];
    }
    return 0;
  }

  root.style.setProperty('--lf-inset-right', `${missing}px`);
  root.dataset['lfDuoRailFallback'] = '1';
  return missing;
}
