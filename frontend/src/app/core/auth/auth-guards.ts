import { inject } from '@angular/core';
import { CanMatchFn, Router, UrlSegment } from '@angular/router';
import { LOGIN_PATH, safeReturnUrl } from './return-url';
import { SessionStore } from './session-store';

/** Page shown when the API cannot be reached at startup. */
export const UNAVAILABLE_PATH = '/unavailable';

/**
 * Pages of the shell need a session. Without one the user goes to the login page, which
 * brings them back here afterwards; when the API cannot be reached, to a page that retries.
 */
export const authGuard: CanMatchFn = async (_route, segments) => {
  const router = inject(Router);
  const status = await inject(SessionStore).ensureLoaded();
  if (status === 'authenticated') {
    return true;
  }
  const returnUrl = attemptedUrl(router, segments);
  const target = status === 'unavailable' ? UNAVAILABLE_PATH : LOGIN_PATH;
  return router.createUrlTree([target], { queryParams: { returnUrl } });
};

/** The login page is for signed-out users: a signed-in user goes where they were heading. */
export const guestGuard: CanMatchFn = async () => {
  const router = inject(Router);
  const status = await inject(SessionStore).ensureLoaded();
  if (status !== 'authenticated') {
    return true;
  }
  const returnUrl = router.currentNavigation()?.extractedUrl.queryParams['returnUrl'] as
    string | undefined;
  return router.parseUrl(safeReturnUrl(returnUrl));
};

function attemptedUrl(router: Router, segments: UrlSegment[]): string {
  const navigation = router.currentNavigation();
  return navigation
    ? router.serializeUrl(navigation.extractedUrl)
    : `/${segments.map((s) => s.path).join('/')}`;
}
