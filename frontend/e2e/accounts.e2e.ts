import { expect, test } from '@playwright/test';
import { expectNoPageScroll } from './support/pages';

// Accounts: create, edit, archive with undo, delete (block 5). Each run uses its own name and
// deletes the account at the end, so the development data stays as it was.
test.describe('accounts', () => {
  test('creates, edits, archives with undo and deletes an account', async ({ page }, info) => {
    const name = `E2E ${info.project.name} ${Date.now()}`;
    const renamed = `${name} bis`;
    const row = (text: string) => page.getByTestId('account-row').filter({ hasText: text });

    await page.goto('/accounts');
    await page.getByTestId('new-account').click();
    await expect(page).toHaveURL('/accounts/new');
    await expectNoPageScroll(page);

    await page.getByLabel('Nome').fill(name);
    await page.getByLabel('Saldo iniziale').fill('-12,5');
    await page.getByRole('button', { name: 'Crea conto' }).click();

    await expect(page).toHaveURL('/accounts');
    await expect(page.getByText('Conto creato.')).toBeVisible();
    await expect(row(name)).toContainText('-12,50');

    // Edit: the row opens the form with the account's data.
    await row(name).getByRole('link').click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Modifica conto');
    await expect(page.getByLabel('Nome')).toHaveValue(name);
    await page.getByLabel('Nome').fill(renamed);
    await page.getByRole('button', { name: 'Salva' }).click();
    await expect(page).toHaveURL('/accounts');
    await expect(row(renamed)).toBeVisible();

    // Archive without a question, then undo from the snackbar.
    await page.getByRole('button', { name: `Azioni per ${renamed}`, exact: true }).click();
    await page.getByRole('menuitem', { name: 'Archivia' }).click();
    await expect(page.getByText(`Conto «${renamed}» archiviato.`)).toBeVisible();
    await expect(row(renamed)).toHaveCount(0);
    await page.getByRole('button', { name: 'Annulla' }).click();
    await expect(row(renamed)).toBeVisible();

    // Delete: the account has no movements, so it can go, after a confirmation.
    await page.getByRole('button', { name: `Azioni per ${renamed}`, exact: true }).click();
    await page.getByRole('menuitem', { name: 'Elimina' }).click();
    const dialog = page.getByRole('alertdialog');
    await expect(dialog).toContainText(`Eliminare «${renamed}»?`);
    await dialog.getByRole('button', { name: 'Elimina' }).click();
    await expect(page.getByText(`Conto «${renamed}» eliminato.`)).toBeVisible();
    await expect(row(renamed)).toHaveCount(0);
  });

  test('shows the errors of the form before calling the API', async ({ page }) => {
    await page.goto('/accounts/new');
    let calls = 0;
    page.on('request', (request) => {
      if (request.method() === 'POST' && request.url().endsWith('/api/accounts')) calls++;
    });

    await page.getByLabel('Valuta').fill('euro');
    await page.getByRole('button', { name: 'Crea conto' }).click();

    await expect(page.getByText('Inserisci il nome del conto.')).toBeVisible();
    await expect(page.getByText('Usa un codice di tre lettere, per esempio EUR.')).toBeVisible();
    await expect(page.getByLabel('Nome')).toBeFocused();
    expect(calls).toBe(0);
  });
});
