import { expect, test } from '@playwright/test';
import {
  expectClickable,
  expectNoPageScroll,
  expectTitleClearOfAccountButton,
} from './support/pages';

/** Pages with a form: on phones the floating button hides there (it covered "Salva"). */
const FORM_PAGES = new Set([
  '/transactions/new',
  '/transactions/new?kind=income',
  '/transfers/new',
  '/accounts/new',
  '/categories/new',
]);

const PAGES = [
  '/dashboard',
  '/transactions',
  '/transactions/new',
  '/transactions/new?kind=income',
  '/transfers/new',
  '/budgets',
  '/accounts',
  '/accounts/new',
  '/categories',
  '/categories/new',
  '/settings',
];

// Regressions of the layout bugs found in review (docs/frontend/testing.md).
test.describe('layout', () => {
  for (const url of PAGES) {
    test(`${url} never scrolls the whole page and every navigation item can be tapped`, async ({
      page,
      isMobile,
    }) => {
      await page.goto(url);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();

      await expectNoPageScroll(page);
      await expectClickable(
        page,
        '.nav-item, [data-testid="new-expense"], [data-testid="settings"], button[type="submit"]',
      );
      await expectTitleClearOfAccountButton(page);
      // Block 6: on a phone the floating button covered the form's "Salva".
      const fab = page.getByTestId('new-expense');
      await (isMobile && FORM_PAGES.has(url)
        ? expect(fab).toBeHidden()
        : expect(fab).toBeVisible());
    });
  }

  test('keeps the account button readable when the page scrolls under it', async ({
    page,
    isMobile,
  }) => {
    test.skip(!isMobile, 'The button floats over the page only on phones');
    await page.goto('/settings');

    // Block 4: without its own surface, text scrolling under the button mixed with the icon.
    const background = await page
      .getByTestId('settings')
      .evaluate((button) => getComputedStyle(button).backgroundColor);
    expect(background).not.toBe('rgba(0, 0, 0, 0)');
  });

  test('shows an error snackbar above the navigation bar and the new expense button', async ({
    page,
    isMobile,
  }) => {
    test.skip(!isMobile, 'The bar is at the bottom only on phones');
    await page.goto('/settings');
    // The API stops answering (the proxy replies 502 with an empty body).
    await page.route('**/api/auth/change-password', (route) =>
      route.fulfill({ status: 502, contentType: 'text/plain', body: '' }),
    );

    await page.getByLabel('Password attuale').fill('any-password');
    await page.getByLabel('Nuova password', { exact: true }).fill('a-new-long-password');
    await page.getByLabel('Ripeti la nuova password').fill('a-new-long-password');
    await page.getByRole('button', { name: 'Aggiorna password' }).click();

    const snackbar = page.locator('.mat-mdc-snack-bar-container');
    await expect(snackbar).toContainText('Il server non risponde. Riprova tra poco.');
    const bar = (await snackbar.boundingBox())!;
    const button = (await page.getByTestId('new-expense').boundingBox())!;
    const nav = (await page
      .getByRole('navigation', { name: 'Navigazione principale' })
      .boundingBox())!;
    expect(bar.y + bar.height).toBeLessThanOrEqual(button.y);
    expect(bar.y + bar.height).toBeLessThanOrEqual(nav.y);
  });
});
