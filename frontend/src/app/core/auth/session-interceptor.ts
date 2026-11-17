import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { ApiError } from '../api/api-error';
import { LOGIN_PATH, loginUrl } from './return-url';
import { SessionStore } from './session-store';

/** Requests whose `401` is an expected answer, not an expired session. */
const SESSION_CHECKS = ['/api/auth/me', '/api/auth/login'];

/**
 * A `401` (`auth.required`) on any other request means the session ended (expired, password
 * changed elsewhere, logout on another tab): forget the user and open the login page, which
 * comes back to the current page afterwards. The error still reaches the caller.
 * Registered before `apiErrorInterceptor`, so it sees `ApiError`s.
 */
export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionStore);
  const router = inject(Router);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof ApiError &&
        error.status === 401 &&
        error.code === 'auth.required' &&
        !SESSION_CHECKS.includes(request.url)
      ) {
        session.markSignedOut();
        const current = router.url;
        if (current !== LOGIN_PATH && !current.startsWith(`${LOGIN_PATH}?`)) {
          void router.navigateByUrl(loginUrl(current));
        }
      }
      return throwError(() => error);
    }),
  );
};
