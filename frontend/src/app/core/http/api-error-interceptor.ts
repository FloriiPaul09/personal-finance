import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { toApiError } from '../api/api-error';

/** Requests to the API: same origin, under `/api` (proxied in development, nginx in containers). */
export function isApiRequest(url: string): boolean {
  return url === '/api' || url.startsWith('/api/') || url.startsWith('/api?');
}

/**
 * Every failed API request fails with an `ApiError` instead of an `HttpErrorResponse`,
 * so stores, `httpResource` errors and the global handler all see the same shape.
 */
export const apiErrorInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request.url)) {
    return next(request);
  }
  return next(request).pipe(
    catchError((error: unknown) =>
      throwError(() => (error instanceof HttpErrorResponse ? toApiError(error) : error)),
    ),
  );
};
