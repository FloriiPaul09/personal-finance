import { expect, test } from '@playwright/test';

test.describe('session', () => {
  test('sends a signed-in user away from the login page', async ({ page }) => {
    await page.goto('/login?returnUrl=%2Fbudgets');

    await expect(page).toHaveURL('/budgets');
  });

  test('logs out from the settings and protects the pages again', async ({ page }) => {
    await page.goto('/dashboard');
    await page.getByRole('link', { name: 'Impostazioni' }).click();
    await expect(page.getByTestId('account-email')).not.toBeEmpty();

    await page.getByRole('button', { name: 'Esci' }).click();

    await expect(page).toHaveURL('/login');
    await page.goto('/dashboard');
    await expect(page).toHaveURL('/login?returnUrl=%2Fdashboard');
  });

  test('opens the login page when the session ends, and comes back afterwards', async ({
    page,
  }) => {
    await page.goto('/settings');
    await expect(page.getByRole('heading', { level: 1, name: 'Impostazioni' })).toBeVisible();
    // The session ends while the page is open (expired, or logged out elsewhere).
    await page.context().clearCookies({ name: 'pf_session' });

    await page.getByLabel('Password attuale').fill('any-password');
    await page.getByLabel('Nuova password', { exact: true }).fill('a-new-long-password');
    await page.getByLabel('Ripeti la nuova password').fill('a-new-long-password');
    await page.getByRole('button', { name: 'Aggiorna password' }).click();

    await expect(page).toHaveURL('/login?returnUrl=%2Fsettings');
  });
});
