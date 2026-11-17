import { expect, Page } from '@playwright/test';

/** Signs in through the login page, like a person does. */
export async function signIn(page: Page, email: string, password: string): Promise<void> {
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Accedi' }).click();
}

/**
 * The page never scrolls more than its content needs, and never sideways (block 1: the rail
 * made a short page 16px taller than the window). A long page (a list of categories on a phone)
 * scrolls, as it should, by the height of its content only.
 */
export async function expectNoPageScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => {
    const content = Math.ceil(document.querySelector('main')?.getBoundingClientRect().height ?? 0);
    return {
      vertical: document.scrollingElement!.scrollHeight - Math.max(window.innerHeight, content),
      horizontal: document.scrollingElement!.scrollWidth - window.innerWidth,
    };
  });
  expect(overflow).toEqual({ vertical: 0, horizontal: 0 });
}

/**
 * The text of the page title never runs under the account button (block 3: on phones the
 * button is fixed at the top right, over the title's line).
 */
export async function expectTitleClearOfAccountButton(page: Page): Promise<void> {
  const overlap = await page.evaluate(() => {
    const title = document.querySelector('h1')!;
    const button = document.querySelector('[data-testid="settings"]')!.getBoundingClientRect();
    const box = title.getBoundingClientRect();
    const textRight = box.right - parseFloat(getComputedStyle(title).paddingRight);
    const sameLine = box.top < button.bottom && box.bottom > button.top;
    return sameLine ? Math.max(0, textRight - button.left) : 0;
  });
  expect(overlap).toBe(0);
}

/** Nothing covers the center of the element (block 1: the button's touch target covered the rail). */
export async function expectClickable(page: Page, selector: string): Promise<void> {
  const covered = await page.evaluate((s) => {
    return Array.from(document.querySelectorAll(s))
      .filter((element) => element.getClientRects().length > 0) // hidden on purpose
      .filter((element) => {
        const box = element.getBoundingClientRect();
        const hit = document.elementFromPoint(box.x + box.width / 2, box.y + box.height / 2);
        return !element.contains(hit);
      })
      .map((element) => element.textContent?.trim() || element.getAttribute('aria-label'));
  }, selector);
  expect(covered).toEqual([]);
}
