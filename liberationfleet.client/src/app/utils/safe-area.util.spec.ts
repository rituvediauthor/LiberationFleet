import {
  DUO_OUTER_RAIL_CSS_PX,
  estimateMissingTrailingSafeInsetCssPx,
  parseCssPx,
  syncDuoOuterTrailingInsetFallback
} from './safe-area.util';

describe('safe-area.util', () => {
  it('parseCssPx reads pixel lengths', () => {
    expect(parseCssPx('84px')).toBe(84);
    expect(parseCssPx(' 12.5px ')).toBe(12.5);
    expect(parseCssPx('0')).toBe(0);
    expect(parseCssPx('')).toBe(0);
  });

  it('does not invent a trailing inset when one is already reported', () => {
    expect(estimateMissingTrailingSafeInsetCssPx(466, 678, 3, 44)).toBe(0);
  });

  it('does not invent a trailing inset on ordinary iPhone sizes', () => {
    expect(estimateMissingTrailingSafeInsetCssPx(393, 852, 3, 0)).toBe(0);
    expect(estimateMissingTrailingSafeInsetCssPx(430, 932, 3, 0)).toBe(0);
  });

  it('estimates Duo outer rail when safe-area-inset-right is missing', () => {
    expect(estimateMissingTrailingSafeInsetCssPx(466, 678, 3, 0)).toBe(DUO_OUTER_RAIL_CSS_PX);
    expect(estimateMissingTrailingSafeInsetCssPx(678, 466, 3, 0)).toBe(DUO_OUTER_RAIL_CSS_PX);
  });

  it('syncDuoOuterTrailingInsetFallback sets, keeps across re-sync, then clears off Duo', () => {
    const root = document.documentElement;
    root.style.removeProperty('--lf-inset-right');
    delete root.dataset['lfDuoRailFallback'];

    const applied = syncDuoOuterTrailingInsetFallback(document, {
      width: 466,
      height: 678,
      devicePixelRatio: 3
    });
    expect(applied).toBe(DUO_OUTER_RAIL_CSS_PX);
    expect(root.style.getPropertyValue('--lf-inset-right')).toBe('84px');
    expect(root.dataset['lfDuoRailFallback']).toBe('1');

    // Re-sync must not clear the fallback just because we published --lf-inset-right.
    const kept = syncDuoOuterTrailingInsetFallback(document, {
      width: 466,
      height: 678,
      devicePixelRatio: 3
    });
    expect(kept).toBe(DUO_OUTER_RAIL_CSS_PX);
    expect(root.dataset['lfDuoRailFallback']).toBe('1');

    const cleared = syncDuoOuterTrailingInsetFallback(document, {
      width: 393,
      height: 852,
      devicePixelRatio: 3
    });
    expect(cleared).toBe(0);
    expect(root.style.getPropertyValue('--lf-inset-right')).toBe('');
    expect(root.dataset['lfDuoRailFallback']).toBeUndefined();
  });
});
