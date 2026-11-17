/** Where the app goes after login when there is no (valid) page to return to. */
export const HOME_URL = '/dashboard';
export const LOGIN_PATH = '/login';

/**
 * The page to open after login: the requested `returnUrl` when it is a path of this app,
 * otherwise the dashboard. Absolute URLs, protocol-relative ones (`//evil.example`) and
 * backslash tricks are refused, so a crafted login link can never send the user to another site.
 */
export function safeReturnUrl(value: string | null | undefined): string {
  if (
    typeof value !== 'string' ||
    !value.startsWith('/') ||
    value.startsWith('//') ||
    value.includes('\\') ||
    value === LOGIN_PATH ||
    value.startsWith(`${LOGIN_PATH}?`) ||
    value.startsWith(`${LOGIN_PATH}/`)
  ) {
    return HOME_URL;
  }
  return value;
}

/** Address of the login page that comes back to `returnUrl` afterwards. */
export function loginUrl(returnUrl: string): string {
  const target = safeReturnUrl(returnUrl);
  return target === HOME_URL ? LOGIN_PATH : `${LOGIN_PATH}?returnUrl=${encodeURIComponent(target)}`;
}
