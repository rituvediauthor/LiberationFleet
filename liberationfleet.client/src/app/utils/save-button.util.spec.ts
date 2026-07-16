import { FormBuilder, Validators } from '@angular/forms';
import { formValuesChanged, isSaveActionDisabled, valuesEqual } from './save-button.util';

describe('save-button.util', () => {
  const fb = new FormBuilder();

  it('valuesEqual compares JSON snapshots', () => {
    expect(valuesEqual({ a: 1 }, { a: 1 })).toBeTrue();
    expect(valuesEqual({ a: 1 }, { a: 2 })).toBeFalse();
  });

  it('formValuesChanged detects edits to raw values', () => {
    const form = fb.group({ name: ['Crew'] });
    expect(formValuesChanged(form, { name: 'Crew' })).toBeFalse();
    form.patchValue({ name: 'Fleet' });
    expect(formValuesChanged(form, { name: 'Crew' })).toBeTrue();
  });

  it('isSaveActionDisabled requires a valid changed form', () => {
    const form = fb.group({ name: ['Crew', Validators.required] });
    const initial = { name: 'Crew' };

    expect(isSaveActionDisabled({ form, initialValues: initial })).toBeTrue();

    form.patchValue({ name: 'Updated' });
    expect(isSaveActionDisabled({ form, initialValues: initial })).toBeFalse();

    form.patchValue({ name: '' });
    expect(isSaveActionDisabled({ form, initialValues: initial })).toBeTrue();

    form.patchValue({ name: 'Updated' });
    expect(isSaveActionDisabled({ form, initialValues: initial, isSaving: true })).toBeTrue();
    expect(isSaveActionDisabled({ form, initialValues: initial, extraInvalid: true })).toBeTrue();
    expect(isSaveActionDisabled({ form: null, initialValues: initial })).toBeTrue();
  });
});
