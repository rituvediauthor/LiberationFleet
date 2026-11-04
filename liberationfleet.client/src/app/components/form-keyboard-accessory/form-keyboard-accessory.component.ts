import { CommonModule } from '@angular/common';
import {
  ChangeDetectorRef,
  Component,
  NgZone,
  OnDestroy,
  OnInit,
  inject
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CharCounterComponent } from '../char-counter/char-counter.component';
import {
  keyboardInsetChangeEventName,
  readAppliedKeyboardInset
} from '../../utils/keyboard-inset.util';
import { isNativeApp } from '../../utils/app-platform.util';

type FormField = HTMLInputElement | HTMLTextAreaElement;

const SKIP_SELECTOR = [
  '.chat-action-bar',
  '.detail-action-bar',
  '.gift-action-bar',
  '.dialog-backdrop',
  '.form-keyboard-accessory',
  '[data-lf-no-form-accessory]'
].join(', ');

/** Input types the accessory may mirror 1:1 (everything else stays text). */
const MIRROR_INPUT_TYPES = new Set([
  'text',
  'password',
  'email',
  'tel',
  'url',
  'search',
  'number'
]);

/**
 * Native form accessory: mirrors the focused field above the keyboard and
 * offers a checkmark control to dismiss (done with this field, not submit).
 */
@Component({
  selector: 'app-form-keyboard-accessory',
  standalone: true,
  imports: [CommonModule, FormsModule, CharCounterComponent],
  templateUrl: './form-keyboard-accessory.component.html',
  styleUrl: './form-keyboard-accessory.component.css'
})
export class FormKeyboardAccessoryComponent implements OnInit, OnDestroy {
  visible = false;
  draft = '';
  multiline = false;
  /** Mirrors the focused field's type so passwords stay masked. */
  inputType = 'text';
  /** From the focused field's maxlength (0 = no limit shown/enforced here). */
  maxLength = 0;
  minLength = 0;
  required = false;
  pattern: string | null = null;
  inputMode: string | null = null;

