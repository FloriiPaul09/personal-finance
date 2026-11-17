import { APIRequestContext, Page } from '@playwright/test';

/**
 * Calls of the API with the session of the page, to prepare and clean up the data of a test
 * (accounts to spend from) without going through screens another test already covers. Writes
 * carry the antiforgery header like the app does; open a page of the app first, which sets the
 * cookie.
 */
export class TestApi {
  private constructor(
    private readonly request: APIRequestContext,
    private readonly xsrf: string,
  ) {}

  static async of(page: Page): Promise<TestApi> {
    const cookie = (await page.context().cookies()).find((c) => c.name === 'XSRF-TOKEN');
    if (!cookie) {
      throw new Error('No XSRF-TOKEN cookie: open a page of the app first.');
    }
    return new TestApi(page.request, decodeURIComponent(cookie.value));
  }

  async createAccount(name: string, initialBalance = 100, currency = 'EUR'): Promise<Account> {
    const response = await this.request.post('/api/accounts', {
      headers: { 'X-XSRF-TOKEN': this.xsrf },
      data: { name, type: 'checking', currency, initialBalance },
    });
    if (!response.ok()) {
      throw new Error(`Account not created: ${response.status()} ${await response.text()}`);
    }
    return response.json();
  }

  async account(id: string): Promise<Account> {
    return (await this.request.get(`/api/accounts/${id}`)).json();
  }

  /** Deletes the account and, first, every movement left on it (a failed run). */
  async deleteAccount(id: string): Promise<void> {
    const list = await (
      await this.request.get(`/api/transactions?accountId=${id}&limit=200`)
    ).json();
    for (const movement of list.items as Movement[]) {
      const [url, version] = movement.transferId
        ? [
            `/api/transfers/${movement.transferId}`,
            await this.version(`/api/transfers/${movement.transferId}`),
          ]
        : [`/api/transactions/${movement.id}`, movement.version];
      if (version !== null) {
        await this.request.delete(url, {
          headers: { 'X-XSRF-TOKEN': this.xsrf, 'If-Match': `"${version}"` },
        });
      }
    }
    const account = await this.account(id);
    await this.request.delete(`/api/accounts/${id}`, {
      headers: { 'X-XSRF-TOKEN': this.xsrf, 'If-Match': `"${account.version}"` },
    });
  }

  private async version(url: string): Promise<number | null> {
    const response = await this.request.get(url);
    return response.ok() ? (await response.json()).version : null;
  }
}

export interface Account {
  id: string;
  name: string;
  balance: number;
  version: number;
}

interface Movement {
  id: string;
  transferId: string | null;
  version: number;
}
