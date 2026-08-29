import { HttpErrorResponse } from '@angular/common/http';

// The API returns { "message": "..." } for every failure, so that is what
// we read first. The rest are fallbacks for network errors and anything
// still coming back as plain text.
export function apiErrorMessage(error: unknown, fallback: string): string {

  const httpError = error as HttpErrorResponse;
  const body = httpError?.error;

  if (body && typeof body === 'object') {
    const message = body.message ?? body.Message ?? body.title ?? body.detail;

    if (typeof message === 'string' && message.trim()) {
      return message.trim();
    }
  }

  if (typeof body === 'string' && body.trim()) {
    return body.trim();
  }

  if (httpError?.status === 0) {
    return 'Cannot reach the server. Please check your connection.';
  }

  return fallback;
}
