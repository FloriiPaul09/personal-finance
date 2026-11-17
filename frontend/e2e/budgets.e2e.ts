import { expect, test } from '@playwright/test';
import { Account, Category, TestApi } from './support/api';

// A budget on the real stack: set from the page, spending counted by the API, stopped from the
// month with "Annulla". Each run works on its own account and category and deletes them at the end.
test.describe('budgets', () => {
  let api: TestApi;
  let account: Account;
  let category: Category;

  // The month and the day of the browser (playwright.config.ts: Europe/Rome).
  const now = new Date();
  const today = now.toLocaleDateString('sv-SE', { timeZone: 'Europe/Rome' });
  const month = today.slice(0, 7);
  const monthLabel = now.toLocaleDateString('it-IT', {
    month: 'long',
    year: 'numeric',
    timeZone: 'Europe/Rome',
  });

  test.beforeEach(async ({ page }, info) => {
    await page.goto('/dashboard');
    api = await TestApi.of(page);
    const stamp = `E2E ${info.project.name} ${Date.now()}`;
    account = await api.createAccount(stamp, 1000);
    category = await api.createCategory(stamp);
  });

  test.afterEach(async () => {
    await api.deleteAccount(account.id); // its expenses first: the category is then unused
    await api.deleteCategory(category.id);
  });

  test('sets a budget, shows what is spent, then stops it from the month', async ({ page }) => {
    await page.goto(`/budgets?month=${month}`);
    await page.getByTestId('new-budget').click();
    await expect(page).toHaveURL(`/budgets/new?month=${month}`);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Nuovo budget');

    await expect(page.getByLabel('Limite mensile')).toBeFocused();
    await page.keyboard.type('100');
    await page.getByRole('combobox', { name: 'Categoria', exact: true }).click();
    await page.getByRole('option', { name: category.name }).click();
    await page.getByTestId('save-budget').click();

    await expect(page).toHaveURL(`/budgets?month=${month}`);
    await expect(page.getByText('Budget salvato.')).toBeVisible();
    const row = page.getByTestId('budget-row').filter({ hasText: category.name });
    await expect(row.getByTestId('budget-note')).toHaveText('Restano 100,00 €');

    // An expense of the month counts: 95 of 100 is past the 90% warning.
    await api.createExpense(account.id, category.id, 95, today);
    await page.reload();
    await expect(row.getByTestId('budget-note')).toHaveText('Restano 5,00 €');
    await expect(row.locator('app-budget-progress')).toHaveAttribute('data-state', 'warning');

    // The row opens the expenses of the category in that month.
    await row.getByRole('link').click();
    await expect(page).toHaveURL(new RegExp(`/transactions\\?categoryId=${category.id}`));
    await expect(page.getByTestId('movement-row')).toHaveCount(1);
    await page.goBack();

    // Stop from the month, then "Annulla" puts it back.
    await page.getByRole('button', { name: `Azioni per ${category.name}`, exact: true }).click();
    await page.getByRole('menuitem', { name: `Ferma da ${monthLabel}` }).click();
    await expect(
      page.getByText(`Budget di ${category.name} fermato da ${monthLabel}.`),
    ).toBeVisible();
    await expect(row).toHaveCount(0);
    await page.getByRole('button', { name: 'Annulla' }).click();
    await expect(row.getByTestId('budget-note')).toHaveText('Restano 5,00 €');

    // Its page shows the history: one entry, the limit set at the start.
    await page.getByRole('button', { name: `Azioni per ${category.name}`, exact: true }).click();
    await page.getByRole('menuitem', { name: 'Modifica' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(`Budget di ${category.name}`);
    await expect(page.getByTestId('budget-entry')).toHaveCount(1);
    await expect(page.getByTestId('budget-entry')).toContainText('100,00 €');
  });
});
