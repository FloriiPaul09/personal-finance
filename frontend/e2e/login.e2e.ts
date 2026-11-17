import { expect, test } from '@playwright/test';
import { E2E_USER, UNKNOWN_EMAIL } from './support/env';
import { expectNoPageScroll, signIn } from './support/pages';

// Critical flow: login on a phone (docs/architecture/testing-strategy.md#minimum-required).
// Phone only: every login counts against the API's rate limit.
test.describe('login', () => {
  test.use({ storageState: { cookies: [], origins: [] } });
  test.skip(({ isMobile }) => !isMobile, 'The login flow runs on the phone project');

  test('opens the login page for a protected page, then comes back to it', async ({ page }) => {
    await page.goto('/accounts');

    await expect(page).toHaveURL('/login?returnUrl=%2Faccounts');
    await expect(page).toHaveTitle('Accedi · Personal Finance');
    await expect(page.getByRole('checkbox', { name: 'Resta connesso' })).toBeChecked();
    await expectNoPageScroll(page);

    await signIn(page, E2E_USER.email, E2E_USER.password);

    await expect(page).toHaveURL('/accounts');
    await expect(page.getByRole('heading', { level: 1, name: 'Conti' })).toBeVisible();
  });

  test('says when email or password are wrong, without locking the real user', async ({ page }) => {
    await page.goto('/login');

    await signIn(page, UNKNOWN_EMAIL, 'not-the-password');

    await expect(page.getByRole('alert')).toHaveText('Email o password non corretti.');
    await expect(page).toHaveURL('/login');
  });

  test('checks the form before calling the API', async ({ page }) => {
    await page.goto('/login');
    let loginCalls = 0;
    page.on('request', (request) => {
      if (request.url().endsWith('/api/auth/login')) loginCalls++;
    });

    await page.getByRole('button', { name: 'Accedi' }).click();

    await expect(page.getByText("Inserisci l'email.")).toBeVisible();
    await expect(page.getByText('Inserisci la password.')).toBeVisible();
    expect(loginCalls).toBe(0);
  });
});
