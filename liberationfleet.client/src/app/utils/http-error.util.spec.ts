import {
  CONNECTIVITY_ERROR_MESSAGE,
  describeLoadError,
  extractHttpErrorMessage,
  isConnectivityError,
  isRetryableLoadError
} from './http-error.util';

describe('http-error.util', () => {
  describe('extractHttpErrorMessage', () => {
    it('prefers the API body message', () => {
      expect(extractHttpErrorMessage({
        message: 'Http failure response for /api/x: 0 Unknown Error',
        error: { message: 'Nope' }
      }, 'fallback')).toBe('Nope');
    });

    it('falls back when nothing useful is present', () => {
      expect(extractHttpErrorMessage(null, 'fallback')).toBe('fallback');
    });
  });

  describe('isConnectivityError', () => {
    it('detects HttpClient status 0 failures', () => {
      expect(isConnectivityError({ status: 0, message: 'Http failure response for /api/x: 0 Unknown Error' })).toBeTrue();
    });

    it('detects ProgressEvent transport failures', () => {
      expect(isConnectivityError({ status: 0, error: new ProgressEvent('error') })).toBeTrue();
    });

    it('does not treat normal API errors as connectivity issues', () => {
      expect(isConnectivityError({ status: 500, error: { message: 'Server exploded' } })).toBeFalse();
    });
  });

  describe('isRetryableLoadError', () => {
    it('retries connectivity failures', () => {
      expect(isRetryableLoadError({ status: 0 })).toBeTrue();
    });

    it('retries migration warm-up 503s', () => {
      expect(isRetryableLoadError({
        status: 503,
        error: { message: 'Server is still applying database updates. Please retry in a moment.' }
      })).toBeTrue();
    });

    it('does not retry ordinary 500s', () => {
      expect(isRetryableLoadError({ status: 500, error: { message: 'boom' } })).toBeFalse();
    });
  });

  describe('describeLoadError', () => {
    it('returns the connectivity message for offline-style failures', () => {
      expect(describeLoadError({ status: 0 }, 'Failed to load')).toBe(CONNECTIVITY_ERROR_MESSAGE);
    });

    it('returns the API message for application failures', () => {
      expect(describeLoadError({ status: 400, error: { message: 'Bad request' } }, 'Failed to load'))
        .toBe('Bad request');
    });
  });
});
