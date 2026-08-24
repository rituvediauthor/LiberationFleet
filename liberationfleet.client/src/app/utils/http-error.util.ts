/**
 * Extracts a human-readable message from an HttpClient error.
 *
 * The server returns `{ success: false, message: '...' }` bodies with non-2xx
 * status codes, which HttpClient surfaces as an HttpErrorResponse whose own
 * `.message` is the opaque "Http failure response for <url>" string. The useful
 * text lives in `.error.message`, so prefer that before falling back.
 */
export function extractHttpErrorMessage(error: unknown, fallback: string): string {
  if (error && typeof error === 'object') {
    const err = error as { error?: { message?: unknown }; message?: unknown };
    const bodyMessage = err.error?.message;
    if (typeof bodyMessage === 'string' && bodyMessage.trim()) {
      return bodyMessage;
    }
    if (typeof err.message === 'string' && err.message.trim() && !err.message.startsWith('Http failure')) {
      return err.message;
    }
  }
  return fallback;
}

/**
 * True when the failure is likely a client connectivity / transport problem
 * (offline, DNS, CORS blocked as status 0, aborted request) rather than an
 * application/API error.
 */
export function isConnectivityError(error: unknown): boolean {
  if (typeof navigator !== 'undefined' && navigator.onLine === false) {
    return true;
  }

  if (!error || typeof error !== 'object') {
    return false;
  }

  const err = error as {
    status?: unknown;
    name?: unknown;
    message?: unknown;
    error?: unknown;
  };

  if (err.status === 0) {
    return true;
  }

  if (typeof ProgressEvent !== 'undefined' && err.error instanceof ProgressEvent) {
    return true;
  }

  if (typeof err.message === 'string') {
    const message = err.message.toLowerCase();
    if (
      message.includes('failed to fetch')
      || message.includes('networkerror')
      || message.includes('network request failed')
      || message.includes('net::err_')
      || message.includes('load failed')
    ) {
      return true;
    }
  }

  return false;
}

/** User-facing copy when a load failed because of the device's connection. */
export const CONNECTIVITY_ERROR_MESSAGE =
  'Unable to reach Liberation Fleet. Check your internet connection — this is a communication problem on your device, not an app bug.';

/**
 * Prefers a connectivity explanation when the device looks offline / unreachable,
 * otherwise falls back to the server or generic message.
 */
export function describeLoadError(
  error: unknown,
  fallback: string,
  connectivityMessage: string = CONNECTIVITY_ERROR_MESSAGE
): string {
  if (isConnectivityError(error)) {
    return connectivityMessage;
  }
  return extractHttpErrorMessage(error, fallback);
}
