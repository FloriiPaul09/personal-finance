import { Component, computed, inject, input, LOCALE_ID } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  BUDGET_CURRENCY,
  BudgetProgressResponse,
  CategoriesDirectory,
  CategorySpendingResponse,
  ErrorState,
} from '../../../core';
import { accountTypeInfo } from '../../../core/selectors';
import { MonthSelector } from '../../../shared/date-pickers';
import { currentMonth, formatMonth, isIsoMonth, IsoMonth } from '../../../shared/dates';
import { AmountPipe } from '../../../shared/money';
import { BudgetProgress, EmptyState } from '../../../shared/ui';
import { SpendingBreakdown } from '../components/spending-breakdown';
import { DashboardStore } from '../data/dashboard-store';

/** Budgets shown on the home page: the closest to their limit. */
export const DASHBOARD_BUDGETS = 3;

interface BudgetRow {
  readonly item: BudgetProgressResponse;
  readonly name: string;
}

interface SpendingGroup {
  readonly currency: string;
  readonly items: readonly CategorySpendingResponse[];
}

/**
 * The home page (`/dashboard?month=2026-11`): how the month is going, then where the money is.
 *
 * 1. the month, never in the future;
 * 2. income, expenses and net of the month, one row per currency;
 * 3. the three budgets closest to their limit, and a link to all of them;
 * 4. the spending per category, a donut with its list;
 * 5. the accounts with their current balance, and the total per currency.
 *
 * Every number comes from one request (`GET /api/dashboard`), never added up here
 * (docs/frontend/ui-components.md#dashboard). Without accounts the page only invites to add one.
 */
@Component({
  selector: 'app-dashboard-page',
  imports: [
    AmountPipe,
    BudgetProgress,
    EmptyState,
    ErrorState,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MonthSelector,
    RouterLink,
    SpendingBreakdown,
  ],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  private readonly store = inject(DashboardStore);
  private readonly categories = inject(CategoriesDirectory);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly locale = inject(LOCALE_ID);

  /** From the query string; the current month when missing, not a month or in the future. */
  readonly month = input<string>();

  protected readonly today = currentMonth();
  protected readonly view = computed<IsoMonth>(() => {
    const month = this.month();
    return isIsoMonth(month) && month <= this.today ? month : this.today;
  });
  protected readonly dashboard = this.store.of(this.view);
  protected readonly budgetCurrency = BUDGET_CURRENCY;
  protected readonly typeOf = accountTypeInfo;

  /** Nothing to show yet: no account, not even an archived one. */
  protected readonly firstUse = computed(() => {
    const value = this.dashboard.value();
    return value !== undefined && value.accounts.length === 0 && value.balances.length === 0;
  });

  /** Several currencies: rows and totals say which one they are in. */
  protected readonly manyCurrencies = computed(
    () => (this.dashboard.value()?.balances.length ?? 0) > 1,
  );

  protected readonly budgets = computed<BudgetRow[]>(() =>
    [...(this.dashboard.value()?.budgets ?? [])]
      .sort((a, b) => b.ratio - a.ratio)
      .slice(0, DASHBOARD_BUDGETS)
      .map((item) => {
        const category = this.categories.byId(item.categoryId);
        return { item, name: category ? this.categories.label(category) : '…' };
      }),
  );
  protected readonly budgetCount = computed(() => this.dashboard.value()?.budgets.length ?? 0);

  protected readonly spending = computed<SpendingGroup[]>(() => {
    const groups = new Map<string, CategorySpendingResponse[]>();
    for (const item of this.dashboard.value()?.spending ?? []) {
      const group = groups.get(item.currency) ?? [];
      group.push(item);
      groups.set(item.currency, group);
    }
    return [...groups].map(([currency, items]) => ({ currency, items }));
  });

  protected readonly monthLabel = computed(() => formatMonth(this.view(), this.locale));

  protected setMonth(month: IsoMonth): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { month },
      replaceUrl: true,
    });
  }
}
