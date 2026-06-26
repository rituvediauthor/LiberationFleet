import { FormGroup } from '@angular/forms';

export function valuesEqual(left: unknown, right: unknown): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

export function formValuesChanged(form: FormGroup, initialValues: unknown): boolean {
  return !valuesEqual(form.getRawValue(), initialValues);
}

export function isSaveActionDisabled(options: {
  form: FormGroup | null;
  initialValues: unknown;
  isLoading?: boolean;
  isSaving?: boolean;
  extraInvalid?: boolean;
}): boolean {
  if (!options.form || options.isLoading || options.isSaving) {
    return true;
  }

  if (options.form.invalid || options.extraInvalid) {
    return true;
  }

  return !formValuesChanged(options.form, options.initialValues);
}
