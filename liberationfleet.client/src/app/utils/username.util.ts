import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

export const USERNAME_MIN_LENGTH = 3;
export const USERNAME_MAX_LENGTH = 30;
export const USERNAME_PATTERN = /^[A-Za-z0-9]+$/;

const RESERVED_SUBSTRINGS = [
  'admin',
  'administrator',
  'moderator',
  'root',
  'system',
  'support',
  'owner',
  'sysadmin',
  'superuser',
  'sql',
  'select',
  'insert',
  'update',
  'delete',
  'drop',
  'truncate',
  'null'
];

/** Rejects reserved substrings (case-insensitive). Mirrors the server UsernamePolicy. */
export function usernameReservedValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = (control.value ?? '').toString().toLowerCase();
    if (!value) {
      return null;
    }
    return RESERVED_SUBSTRINGS.some(reserved => value.includes(reserved))
      ? { usernameReserved: true }
      : null;
  };
}

export function usernameValidators(): ValidatorFn[] {
  return [
    Validators.required,
    Validators.minLength(USERNAME_MIN_LENGTH),
    Validators.maxLength(USERNAME_MAX_LENGTH),
    Validators.pattern(USERNAME_PATTERN),
    usernameReservedValidator()
  ];
}
