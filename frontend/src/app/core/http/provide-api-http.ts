import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
  withXsrfConfiguration,
} from '@angular/common/http';
import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { sessionInterceptor } from '../auth/session-interceptor';
import { apiErrorInterceptor } from './api-error-interceptor';

/** Endpoint that sets the antiforgery cookies (docs/backend/security.md#csrf). */
export const ANTIFORGERY_URL = '/api/auth/antiforgery';
/** The app waits at most this long for the token before it starts anyway. */
export const ANTIFORGERY_TIMEOUT_MS = 5000;

/**
 * HTTP for the whole app:
 * - antiforgery with Angular's built-in support and the names the API uses: the `XSRF-TOKEN`
 *   cookie is copied into the `X-XSRF-TOKEN` header of every POST / PUT / PATCH / DELETE to the
 *   same origin. Angular reads the cookie at each request, so the new token set by login and
 *   logout is picked up without code;
 * - every API error becomes an `ApiError` (`apiErrorInterceptor`); a `401` on an ordinary request
 *   ends the session and opens the login page (`sessionInterceptor`);
 * - the antiforgery cookie is requested once at startup, before the first page.
 */
export function provideApiHttp(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideHttpClient(
      // Outer first: the session interceptor sees the ApiError made by the inner one.
      withInterceptors([sessionInterceptor, apiErrorInterceptor]),
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    provideAppInitializer(() => requestAntiforgeryToken(inject(HttpClient))),
  ]);
}

/**
 * Asks the API for the antiforgery cookie. Never fails and never waits more than
 * `ANTIFORGERY_TIMEOUT_MS`: offline (the PWA opens without network) or with the API down,
 * the app still starts and shows its pages; a write sent without a token is refused with
 * `auth.antiforgery_invalid`, whose message asks to reload. Resolves `true` when the cookie is set.
 */
export async function requestAntiforgeryToken(http: HttpClient): Promise<boolean> {
  try {
    await firstValueFrom(http.get(ANTIFORGERY_URL).pipe(timeout(ANTIFORGERY_TIMEOUT_MS)));
    return true;
  } catch (error) {
    console.warn('Antiforgery token not available; writes will fail until a reload.', error);
    return false;
  }
}
