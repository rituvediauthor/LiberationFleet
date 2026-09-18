import { FormGroup } from '@angular/forms';

/**
 * Normalize form snapshots so browser quirks do not break dirty detection
 * (e.g. number inputs as "2" vs 2, key order, null vs undefined).
 */
export function normalizeForCompare(value: unknown): unknown {
  if (value === null || value === undefined) {
    return null;
  }

  if (typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }

  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (trimmed !== '' && /^-?\d+(\.\d+)?$/.test(trimmed)) {
      const asNumber = Number(trimmed);
      if (Number.isFinite(asNumber)) {
        return asNumber;
      }
    }
    return trimmed;
  }

  if (Array.isArray(value)) {
    return value.map(item => normalizeForCompare(item));
  }

  if (typeof value === 'object') {
    const record = value as Record<string, unknown>;
    const normalized: Record<string, unknown> = {};
    for (const key of Object.keys(record).sort()) {
      normalized[key] = normalizeForCompare(record[key]);
    }
    return normalized;
  }

  return value;
}

export function valuesEqual(left: unknown, right: unknown): boolean {
  return JSON.stringify(normalizeForCompare(left)) === JSON.stringify(normalizeForCompare(right));
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
