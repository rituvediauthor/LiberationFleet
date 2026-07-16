import { FormControl } from '@angular/forms';
import { controlErrorId, controlHintId, isControlInvalidForA11y } from './a11y-form.util';

describe('a11y-form.util', () => {
  it('isControlInvalidForA11y is false for null or pristine valid controls', () => {
    expect(isControlInvalidForA11y(null)).toBeFalse();
    expect(isControlInvalidForA11y(undefined)).toBeFalse();

    const control = new FormControl('ok');
    expect(isControlInvalidForA11y(control)).toBeFalse();
  });

  it('isControlInvalidForA11y is true only when invalid and touched or dirty', () => {
    const control = new FormControl('', { validators: control => (control.value ? null : { required: true }) });
    expect(isControlInvalidForA11y(control)).toBeFalse();

    control.markAsTouched();
    expect(isControlInvalidForA11y(control)).toBeTrue();

    control.markAsUntouched();
    control.markAsDirty();
    expect(isControlInvalidForA11y(control)).toBeTrue();
  });

  it('builds stable error and hint ids', () => {
    expect(controlErrorId('email')).toBe('email-error');
    expect(controlHintId('email')).toBe('email-hint');
  });
});
