import { AbstractControl } from '@angular/forms';

/** True when a control should expose aria-invalid / error messaging. */
export function isControlInvalidForA11y(control: AbstractControl | null | undefined): boolean {
  return !!control && control.invalid && (control.touched || control.dirty);
}

export function controlErrorId(fieldId: string): string {
  return `${fieldId}-error`;
}

export function controlHintId(fieldId: string): string {
  return `${fieldId}-hint`;
}
