import {
  afterNextRender,
  Component,
  computed,
  effect,
  inject,
  Injector,
  input,
  linkedSignal,
  LOCALE_ID,
  signal,
  untracked,
} from '@angular/core';
import { form, FormField, required, submit, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import {
  ApiError,
  BUDGET_CURRENCY,
  BudgetResponse,
  CategoriesDirectory,
  CategoryResponse,
  ErrorState,
  Notifier,
  PageExit,
} from '../../../core';
import { apiFormErrors, focusFirstError } from '../../../core/forms';
import { CategoryDisabledReason, CategorySelect } from '../../../core/selectors';
import { MonthSelector } from '../../../shared/date-pickers';
import { currentMonth, formatMonth, isIsoMonth, IsoMonth } from '../../../shared/dates';
import { AmountInput, AmountPipe } from '../../../shared/money';
import { confirmAction, EmptyState } from '../../../shared/ui';
import { BudgetsStore } from '../data/budgets-store';

interface BudgetModel {
  categoryId: string | null;
  from: IsoMonth;
  amount: number | null;
}

interface Start {
  readonly key: string;
  readonly model: BudgetModel;
}

/** Codes about the category: on its field for a new budget, on the form when it is fixed. */
const CATEGORY_CODES = [
  'budget.branch_conflict',
  'budget.category_not_expense',
  'category.archived',
  'category.not_found',
] as const;

/**
 * The budget of an expense category from a month (`/budgets/new?month=…`,
 * `/budgets/:categoryId?month=…`). The month starts as the one the budgets page showed; saving
 * changes that month's entry if there is one, otherwise adds one (docs/backend/api-endpoints.md#budgets).
 * Below the form, the history of the category: each entry can be deleted, after a confirmation,
 * to correct a mistake.
 */
@Component({
  selector: 'app-budget-form-page',
  imports: [
    AmountInput,
    AmountPipe,
    CategorySelect,
    EmptyState,
    ErrorState,
    FormField,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MonthSelector,
    RouterLink,
  ],
  templateUrl: './budget-form-page.html',
  styleUrl: './budget-form-page.scss',
})
export class BudgetFormPage {
  protected readonly store = inject(BudgetsStore);
  private readonly categories = inject(CategoriesDirectory);
  private readonly notifier = inject(Notifier);
  private readonly exit = inject(PageExit);
  private readonly dialog = inject(MatDialog);
  private readonly injector = inject(Injector);
  private readonly locale = inject(LOCALE_ID);

  /** From the route: absent for a new budget. */
  readonly categoryId = input<string>();
  /** From the query string: the month the budgets page showed. */
  readonly month = input<string>();

  protected readonly currency = BUDGET_CURRENCY;
  protected readonly isNew = computed(() => !this.categoryId());
  private readonly startMonth = computed<IsoMonth>(() => {
    const month = this.month();
    return isIsoMonth(month) ? month : currentMonth();
  });
  protected readonly backParams = computed(() => ({ month: this.startMonth() }));

  protected readonly category = computed(() => this.categories.byId(this.categoryId()));
  protected readonly categoryName = computed(() => {
    const category = this.category();
    return category ? this.categories.label(category) : '';
  });
  protected readonly ready = computed(
    () => this.store.entries() !== undefined && this.categories.all() !== undefined,
  );
  protected readonly notFound = computed(
    () => !this.isNew() && this.ready() && this.category()?.kind !== 'expense',
  );
  protected readonly loadError = computed(
    () => this.store.entriesError() ?? this.categories.error(),
  );

  /** Entries of the category, the latest month first. */
  protected readonly history = computed(() => {
    const id = this.categoryId();
    return id ? this.store.entriesOf(id).reverse() : [];
  });

  protected readonly changedElsewhere = signal(false);

  private readonly start = computed<Start>(
    () => {
      const id = this.categoryId() ?? null;
      const from = this.startMonth();
      // The limit in force that month, as a starting point to change.
      const inForce = untracked(() => (id ? this.store.entriesOf(id) : []))
        .reverse()
        .find((entry) => entry.from <= from);
      return {
        key: `${id}:${from}:${this.ready()}`,
        model: { categoryId: id, from, amount: inForce?.amount ?? null },
      };
    },
    { equal: (a, b) => a.key === b.key },
  );

  protected readonly model = linkedSignal(() => this.start().model);
  protected readonly budgetForm = form(this.model, (path) => {
    required(path.categoryId, { message: $localize`:Budget form error:Scegli la categoria.` });
    required(path.amount, { message: $localize`:Budget form error:Inserisci il limite.` });
    validate(path.amount, ({ value }) =>
      value() !== null && value()! <= 0
        ? {
            kind: 'positive',
            message: $localize`:Budget form error:Il limite deve essere maggiore di zero.`,
          }
        : undefined,
    );
  });

  /**
   * One level per branch, as the API: a category with a budget, a subcategory of a parent with a
   * budget and a parent of subcategories with a budget cannot get a new one.
   */
  protected readonly disabledReason: CategoryDisabledReason = (category: CategoryResponse) => {
    const budgeted = this.store.budgetedIds();
    if (budgeted.has(category.id)) {
      return $localize`:Why a category cannot get a new budget:Ha già un budget: cambialo dalla pagina Budget`;
    }
    if (category.parentId && budgeted.has(category.parentId)) {
      const parent = this.categories.byId(category.parentId);
      return $localize`:Why a category cannot get a new budget:Il budget è su «${parent?.name ?? ''}:parent:»`;
    }
    const childBudgeted = (this.categories.all() ?? []).some(
      (child) => child.parentId === category.id && budgeted.has(child.id),
    );
    return childBudgeted
      ? $localize`:Why a category cannot get a new budget:Il budget è sulle sue sottocategorie`
      : null;
  };

  constructor() {
    effect(() => {
      if (this.ready() && !this.notFound()) {
        untracked(() =>
          afterNextRender(() => this.budgetForm.amount().focusBoundControl(), {
            injector: this.injector,
          }),
        );
      }
    });
  }

  protected monthLabel(month: IsoMonth): string {
    return formatMonth(month, this.locale);
  }

  protected setFrom(month: IsoMonth): void {
    this.model.update((model) => ({ ...model, from: month }));
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.changedElsewhere.set(false);
    await submit(this.budgetForm, {
      action: async () => {
        const { categoryId, from, amount } = this.model();
        try {
          await this.store.setFrom(categoryId!, from, amount);
        } catch (error) {
          if (error instanceof ApiError && error.status === 412) {
            this.changedElsewhere.set(true);
            return undefined;
          }
          const categoryCodes = Object.fromEntries(
            CATEGORY_CODES.map((code) => [code, 'categoryId' as const]),
          );
          const errors = apiFormErrors(this.budgetForm, error, {
            fieldsByCode: {
              ...(this.isNew() ? categoryCodes : {}),
              'amount.not_positive': 'amount',
              'amount.too_many_decimals': 'amount',
            },
            formCodes: this.isNew() ? ['budget.month_taken'] : [...CATEGORY_CODES],
          });
          if (errors) {
            return errors;
          }
          throw error;
        }
        void this.notifier.show($localize`:Snackbar after saving a budget:Budget salvato.`);
        await this.exit.leave(`/budgets?month=${from}`);
        return undefined;
      },
      onInvalid: (field) => focusFirstError(field),
    });
  }

  protected deleteLabel(entry: BudgetResponse): string {
    return $localize`:Button that deletes a budget entry:Elimina la voce di ${this.monthLabel(entry.from)}:month:`;
  }

  /** Removes a wrong entry, after a confirmation: the months it covered go back as before. */
  protected async deleteEntry(entry: BudgetResponse): Promise<void> {
    const confirmed = await confirmAction(this.dialog, {
      title: $localize`:Title of the budget entry delete confirmation:Eliminare questa voce?`,
      message: $localize`:Message of the budget entry delete confirmation:Serve a correggere un errore: da ${this.monthLabel(entry.from)}:month: il budget torna come prima di questa voce. Per smettere di usare il budget, fermalo dalla pagina Budget. L'eliminazione non si può annullare.`,
      confirm: $localize`:Button that confirms a delete:Elimina`,
      cancel: $localize`:Button that closes a confirmation:Annulla`,
    });
    if (!confirmed) {
      return;
    }
    await this.store.delete(entry);
    void this.notifier.show($localize`:Snackbar after deleting a budget entry:Voce eliminata.`);
  }
}
