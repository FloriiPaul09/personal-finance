/** Address of the app (dev server, which proxies /api to the API). */
export const BASE_URL = process.env['E2E_BASE_URL'] ?? 'http://localhost:4200';

/**
 * The user the tests sign in with: by default the development initial user of
 * appsettings.Development.json (docs/devops/configuration.md). Override with E2E_EMAIL / E2E_PASSWORD.
 */
export const E2E_USER = {
  email: process.env['E2E_EMAIL'] ?? 'dev@example.com',
  password: process.env['E2E_PASSWORD'] ?? 'dev-only-password',
};

/** Session saved by auth.setup.ts and reused by every test (git-ignored). */
export const AUTH_FILE = 'e2e/.auth/user.json';

/** An address no user has: wrong logins use it, so the real user is never locked out. */
export const UNKNOWN_EMAIL = 'nobody@example.invalid';
