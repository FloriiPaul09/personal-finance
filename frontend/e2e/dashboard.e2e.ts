import { expect, test } from '@playwright/test';
import { Account, Category, TestApi } from './support/api';

// The home page on the real stack: every number comes from the API. Each run works on its own
// account in a currency of its own, so its totals are exactly its movements whatever else the
// dev user has (or other tests add at the same time), and deletes everything at the end.
test.describe('dashboard', () => {
  let api: TestApi;
  let account: Account;
  let expenses: Category;
  let income: Category;
  let currency: string;

  // The month and the day of the browser (playwright.config.ts: Europe/Rome).
  const now = new Date();
  const today = now.toLocaleDateString('sv-SE', { timeZone: 'Europe/Rome' });
  const month = today.slice(0, 7);

  test.beforeEach(async ({ page }, info) => {
    await page.goto('/dashboard');
    api = await TestApi.of(page);
    const stamp = `E2E ${info.project.name} ${Date.now()}`;
    currency = info.project.name === 'phone' ? 'CHF' : 'SEK';
    account = await api.createAccount(stamp, 1000, currency);
    expenses = await api.createCategory(stamp);
    income = await api.createCategory(`${stamp} entrate`, 'income');
    // Far over its limit, so it is among the three budgets closest to (past) their limit.
    await api.createBudget(expenses.id, month, 10);
    await api.createExpense(account.id, expenses.id, 95, today);
    await api.createIncome(account.id, income.id, 2000, today);
  });

  test.afterEach(async () => {
    await api.deleteAccount(account.id); // its movements first: the categories are then unused
    await api.deleteCategory(expenses.id); // with its budget entry
    await api.deleteCategory(income.id);
  });

  test('shows the month, its budgets, its spending and the balances', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Home');

    // Income, expenses and net of the month, in the account's currency.
    const totals = page.locator(`[data-testid="totals-row"][data-currency="${currency}"]`);
    await expect(totals.getByTestId('income')).toHaveText(`2.000,00 ${currency}`);
    await expect(totals.getByTestId('expense')).toHaveText(`95,00 ${currency}`);
    await expect(totals.getByTestId('net')).toHaveText(`+1.905,00 ${currency}`);

    // The budget, over its limit.
    const budget = page.getByTestId('budget-row').filter({ hasText: expenses.name });
    await expect(budget.locator('app-budget-progress')).toHaveAttribute('data-state', 'over');
    await expect(budget.getByTestId('budget-note')).toHaveText('Superato di 85,00 €');

    // The spending: a donut, and the category in its list.
    await expect(page.locator('app-donut-chart canvas').first()).toBeVisible();
    const spending = page.getByTestId('spending-row').filter({ hasText: expenses.name });
    await expect(spending).toContainText(`95,00 ${currency}`);

    // The account with its current balance, and the total of its currency.
    const row = page.getByTestId('account-row').filter({ hasText: account.name });
    await expect(row).toContainText(`2.905,00 ${currency}`);
    await expect(
      page.locator(`[data-testid="balance-total"][data-currency="${currency}"]`),
    ).toContainText(`2.905,00 ${currency}`);

    // The category opens its expenses of the month.
    await spending.click();
    await expect(page).toHaveURL(
      new RegExp(`/transactions\\?categoryId=${expenses.id}&month=${month}`),
    );
    await expect(page.getByTestId('movement-row')).toHaveCount(1);
    await page.goBack();
    await expect(page).toHaveURL('/dashboard');

    // The month before: no movements of the account, the same current balance.
    await page.getByRole('button', { name: 'Mese precedente' }).click();
    await expect(page).toHaveURL(/\/dashboard\?month=\d{4}-\d{2}$/);
    await expect(totals).toHaveCount(0);
    await expect(spending).toHaveCount(0);
    await expect(row).toContainText(`2.905,00 ${currency}`);
    await expect(page.getByRole('button', { name: 'Mese successivo' })).toBeEnabled();

    // Back to the current month: no month after it.
    await page.getByRole('button', { name: 'Mese corrente' }).click();
    await expect(totals.getByTestId('expense')).toHaveText(`95,00 ${currency}`);
    await expect(page.getByRole('button', { name: 'Mese successivo' })).toBeDisabled();
  });
});
