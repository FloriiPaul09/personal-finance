import { expect, test } from '@playwright/test';
import { Account, TestApi } from './support/api';
import { expectNoPageScroll } from './support/pages';

// Critical flow: an expense entered from a phone (docs/architecture/testing-strategy.md#minimum-required),
// then the list and its filters. Each run works on its own account and deletes it at the end.
test.describe('movements', () => {
  let api: TestApi;
  let account: Account;

  test.beforeEach(async ({ page }, info) => {
    await page.goto('/dashboard');
    api = await TestApi.of(page);
    account = await api.createAccount(`E2E ${info.project.name} ${Date.now()}`, 100);
  });

  test.afterEach(async () => {
    await api.deleteAccount(account.id);
  });

  test('enters an expense from the new expense button, then finds and deletes it', async ({
    page,
  }) => {
    await page.reload(); // the new account in the lists of the app
    await page.getByTestId('new-expense').click();
    await expect(page).toHaveURL('/transactions/new');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Nuova spesa');
    await expectNoPageScroll(page);

    // The amount has the focus: the person just types.
    await expect(page.getByLabel('Importo')).toBeFocused();
    await page.keyboard.type('12,50');
    await page.getByRole('combobox', { name: 'Categoria' }).click();
    await page.getByRole('option', { name: 'Spesa', exact: true }).click();
    await page.getByRole('combobox', { name: 'Conto' }).click();
    await page.getByRole('option', { name: new RegExp(account.name) }).click();
    await page.getByTestId('save-movement').click();

    await expect(page).toHaveURL('/dashboard');
    await expect(page.getByText('Spesa salvata.')).toBeVisible();
    await expect.poll(async () => (await api.account(account.id)).balance).toBe(87.5);

    // The list, filtered on the account through the URL.
    await page.goto(`/transactions?accountId=${account.id}`);
    const row = page.getByTestId('movement-row');
    await expect(row).toHaveCount(1);
    await expect(row).toContainText('Spesa');
    await expect(row).toContainText('-12,50');
    await expect(page.getByRole('heading', { level: 2 }).first()).toHaveText('Oggi');

    // Delete it from its form, after the confirmation.
    await row.getByRole('link').click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Modifica spesa');
    await page.getByTestId('delete-movement').click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Elimina' }).click();
    await expect(page.getByText('Movimento eliminato.')).toBeVisible();
    await expect.poll(async () => (await api.account(account.id)).balance).toBe(100);
  });

  test('"Annulla" after saving removes the new expense', async ({ page }) => {
    await page.goto('/transactions/new');
    await page.getByLabel('Importo').fill('5');
    await page.getByRole('combobox', { name: 'Categoria' }).click();
    await page.getByRole('option', { name: 'Svago', exact: true }).click();
    await page.getByRole('combobox', { name: 'Conto' }).click();
    await page.getByRole('option', { name: new RegExp(account.name) }).click();
    await page.getByTestId('save-movement').click();

    await page.getByRole('button', { name: 'Annulla' }).click();
    await expect.poll(async () => (await api.account(account.id)).balance).toBe(100);
  });

  test('filters the list by text from the filter panel, in the URL', async ({ page }) => {
    await page.goto('/transactions');
    await page.getByTestId('toggle-filters').click();
    await page.getByTestId('search').fill('nessun movimento ha questo testo');
    await page.getByTestId('search').press('Enter');

    await expect(page).toHaveURL(/q=nessun/);
    await expect(page.getByText('Nessun movimento con questi filtri')).toBeVisible();
    await page.getByRole('button', { name: /Togli il filtro/ }).click();
    await expect(page).not.toHaveURL(/q=/);
  });
});
