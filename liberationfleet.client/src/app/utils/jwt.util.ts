/**
 * Returns true when the JWT is missing, malformed, or past its exp claim.
 */
export function isJwtExpired(token: string): boolean {
  try {
    const payload = parseJwtPayload(token);
    if (!payload || typeof payload['exp'] !== 'number') {
      return true;
    }

    return Date.now() >= payload['exp'] * 1000;
  } catch {
    return true;
  }
}

export function getUserIdFromToken(token: string): number | null {
  try {
    const payload = parseJwtPayload(token);
    if (!payload) {
      return null;
    }

    const rawId = payload['sub']
      ?? payload['nameid']
      ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];

    const userId = Number(rawId);
    return Number.isFinite(userId) ? userId : null;
  } catch {
    return null;
  }
}

function parseJwtPayload(token: string): Record<string, unknown> | null {
  const parts = token.split('.');
  if (parts.length < 2) {
    return null;
  }

  const payloadJson = atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'));
  return JSON.parse(payloadJson) as Record<string, unknown>;
}
