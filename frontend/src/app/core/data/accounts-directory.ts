import { computed, Service, Signal } from '@angular/core';
import { AccountResponse } from '../api/models';
import { referenceList } from './reference-list';

/**
 * The accounts of the user, archived ones included, read once for the whole app: the accounts
 * page, the account selector and later movements, budgets and the dashboard share this list
 * (docs/frontend/state-management.md#reference-data). A person has a handful of accounts, so the
 * API does not page them. Read again after writes to accounts, transactions and transfers,
 * because `balance` and `hasMovements` come from the movements.
 */
@Service()
export class AccountsDirectory {
  private readonly list = referenceList<AccountResponse>('/api/accounts', [
    'accounts',
    'transactions',
    'transfers',
  ]);

  /** Every account: active ones by name, then archived ones by name (the API's order). */
  readonly all: Signal<readonly AccountResponse[] | undefined> = this.list.items;
  /** Accounts that accept new movements. */
  readonly active = computed(() => this.all()?.filter((account) => !account.isArchived));
  readonly error = this.list.error;
  readonly loading = this.list.loading;

  /** The account with this id, archived or not; `undefined` while loading or when unknown. */
  byId(id: string | null | undefined): AccountResponse | undefined {
    return id ? this.all()?.find((account) => account.id === id) : undefined;
  }

  reload(): void {
    this.list.reload();
  }
}
