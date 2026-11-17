import { httpResource } from '@angular/common/http';
import { computed, inject, linkedSignal, Service, Signal } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import {
  ApiError,
  BudgetProgressResponse,
  BudgetResponse,
  DataChanges,
  ListResponse,
  referenceList,
} from '../../../core';
import { IsoMonth } from '../../../shared/dates';
import { BudgetsApi } from './budgets-api';

/** A budget change that "Annulla" can take back. */
export interface BudgetChange {
  /** The entry written. */
  readonly entry: BudgetResponse;
  /** Puts the budget back as it was before the change. */
  undo(): Promise<unknown>;
}

/** The progress of the budgets of one month. */
export interface BudgetProgressList {
  /** The last list of the month, kept while it is read again after a write. */
  readonly items: Signal<readonly BudgetProgressResponse[] | undefined>;
  readonly error: Signal<ApiError | undefined>;
  /** True while the month has no list yet. */
  readonly loading: Signal<boolean>;
  reload(): void;
}

/**
 * The budgets feature's only way to the API (docs/backend/api-endpoints.md#budgets). A budget is
 * a history of entries per expense category; every write tells `DataChanges` that `budgets`
 * changed, so the progress, the categories in use and (later) the dashboard are read again.
 */
@Service()
export class BudgetsStore {
  private readonly api = inject(BudgetsApi);
  private readonly changes = inject(DataChanges);
  private readonly list = referenceList<BudgetResponse>('/api/budgets', ['budgets'], {});

  /** Every entry of every category: a few per category a year, so the whole list is read. */
  readonly entries = this.list.items;
  readonly entriesError = this.list.error;
  readonly entriesLoading = this.list.loading;

  /** Categories with at least one entry, stopped ones included: the API's "budgeted". */
  readonly budgetedIds = computed(
    () => new Set((this.entries() ?? []).map((entry) => entry.categoryId)),
  );

  reloadEntries(): void {
    this.list.reload();
  }

  /** The entries of a category, oldest month first. */
  entriesOf(categoryId: string): BudgetResponse[] {
    return (this.entries() ?? [])
      .filter((entry) => entry.categoryId === categoryId)
      .sort((a, b) => a.from.localeCompare(b.from));
  }

  /**
   * The progress of every budget in `month` (always sent: the API would use its own UTC month),
   * read again after writes to budgets, movements and categories, which change what is spent.
   * Call it in an injection context (a page).
   */
  progressOf(month: Signal<IsoMonth>): BudgetProgressList {
    const resource = httpResource<ListResponse<BudgetProgressResponse>>(() => {
      this.changes.version('budgets', 'transactions', 'categories');
      return { url: '/api/budgets/progress', params: { month: month() } };
    });
    const items = linkedSignal<
      { month: IsoMonth; items: readonly BudgetProgressResponse[] | undefined },
      readonly BudgetProgressResponse[] | undefined
    >({
      source: () => ({
        month: month(),
        items: resource.hasValue() ? resource.value().items : undefined,
      }),
      // After a write the same month is read again: keep its rows meanwhile, not another month's.
      computation: (source, previous) =>
        source.items ??
        (previous && previous.source.month === source.month ? previous.value : undefined),
    });
    const error = computed(() => resource.error() as ApiError | undefined);
    return {
      items,
      error,
      loading: computed(() => items() === undefined && error() === undefined),
      reload: () => void resource.reload(),
    };
  }

  /**
   * Sets the budget of a category from `month` (`amount` `null` stops it): changes that month's
   * entry if there is one, otherwise adds one. The entries are read from the API first, so the
   * version sent is the current one.
   */
  async setFrom(categoryId: string, month: IsoMonth, amount: number | null): Promise<BudgetChange> {
    try {
      const current = await firstValueFrom(this.api.list(categoryId));
      const existing = current.items.find((entry) => entry.from === month);
      if (existing) {
        const updated = await firstValueFrom(this.api.update(existing, amount));
        return {
          entry: updated,
          undo: () => this.write(this.api.update(updated, existing.amount)),
        };
      }
      const created = await firstValueFrom(this.api.create({ categoryId, from: month, amount }));
      return { entry: created, undo: () => this.write(this.api.delete(created)) };
    } finally {
      this.changes.notify('budgets');
    }
  }

  /** Removes a wrong entry; without entries the category has no budget. */
  delete(entry: BudgetResponse): Promise<void> {
    return this.write(this.api.delete(entry));
  }

  private async write<T>(request: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(request);
    } finally {
      this.changes.notify('budgets');
    }
  }
}
