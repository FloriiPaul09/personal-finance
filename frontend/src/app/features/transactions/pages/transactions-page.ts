import {
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  LOCALE_ID,
  signal,
  untracked,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AccountsDirectory,
  CategoriesDirectory,
  cursorPager,
  ErrorState,
  TransactionResponse,
} from '../../../core';
import { AccountSelect, CategorySelect } from '../../../core/selectors';
import { DateInput, MonthSelector } from '../../../shared/date-pickers';
import { formatIsoDate, IsoMonth, todayIso } from '../../../shared/dates';
import { AmountPipe } from '../../../shared/money';
import { EmptyState } from '../../../shared/ui';
import { ScrollEnd } from '../components/scroll-end';
import { dayLabel, groupByDay, MovementQuery, movementView } from '../data/movement-query';
import { TransactionsStore } from '../data/transactions-store';

type QueryKey = keyof MovementQuery;

interface FilterChip {
  readonly key: QueryKey | 'period';
  readonly label: string;
}

const KIND_LABELS = {
  expense: $localize`:Kind filter of the movement list:Uscite`,
  income: $localize`:Kind filter of the movement list:Entrate`,
  transfer: $localize`:Kind filter of the movement list:Trasferimenti`,
};

/**
 * The movements of a period, newest first, grouped by day, loaded page by page while scrolling
 * (docs/frontend/ui-components.md#movement-list). The period and the filters live in the URL
 * (`?month=2026-11&accountId=…`), so a filtered list can be bookmarked and survives a reload.
 * No totals here: sums come from the API (dashboard).
 */
@Component({
  selector: 'app-transactions-page',
  imports: [
    AccountSelect,
    AmountPipe,
    CategorySelect,
    DateInput,
    EmptyState,
    ErrorState,
    MatButtonModule,
    MatButtonToggleModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MonthSelector,
    RouterLink,
    ScrollEnd,
  ],
  templateUrl: './transactions-page.html',
  styleUrl: './transactions-page.scss',
})
export class TransactionsPage {
  private readonly store = inject(TransactionsStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly locale = inject(LOCALE_ID);
  protected readonly accounts = inject(AccountsDirectory);
  protected readonly categories = inject(CategoriesDirectory);

  // From the query string.
  readonly month = input<string>();
  readonly from = input<string>();
  readonly to = input<string>();
  readonly accountId = input<string>();
  readonly categoryId = input<string>();
  readonly kind = input<string>();
  readonly q = input<string>();

  protected readonly view = computed(() =>
    movementView({
      month: this.month(),
      from: this.from(),
      to: this.to(),
      accountId: this.accountId(),
      categoryId: this.categoryId(),
      kind: this.kind(),
      q: this.q(),
    }),
  );
  protected readonly showFilters = signal(false);
  protected readonly today = todayIso();
  protected readonly kinds = (['expense', 'income', 'transfer'] as const).map((value) => ({
    value,
    label: KIND_LABELS[value],
  }));
  /** The categories offered by the filter follow the kind filter. */
  protected readonly categoryKind = computed(() => {
    const kind = this.view().filters.kind;
    return kind === 'expense' || kind === 'income' ? kind : null;
  });

  protected readonly page = cursorPager(
    (cursor) => this.store.list(this.view().filters, cursor),
    inject(DestroyRef),
  );
  protected readonly days = computed(() => groupByDay(this.page.items()));

  /** The filters in use, as removable chips. */
  protected readonly chips = computed<FilterChip[]>(() => {
    const { filters, customPeriod } = this.view();
    const chips: FilterChip[] = [];
    if (customPeriod) {
      chips.push({
        key: 'period',
        label: `${this.formatDay(filters.from)} – ${this.formatDay(filters.to)}`,
      });
    }
    if (filters.kind) {
      chips.push({ key: 'kind', label: KIND_LABELS[filters.kind] });
    }
    if (filters.accountId) {
      chips.push({
        key: 'accountId',
        label: this.accounts.byId(filters.accountId)?.name ?? '…',
      });
    }
    if (filters.categoryId) {
      const category = this.categories.byId(filters.categoryId);
      chips.push({ key: 'categoryId', label: category ? this.categories.label(category) : '…' });
    }
    if (filters.q) {
      chips.push({ key: 'q', label: `«${filters.q}»` });
    }
    return chips;
  });

  constructor() {
    // A new query, or a write to movements anywhere in the app: read the first page again.
    effect(() => {
      this.view();
      this.store.listVersion();
      untracked(() => this.page.reset());
    });
  }

  protected setMonth(month: IsoMonth): void {
    void this.navigate({ month, from: null, to: null });
  }

  protected setFilter(key: QueryKey, value: string | null): void {
    const changes: Partial<Record<QueryKey, string | null>> = { [key]: value || null };
    if (key === 'kind' && value === 'transfer') {
      changes.categoryId = null; // transfers have no category
    }
    void this.navigate(changes);
  }

  protected setPeriod(from: string | null, to: string | null): void {
    const current = this.view().filters;
    void this.navigate({ from: from ?? current.from, to: to ?? current.to });
  }

  protected remove(chip: FilterChip): void {
    void this.navigate(chip.key === 'period' ? { from: null, to: null } : { [chip.key]: null });
  }

  protected removeLabel(chip: FilterChip): string {
    return $localize`:Button that removes a filter:Togli il filtro ${chip.label}:filter:`;
  }

  protected clearFilters(): void {
    void this.navigate({ accountId: null, categoryId: null, kind: null, q: null });
  }

  protected loadMore(): void {
    if (!this.page.error()) {
      this.page.loadMore();
    }
  }

  protected retry(): void {
    if (this.page.loaded()) {
      this.page.loadMore();
    } else {
      this.page.reset();
    }
  }

  protected dayLabel(date: string): string {
    return dayLabel(date, this.today, this.locale);
  }

  /** "Spesa", "Trasporti › Carburante", or the direction of a transfer leg. */
  protected title(movement: TransactionResponse): string {
    if (movement.kind === 'transfer') {
      return movement.signedAmount < 0
        ? $localize`:Title of an outgoing transfer leg:Trasferimento in uscita`
        : $localize`:Title of an incoming transfer leg:Trasferimento in entrata`;
    }
    const category = this.categories.byId(movement.categoryId);
    return category ? this.categories.label(category) : '…';
  }

  /** Where a row leads: a transfer leg opens its transfer. */
  protected link(movement: TransactionResponse): string[] {
    return movement.transferId
      ? ['/transfers', movement.transferId]
      : ['/transactions', movement.id];
  }

  private formatDay(date: string): string {
    return formatIsoDate(date, this.locale, { day: 'numeric', month: 'short', year: 'numeric' });
  }

  private navigate(changes: Partial<Record<QueryKey, string | null>>): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: changes,
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