  private activeField: FormField | null = null;
  private syncingFromField = false;
  private readonly zone = inject(NgZone);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly onFocusIn = (event: FocusEvent) => this.handleFocus(event);
  private readonly onFocusOut = () => this.scheduleHideIfBlurred();
  private readonly onKeyboardInset = () => this.syncVisibility();
  private readonly onFieldInput = () => this.pullDraftFromField();
  private hideTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    if (typeof document === 'undefined' || !isNativeApp()) {
      return;
    }
    // Listen outside Angular; push state updates back into the zone below.
    this.zone.runOutsideAngular(() => {
      document.addEventListener('focusin', this.onFocusIn, true);
      document.addEventListener('focusout', this.onFocusOut, true);
      window.addEventListener(keyboardInsetChangeEventName, this.onKeyboardInset);
    });
  }

  ngOnDestroy(): void {
    if (typeof document === 'undefined') {
      return;
    }
    document.removeEventListener('focusin', this.onFocusIn, true);
    document.removeEventListener('focusout', this.onFocusOut, true);
    window.removeEventListener(keyboardInsetChangeEventName, this.onKeyboardInset);
    this.unbindActiveField();
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
    }
  }

  get draftLength(): number {
    return this.draft?.length ?? 0;
  }

  get showCharCounter(): boolean {
    return this.maxLength > 0;
  }

  onDraftChange(value: string): void {
    let next = value ?? '';
    if (this.maxLength > 0 && next.length > this.maxLength) {
      next = next.slice(0, this.maxLength);
    }
    this.draft = next;
    if (!this.activeField || this.syncingFromField) {
      return;
    }
    this.writeValueToField(this.activeField, this.draft);
  }

  done(): void {
    const field = this.activeField;
    this.hide();
    field?.blur();
  }

  private handleFocus(event: FocusEvent): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement)) {
      return;
    }
    if (!this.isAccessoryEligible(target)) {
      return;
    }
    this.zone.run(() => {
      if (this.hideTimer) {
        clearTimeout(this.hideTimer);
        this.hideTimer = null;
      }
      this.bindField(target);
      this.syncVisibility();
      this.scrollFieldIntoView(target);
      this.cdr.markForCheck();
    });
  }

  private scheduleHideIfBlurred(): void {
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
    }
    this.hideTimer = setTimeout(() => {
      this.hideTimer = null;
      this.zone.run(() => {
        const active = document.activeElement;
        if (active instanceof HTMLElement && active.closest('.form-keyboard-accessory')) {
          return;
        }
        if (active === this.activeField) {
          return;
        }
        if (active instanceof HTMLInputElement || active instanceof HTMLTextAreaElement) {
          if (this.isAccessoryEligible(active)) {
            this.bindField(active);
            this.syncVisibility();
            this.cdr.markForCheck();
            return;
          }
        }
        this.hide();
      });
    }, 40);
  }

  private bindField(field: FormField): void {
    if (this.activeField && this.activeField !== field) {
      this.unbindActiveField();
    }
    this.activeField = field;
    this.multiline = field instanceof HTMLTextAreaElement;
    this.inputType = this.resolveAccessoryInputType(field);
    this.applyFieldRestrictions(field);
    // Keep the mirror in sync while the user types in the original control.
    field.addEventListener('input', this.onFieldInput);
    field.addEventListener('compositionend', this.onFieldInput);
    this.pullDraftFromField();
  }

  private applyFieldRestrictions(field: FormField): void {
    this.maxLength = this.readPositiveLimit(field.maxLength, field.getAttribute('maxlength'));
    this.minLength = this.readPositiveLimit(field.minLength, field.getAttribute('minlength'));
    this.required = !!field.required || field.getAttribute('aria-required') === 'true';
    const pattern = field instanceof HTMLInputElement
      ? (field.getAttribute('pattern') || field.pattern || '')
      : '';
    this.pattern = pattern.trim() ? pattern : null;
    const inputMode = field.getAttribute('inputmode');
    this.inputMode = inputMode?.trim() ? inputMode.trim() : null;
  }

  private readPositiveLimit(domValue: number, attrValue: string | null): number {
    if (typeof domValue === 'number' && domValue > 0) {
      return domValue;
    }
    const parsed = Number(attrValue);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
  }

  private unbindActiveField(): void {
    if (!this.activeField) {
      return;
    }
    this.activeField.removeEventListener('input', this.onFieldInput);
    this.activeField.removeEventListener('compositionend', this.onFieldInput);
    this.activeField = null;
  }

  private pullDraftFromField(): void {
    if (!this.activeField) {
      return;
    }
    const next = this.activeField.value ?? '';
    const apply = () => {
      this.syncingFromField = true;
      this.draft = next;
      this.syncingFromField = false;
      this.cdr.detectChanges();
    };
    // DOM `input` fires outside Angular; force the accessory view to catch up.
    if (NgZone.isInAngularZone()) {
      apply();
    } else {
      this.zone.run(apply);
    }
  }

  private writeValueToField(field: FormField, value: string): void {
    if (field.value === value) {
      return;
    }
    field.value = value;
    // Prefer InputEvent so Angular ControlValueAccessors and native listeners see a real edit.
    try {
      field.dispatchEvent(
        new InputEvent('input', {
          bubbles: true,
          cancelable: true,
          data: value,
          inputType: 'insertText'
        })
      );
    } catch {
      field.dispatchEvent(new Event('input', { bubbles: true }));
    }
  }

  private resolveAccessoryInputType(field: FormField): string {
    if (!(field instanceof HTMLInputElement)) {
      return 'text';
    }
    const type = (field.type || field.getAttribute('type') || 'text').toLowerCase();
    return MIRROR_INPUT_TYPES.has(type) ? type : 'text';
  }

  private syncVisibility(): void {
    const inset = readAppliedKeyboardInset();
    const nextVisible = !!this.activeField && inset > 40;
    const apply = () => {
      this.visible = nextVisible;
      if (this.visible && this.activeField) {
        this.scrollFieldIntoView(this.activeField);
      }
      this.cdr.markForCheck();
    };
    if (NgZone.isInAngularZone()) {
      apply();
    } else {
      this.zone.run(apply);
    }
  }

  private hide(): void {
    this.unbindActiveField();
    this.visible = false;
    this.draft = '';
    this.multiline = false;
    this.inputType = 'text';
    this.maxLength = 0;
    this.minLength = 0;
    this.required = false;
    this.pattern = null;
    this.inputMode = null;
    this.cdr.markForCheck();
  }

  private isAccessoryEligible(field: FormField): boolean {
    if (field.disabled || field.readOnly) {
      return false;
    }
    if (field.closest(SKIP_SELECTOR)) {
      return false;
    }
    const type = (field.getAttribute('type') || 'text').toLowerCase();
    if (field instanceof HTMLInputElement) {
      const blocked = new Set([
        'button', 'checkbox', 'color', 'file', 'hidden', 'image',
        'radio', 'range', 'reset', 'submit'
      ]);
      if (blocked.has(type)) {
        return false;
      }
    }
    return true;
  }

  private scrollFieldIntoView(field: FormField): void {
    requestAnimationFrame(() => {
      const inset = readAppliedKeyboardInset();
      const accessoryHeight = 56;
      const rect = field.getBoundingClientRect();
      const viewportBottom = window.innerHeight - inset - accessoryHeight - 12;
      if (rect.bottom > viewportBottom || rect.top < 12) {
        field.scrollIntoView({ block: 'center', inline: 'nearest', behavior: 'smooth' });
      }
    });
  }
}
