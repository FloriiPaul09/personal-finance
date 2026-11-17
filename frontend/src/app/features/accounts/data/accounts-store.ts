import { inject, Service } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { AccountRequest, AccountResponse, AccountsDirectory, DataChanges } from '../../../core';
import { AccountsApi } from './accounts-api';

/**
 * The accounts feature's only way to the API (docs/frontend/state-management.md#the-store-pattern).
 * Reads come from `AccountsDirectory` in core, the list the selectors use too; every write tells
 * `DataChanges`, so that list (and later anything else showing accounts) is read again.
 */
@Service()
export class AccountsStore {
  private readonly api = inject(AccountsApi);
  private readonly directory = inject(AccountsDirectory);
  private readonly changes = inject(DataChanges);

  readonly all = this.directory.all;
  readonly error = this.directory.error;
  readonly loading = this.directory.loading;

  byId(id: string | null | undefined): AccountResponse | undefined {
    return this.directory.byId(id);
  }

  reload(): void {
    this.directory.reload();
  }

  create(body: AccountRequest): Promise<AccountResponse> {
    return this.write(this.api.create(body));
  }

  update(account: AccountResponse, body: AccountRequest): Promise<AccountResponse> {
    return this.write(this.api.update(account, body));
  }

  archive(account: AccountResponse): Promise<AccountResponse> {
    return this.write(this.api.archive(account));
  }

  unarchive(account: AccountResponse): Promise<AccountResponse> {
    return this.write(this.api.unarchive(account));
  }

  delete(account: AccountResponse): Promise<void> {
    return this.write(this.api.delete(account));
  }

  /** Sends a write, then has the accounts read again: also after a failure (e.g. a `412`). */
  private async write<T>(request: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(request);
    } finally {
      this.changes.notify('accounts');
    }
  }
}
