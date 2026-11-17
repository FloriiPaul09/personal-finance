import { httpResource } from '@angular/common/http';
import { computed, inject, linkedSignal, Service, Signal } from '@angular/core';
import { ApiError, DashboardResponse, DataChanges } from '../../../core';
import { IsoMonth } from '../../../shared/dates';

/** The dashboard of one month. */
export interface MonthDashboard {
  /** The last answer for the month, kept while it is read again after a write. */
  readonly value: Signal<DashboardResponse | undefined>;
  readonly error: Signal<ApiError | undefined>;
  /** True while the month has no answer yet. */
  readonly loading: Signal<boolean>;
  reload(): void;
}

/**
 * The dashboard's only way to the API (docs/backend/api-endpoints.md#dashboard): one request with
 * every section. It is read again after any write it is computed from, so a new expense, a
 * transfer, a renamed category or a changed budget show up when the home page opens again
 * (docs/frontend/state-management.md#refresh-across-features).
 */
@Service()
export class DashboardStore {
  private readonly changes = inject(DataChanges);

  /**
   * The dashboard of `month` (always sent: the API would use its own UTC month). Call it in an
   * injection context (a page).
   */
  of(month: Signal<IsoMonth>): MonthDashboard {
    const resource = httpResource<DashboardResponse>(() => {
      this.changes.version('accounts', 'transactions', 'transfers', 'budgets', 'categories');
      return { url: '/api/dashboard', params: { month: month() } };
    });
    const value = linkedSignal<
      { month: IsoMonth; value: DashboardResponse | undefined },
      DashboardResponse | undefined
    >({
      source: () => ({
        month: month(),
        value: resource.hasValue() ? resource.value() : undefined,
      }),
      // After a write the same month is read again: keep its numbers meanwhile, not another month's.
      computation: (source, previous) =>
        source.value ??
        (previous && previous.source.month === source.month ? previous.value : undefined),
    });
    const error = computed(() => resource.error() as ApiError | undefined);
    return {
      value,
      error,
      loading: computed(() => value() === undefined && error() === undefined),
      reload: () => void resource.reload(),
    };
  }
}
