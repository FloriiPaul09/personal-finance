import { defineConfig, devices } from '@playwright/test';
import { AUTH_FILE, BASE_URL } from './e2e/support/env';

const CI = !!process.env['CI'];

/**
 * End-to-end tests of the critical flows on the full stack (docs/frontend/testing.md#end-to-end).
 * Needs PostgreSQL running (`docker compose up -d db`); the API and the dev server are started
 * here when they are not already running.
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  fullyParallel: true,
  forbidOnly: CI,
  retries: CI ? 1 : 0,
  reporter: CI
    ? [['list'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'on-failure' }]],
  use: {
    baseURL: BASE_URL,
    locale: 'it-IT',
    timezoneId: 'Europe/Rome',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    // Signs in once and saves the session for the other projects: the API limits logins
    // (10 per minute per address, lockout after 5 wrong passwords).
    { name: 'setup', testMatch: '**/auth.setup.ts' },
    {
      name: 'phone',
      use: { ...devices['Pixel 7'], storageState: AUTH_FILE },
      dependencies: ['setup'],
    },
    {
      name: 'desktop',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1280, height: 720 },
        storageState: AUTH_FILE,
      },
      dependencies: ['setup'],
    },
  ],
  webServer: [
    {
      command: 'dotnet run --project ../backend/src/Api --launch-profile http',
      url: 'http://localhost:5080/api/health',
      reuseExistingServer: !CI,
      timeout: 180_000,
    },
    {
      command: 'pnpm start',
      url: BASE_URL,
      reuseExistingServer: !CI,
      timeout: 180_000,
    },
  ],
});
