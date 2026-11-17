import { expect, test } from '@playwright/test';
import { Account, TestApi } from './support/api';

// Critical flow: a transfer between two accounts, with both balances checked, then deleted.
test.describe('transfers', () => {
  let api: TestApi;
  let from: Account;
  let to: Account;

  test.beforeEach(async ({ page }, info) => {
    await page.goto('/dashboard');
    api = await TestApi.of(page);
    const stamp = `E2E ${info.project.name} ${Date.now()}`;
    from = await api.createAccount(`${stamp} origine`, 1000);
    to = await api.createAccount(`${stamp} destinazione`, 0);
  });

  test.afterEach(async () => {
    await api.deleteAccount(from.id);
    await api.deleteAccount(to.id);
  });

  test('moves money between two accounts and back when deleted', async ({ page }) => {
    await page.goto('/transactions/new');
    await page.getByTestId('kind-transfer').click();
    await expect(page).toHaveURL('/transfers/new');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Nuovo trasferimento');

    await page.getByLabel('Importo').fill('300');
    await page.getByRole('combobox', { name: 'Dal conto', exact: true }).click();
    await page.getByRole('option', { name: new RegExp(from.name) }).click();
    await page.getByRole('combobox', { name: 'Al conto', exact: true }).click();
    await page.getByRole('option', { name: new RegExp(to.name) }).click();
    await page.getByLabel('Descrizione').fill('Risparmio');
    await page.getByTestId('save-transfer').click();

    // The form replaced /transactions/new, the first page loaded: nothing of the app behind,
    // so saving opens the list instead of going back to the page before the app.
    await expect(page.getByText('Trasferimento salvato.')).toBeVisible();
    await expect(page).toHaveURL('/transactions');
    await expect.poll(async () => (await api.account(from.id)).balance).toBe(700);
    await expect.poll(async () => (await api.account(to.id)).balance).toBe(300);

    // Both legs are in the list; a leg opens its transfer.
    await page.goto(`/transactions?kind=transfer&accountId=${to.id}`);
    const leg = page.getByTestId('movement-row');
    await expect(leg).toHaveCount(1);
    await expect(leg).toContainText('Trasferimento in entrata');
    await expect(leg).toContainText('+300,00');
    await leg.getByRole('link').click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Modifica trasferimento');

    await page.getByTestId('delete-transfer').click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Elimina' }).click();
    await expect(page.getByText('Trasferimento eliminato.')).toBeVisible();
    await expect.poll(async () => (await api.account(from.id)).balance).toBe(1000);
    await expect.poll(async () => (await api.account(to.id)).balance).toBe(0);
  });
});
