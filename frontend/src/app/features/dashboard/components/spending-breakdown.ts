import { Component, computed, inject, input, LOCALE_ID, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { CategoriesDirectory, CategorySpendingResponse } from '../../../core';
import { BarChart, BarItem } from '../../../shared/charts';
import { formatMonth, IsoMonth } from '../../../shared/dates';
import { AmountPipe, formatAmount } from '../../../shared/money';

/** Rows shown before "Mostra tutte". */
export const SPENDING_ROWS = 5;

interface SpendingRow {
  readonly item: CategorySpendingResponse;
  readonly name: string;
}

/**
 * The spending of a month in one currency, per top-level category, largest first (the API's
 * order and sums): horizontal bars as long as each amount compared with the largest, then the
 * same categories as a list with their amounts. Five categories, "Mostra tutte" the rest (bars
 * and rows together). A row, or its bar, opens the expenses of the category in that month
 * (docs/frontend/ui-components.md#dashboard).
 */
@Component({
  selector: 'app-spending-breakdown',
  imports: [AmountPipe, BarChart, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <app-bar-chart
      [items]="bars()"
      [label]="chartLabel()"
      [format]="format"
      (itemClick)="open(visible()[$event])"
    />
    <ul class="rows" [attr.aria-label]="listLabel()">
      @for (row of visible(); track row.item.categoryId) {
        <li>
          <a
            class="row"
            routerLink="/transactions"
            [queryParams]="{ categoryId: row.item.categoryId, month: month() }"
            data-testid="spending-row"
          >
            <span class="name">{{ row.name }}</span>
            <span class="pf-amount">{{ row.item.spent | amount: currency() }}</span>
            <mat-icon class="chevron" svgIcon="chevron_right" aria-hidden="true" />
          </a>
        </li>
      }
    </ul>
    @if (rows().length > limit) {
      <button
        matButton
        type="button"
        class="more"
        [attr.aria-expanded]="showAll()"
        (click)="showAll.set(!showAll())"
        data-testid="spending-more"
      >
        <mat-icon [svgIcon]="showAll() ? 'keyboard_arrow_up' : 'keyboard_arrow_down'" />
        @if (showAll()) {
          <ng-container i18n="Button that shows only the first categories of the spending"
            >Mostra meno</ng-container
          >
        } @else {
          <ng-container i18n="Button that shows every category of the spending"
            >Mostra tutte ({{ rows().length }})</ng-container
          >
        }
      </button>
    }
  `,
  styleUrl: './spending-breakdown.scss',
})
export class SpendingBreakdown {
  private readonly categories = inject(CategoriesDirectory);
  private readonly locale = inject(LOCALE_ID);
  private readonly router = inject(Router);

  /** The spending of one currency, largest first. */
  readonly items = input.required<readonly CategorySpendingResponse[]>();
  readonly currency = input.required<string>();
  readonly month = input.required<IsoMonth>();

  protected readonly limit = SPENDING_ROWS;
  protected readonly showAll = signal(false);

  protected readonly rows = computed<SpendingRow[]>(() =>
    this.items().map((item) => {
      const category = this.categories.byId(item.categoryId);
      return { item, name: category ? this.categories.label(category) : '…' };
    }),
  );
  protected readonly visible = computed(() =>
    this.showAll() ? this.rows() : this.rows().slice(0, this.limit),
  );
  protected readonly bars = computed<BarItem[]>(() =>
    this.visible().map((row) => ({ label: row.name, value: row.item.spent })),
  );

  private readonly monthLabel = computed(() => formatMonth(this.month(), this.locale));
  protected readonly chartLabel = computed(
    () =>
      $localize`:Description of the spending chart:Spesa di ${this.monthLabel()}:month: per categoria in ${this.currency()}:currency:, come nella lista che segue`,
  );
  protected readonly listLabel = computed(
    () =>
      $localize`:List of the spending per category:Spesa per categoria in ${this.currency()}:currency:`,
  );

  protected readonly format = (value: number): string =>
    formatAmount(value, this.currency(), this.locale);

  /** The expenses of the category in the month, as the row's link. */
  protected open(row: SpendingRow | undefined): void {
    if (row) {
      void this.router.navigate(['/transactions'], {
        queryParams: { categoryId: row.item.categoryId, month: this.month() },
      });
    }
  }
}
