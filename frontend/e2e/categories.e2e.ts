import { expect, test } from '@playwright/test';

// Categories: a new income category, a subcategory from its menu, then both deleted (block 5).
// Each run uses its own names and deletes what it created. Names are matched exactly: the
// subcategory's name starts with the parent's.
test.describe('categories', () => {
  test('creates a category and a subcategory, then deletes them', async ({ page }, info) => {
    const parent = `E2E ${info.project.name} ${Date.now()}`;
    const child = `${parent} figlia`;
    const row = (text: string) =>
      page.getByTestId('category-row').filter({ has: page.getByText(text, { exact: true }) });

    await page.goto('/categories');
    await page.getByRole('tab', { name: 'Entrate' }).click();
    await expect(page).toHaveURL('/categories?kind=income');

    await page.getByTestId('new-category').click();
    await expect(page.getByRole('radio', { name: 'Entrata' })).toBeChecked();
    await page.getByLabel('Nome').fill(parent);
    await page.getByRole('button', { name: 'Crea categoria' }).click();
    await expect(page).toHaveURL('/categories?kind=income');
    await expect(row(parent)).toBeVisible();

    // A subcategory from the menu of the parent: the form starts under it.
    await page.getByRole('button', { name: `Azioni per ${parent}`, exact: true }).click();
    await page.getByRole('menuitem', { name: 'Aggiungi sottocategoria' }).click();
    await expect(page.getByRole('combobox', { name: 'Categoria madre' })).toContainText(parent);
    await page.getByLabel('Nome').fill(child);
    await page.getByRole('button', { name: 'Crea categoria' }).click();
    await expect(row(child)).toHaveClass(/sub/);

    // The parent cannot be deleted while it has a subcategory.
    await page.getByRole('button', { name: `Azioni per ${parent}`, exact: true }).click();
    await expect(page.getByRole('menuitem', { name: /Elimina/ })).toBeDisabled();
    await page.keyboard.press('Escape');

    for (const name of [child, parent]) {
      await page.getByRole('button', { name: `Azioni per ${name}`, exact: true }).click();
      await page.getByRole('menuitem', { name: 'Elimina' }).click();
      await page.getByRole('alertdialog').getByRole('button', { name: 'Elimina' }).click();
      await expect(row(name)).toHaveCount(0);
    }
  });
});
