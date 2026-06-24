/**
 * Returns true when the JWT is missing, malformed, or past its exp claim.
 */
export function isJwtExpired(token: string): boolean {
  try {
    const parts = token.split('.');
    if (parts.length < 2) {
      return true;
    }

    const payloadJson = atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'));
    const payload = JSON.parse(payloadJson) as { exp?: number };
    if (typeof payload.exp !== 'number') {
      return true;
    }

    return Date.now() >= payload.exp * 1000;
  } catch {
    return true;
  }
}
