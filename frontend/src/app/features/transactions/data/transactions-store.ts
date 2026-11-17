import { httpResource } from '@angular/common/http';
import { computed, inject, Service } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import {
  CreateTransactionRequest,
  DataChanges,
  ListResponse,
  TagResponse,
  TransactionResponse,
  UpdateTransactionRequest,
} from '../../../core';
import { TransactionsApi } from './transactions-api';

/**
 * The transactions feature's only way to the API (docs/frontend/state-management.md#the-store-pattern).
 * Every write tells `DataChanges` that `transactions` changed: balances, categories in use, the
 * tags and the list are read again.
 */
@Service()
export class TransactionsStore {
  private readonly api = inject(TransactionsApi);
  private readonly changes = inject(DataChanges);

  private readonly tagsResource = httpResource<ListResponse<TagResponse>>(() => {
    this.changes.version('transactions');
    return '/api/tags';
  });

  /** Tag names, the most used first, for the suggestions of the tag field. */
  readonly tagNames = computed(() =>
    [...(this.tagsResource.hasValue() ? this.tagsResource.value().items : [])]
      .sort((a, b) => b.transactionCount - a.transactionCount || a.name.localeCompare(b.name))
      .map((tag) => tag.name),
  );

  /** Version of the data the list shows: it changes after every write to movements. */
  listVersion(): number {
    return this.changes.version('transactions', 'transfers');
  }

  list(...args: Parameters<TransactionsApi['list']>) {
    return this.api.list(...args);
  }

  load(id: string): Promise<TransactionResponse> {
    return firstValueFrom(this.api.get(id));
  }

  create(body: CreateTransactionRequest): Promise<TransactionResponse> {
    return this.write(this.api.create(body));
  }

  update(
    transaction: TransactionResponse,
    body: UpdateTransactionRequest,
  ): Promise<TransactionResponse> {
    return this.write(this.api.update(transaction, body));
  }

  delete(transaction: TransactionResponse): Promise<void> {
    return this.write(this.api.delete(transaction));
  }

  /** Sends a write, then has the readers of transactions read again, also after a failure. */
  private async write<T>(request: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(request);
    } finally {
      this.changes.notify('transactions');
    }
  }
}
