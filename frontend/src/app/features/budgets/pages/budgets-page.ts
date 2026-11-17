import { Component, computed, inject, input, LOCALE_ID } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  BUDGET_CURRENCY,
  BudgetProgressResponse,
  CategoriesDirectory,
  ErrorState,
  Notifier,
} from '../../../core';
import { MonthSelector } from '../../../shared/date-pickers';
import { currentMonth, formatMonth, isIsoMonth, IsoMonth } from '../../../shared/dates';
import { BudgetProgress, EmptyState } from '../../../shared/ui';
import { BudgetsStore } from '../data/budgets-store';

interface BudgetRow {
  readonly item: BudgetProgressResponse;
  readonly name: string;
}

/**
 * The budgets of a month (`/budgets?month=2026-11`): one row per category with a budget that
 * month, with its progress. The row opens the expenses of the category in that month; its menu
 * changes the budget or stops it from the month shown, with "Annulla"
 * (docs/frontend/ui-components.md#budgets).
 */
@Component({
  selector: 'app-budgets-page',
  imports: [
    BudgetProgress,
    EmptyState,
    ErrorState,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MonthSelector,
    RouterLink,
  ],
  templateUrl: './budgets-page.html',
  styleUrl: './budgets-page.scss',
})
export class BudgetsPage {
  private readonly store = inject(BudgetsStore);
  private readonly categories = inject(CategoriesDirectory);
  private readonly notifier = inject(Notifier);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly locale = inject(LOCALE_ID);

  /** From the query string; the current month when missing or not a month. */
  readonly month = input<string>();

  protected readonly view = computed<IsoMonth>(() => {
    const month = this.month();
    return isIsoMonth(month) ? month : currentMonth();
  });
  protected readonly monthLabel = computed(() => formatMonth(this.view(), this.locale));
  protected readonly progress = this.store.progressOf(this.view);
  protected readonly currency = BUDGET_CURRENCY;

  protected readonly rows = computed<BudgetRow[]>(() =>
    (this.progress.items() ?? []).map((item) => {
      const category = this.categories.byId(item.categoryId);
      return { item, name: category ? this.categories.label(category) : '…' };
    }),
  );

  protected setMonth(month: IsoMonth): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { month },
      replaceUrl: true,
    });
  }

  protected actionsLabel(row: BudgetRow): string {
    return $localize`:Menu button of a budget row:Azioni per ${row.name}:name:`;
  }

  protected stopLabel(): string {
    return $localize`:Menu item that stops a budget from a month:Ferma da ${this.monthLabel()}:month:`;
  }

  /** Stops the budget from the month shown; "Annulla" puts it back. */
  protected async stop(row: BudgetRow): Promise<void> {
    const change = await this.store.setFrom(row.item.categoryId, this.view(), null);
    void this.notifier.undoable(
      $localize`:Snackbar after stopping a budget:Budget di ${row.name}:name: fermato da ${this.monthLabel()}:month:.`,
      () => void change.undo(),
    );
  }
}
