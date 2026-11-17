import { expect, test as setup } from '@playwright/test';
import { AUTH_FILE, E2E_USER } from './support/env';
import { signIn } from './support/pages';

setup('sign in once for every test', async ({ page }) => {
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/\/login/);

  await signIn(page, E2E_USER.email, E2E_USER.password);

  await expect(page.getByRole('heading', { level: 1, name: 'Home' })).toBeVisible();
  await page.context().storageState({ path: AUTH_FILE });
});
