import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
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

/**
 * Native form accessory: mirrors the focused field above the keyboard and
 * offers a checkmark control to dismiss (done with this field, not submit).
 */
@Component({
  selector: 'app-form-keyboard-accessory',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './form-keyboard-accessory.component.html',
  styleUrl: './form-keyboard-accessory.component.css'
})
export class FormKeyboardAccessoryComponent implements OnInit, OnDestroy {
  visible = false;
  draft = '';
  multiline = false;

  private activeField: FormField | null = null;
  private syncingFromField = false;
  private readonly onFocusIn = (event: FocusEvent) => this.handleFocus(event);
  private readonly onFocusOut = () => this.scheduleHideIfBlurred();
  private readonly onKeyboardInset = () => this.syncVisibility();
  private readonly onFieldInput = () => this.pullDraftFromField();
  private hideTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    if (typeof document === 'undefined' || !isNativeApp()) {
      return;
    }
    document.addEventListener('focusin', this.onFocusIn, true);
    document.addEventListener('focusout', this.onFocusOut, true);
    window.addEventListener(keyboardInsetChangeEventName, this.onKeyboardInset);
  }

  ngOnDestroy(): void {
    if (typeof document === 'undefined') {
      return;
    }
    document.removeEventListener('focusin', this.onFocusIn, true);
    document.removeEventListener('focusout', this.onFocusOut, true);
    window.removeEventListener(keyboardInsetChangeEventName, this.onKeyboardInset);
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
    }
  }

  onDraftChange(value: string): void {
    this.draft = value;
    if (!this.activeField || this.syncingFromField) {
      return;
    }
    this.activeField.value = value;
    this.activeField.dispatchEvent(new Event('input', { bubbles: true }));
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
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
      this.hideTimer = null;
    }
    this.bindField(target);
    this.syncVisibility();
    this.scrollFieldIntoView(target);
  }

  private scheduleHideIfBlurred(): void {
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
    }
    this.hideTimer = setTimeout(() => {
      this.hideTimer = null;
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
          return;
        }
      }
      this.hide();
    }, 40);
  }

  private bindField(field: FormField): void {
    if (this.activeField && this.activeField !== field) {
      this.activeField.removeEventListener('input', this.onFieldInput);
    }
    this.activeField = field;
    this.multiline = field instanceof HTMLTextAreaElement;
    field.addEventListener('input', this.onFieldInput);
    this.pullDraftFromField();
  }

  private pullDraftFromField(): void {
    if (!this.activeField) {
      return;
    }
    this.syncingFromField = true;
    this.draft = this.activeField.value ?? '';
    this.syncingFromField = false;
  }

  private syncVisibility(): void {
    const inset = readAppliedKeyboardInset();
    this.visible = !!this.activeField && inset > 40;
    if (this.visible && this.activeField) {
      this.scrollFieldIntoView(this.activeField);
    }
  }

  private hide(): void {
    if (this.activeField) {
      this.activeField.removeEventListener('input', this.onFieldInput);
    }
    this.visible = false;
    this.activeField = null;
    this.draft = '';
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
