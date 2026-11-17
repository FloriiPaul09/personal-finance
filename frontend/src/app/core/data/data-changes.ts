import { Service, signal, WritableSignal } from '@angular/core';

/** Resources of the API whose writes other features may need to see (block 5 onwards). */
export type DataResource = 'accounts' | 'categories' | 'transactions' | 'transfers' | 'budgets';

/**
 * Data changes by resource, so a write in one feature refreshes the data of the others
 * (docs/frontend/state-management.md#refresh-across-features).
 *
 * - A store calls `notify('accounts')` after each write to accounts, failed ones included: a
 *   `412` means the data changed elsewhere, so it must be read again too.
 * - A reader calls `version('accounts', 'transactions', …)` inside the request of its
 *   `httpResource`: the request changes after every write to one of them, so it loads again.
 *   The reader lists what its data is computed from (an account's balance comes from the
 *   transactions and transfers), the writer only what it wrote.
 */
@Service()
export class DataChanges {
  private readonly counters = new Map<DataResource, WritableSignal<number>>();

  /** Number of writes so far to any of these resources; reactive. */
  version(...resources: DataResource[]): number {
    return resources.reduce((sum, resource) => sum + this.counter(resource)(), 0);
  }

  /** These resources changed on the server. */
  notify(...resources: DataResource[]): void {
    for (const resource of resources) {
      this.counter(resource).update((count) => count + 1);
    }
  }

  private counter(resource: DataResource): WritableSignal<number> {
    let counter = this.counters.get(resource);
    if (!counter) {
      counter = signal(0);
      this.counters.set(resource, counter);
    }
    return counter;
  }
}
