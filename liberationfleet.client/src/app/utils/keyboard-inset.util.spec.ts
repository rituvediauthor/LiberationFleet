import {
  applyKeyboardInsetToDocument,
  composerClearance,
  computeKeyboardInset,
  readAppliedKeyboardInset
} from './keyboard-inset.util';

describe('keyboard-inset.util', () => {
  it('returns 0 when the visual viewport fills the window', () => {
    expect(computeKeyboardInset(800, 800, 0)).toBe(0);
  });

  it('returns the covered band when the keyboard shrinks the visual viewport', () => {
    expect(computeKeyboardInset(800, 468, 0)).toBe(332);
  });

  it('subtracts iOS visualViewport.offsetTop so a scrolled page is not double-padded', () => {
    expect(computeKeyboardInset(800, 468, 40)).toBe(292);
  });

  it('never returns a negative inset', () => {
    expect(computeKeyboardInset(600, 800, 0)).toBe(0);
  });

  it('writes CSS vars and toggles lf-keyboard-open', () => {
    const root = document.createElement('div').style;
    const host = document.createElement('div');
    applyKeyboardInsetToDocument(root, host.classList, 300, 12);
    expect(root.getPropertyValue('--lf-keyboard-inset')).toBe('300px');
    expect(root.getPropertyValue('--lf-visual-top')).toBe('12px');
    expect(host.classList.contains('lf-keyboard-open')).toBeTrue();

    applyKeyboardInsetToDocument(root, host.classList, 0, 0);
    expect(host.classList.contains('lf-keyboard-open')).toBeFalse();
  });

  it('reads the inset written on the document when the visual viewport stays full', () => {
    document.documentElement.style.setProperty('--lf-keyboard-inset', '336px');
    expect(readAppliedKeyboardInset()).toBe(336);
    document.documentElement.style.removeProperty('--lf-keyboard-inset');
  });

  it('reserves the composer and the keyboard so messages can scroll above both', () => {
    expect(composerClearance(88, 0)).toBe(88);
    expect(composerClearance(96.2, 336)).toBe(433);
    expect(composerClearance(0, 300)).toBe(372);
  });
});
