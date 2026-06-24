import { isJwtExpired } from './jwt.util';

describe('isJwtExpired', () => {
  function createToken(expSeconds: number): string {
    const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
    const payload = btoa(JSON.stringify({ exp: expSeconds }));
    return `${header}.${payload}.signature`;
  }

  it('returns true for expired tokens', () => {
    const expired = Math.floor(Date.now() / 1000) - 60;
    expect(isJwtExpired(createToken(expired))).toBeTrue();
  });

  it('returns false for valid tokens', () => {
    const valid = Math.floor(Date.now() / 1000) + 3600;
    expect(isJwtExpired(createToken(valid))).toBeFalse();
  });

  it('returns true for malformed tokens', () => {
    expect(isJwtExpired('not-a-jwt')).toBeTrue();
  });
});
