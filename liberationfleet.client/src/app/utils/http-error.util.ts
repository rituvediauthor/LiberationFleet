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
