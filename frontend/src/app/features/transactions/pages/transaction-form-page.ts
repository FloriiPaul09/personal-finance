import {
  afterNextRender,
  Component,
  computed,
  effect,
  inject,
  Injector,
  input,
  linkedSignal,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { form, FormField, maxLength, required, submit, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { NgTemplateOutlet } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import {
  AccountsDirectory,
  ApiError,
  CategoriesDirectory,
  CategoryKind,
  ErrorState,
  newId,
  Notifier,
  PageExit,
  RecentChoices,
  TransactionResponse,
} from '../../../core';
import { apiFormErrors, focusFirstError } from '../../../core/forms';
import { AccountSelect, CategorySelect, MovementKindSwitch } from '../../../core/selectors';
import { DateInput } from '../../../shared/date-pickers';
import { IsoDate, todayIso } from '../../../shared/dates';
import { AmountInput } from '../../../shared/money';
import { confirmAction, EmptyState } from '../../../shared/ui';
import { RecentCategories } from '../components/recent-categories';
import { TagsInput } from '../components/tags-input';
import { TransactionsStore } from '../data/transactions-store';

/** Longest description the API accepts. */
export const DESCRIPTION_MAX_LENGTH = 200;

const TRANSACTIONS_PATH = '/transactions';

interface MovementModel {
  amount: number | null;
  categoryId: string | null;
  accountId: string | null;
  date: IsoDate | null;
  description: string;
  tags: string[];
}

/** The values a form starts from, with a key that changes only when the form must start again. */
interface Start {
  readonly key: string;
  readonly model: MovementModel;
}

const TEXTS = {
  expense: {
    new: $localize`:Title of the new expense form:Nuova spesa`,
    edit: $localize`:Title of the expense form:Modifica spesa`,
    saved: $localize`:Snackbar after saving a new expense:Spesa salvata.`,
  },
  income: {
    new: $localize`:Title of the new income form:Nuova entrata`,
    edit: $localize`:Title of the income form:Modifica entrata`,
    saved: $localize`:Snackbar after saving a new income:Entrata salvata.`,
  },
};

/**
 * Quick entry of an expense or an income (`/transactions/new`, `?kind=income`) and changes to one
 * (`/transactions/:id`), docs/frontend/ui-components.md#quick-entry:
 *
 * - amount first, with the numeric keyboard; the category among the last used (chips) or in the
 *   full selector; the account of the last movement; today; description and tags folded away;
 * - the id is chosen when the form opens (`newId()`): the same request sent twice creates one
 *   movement (the PWA's offline queue will rely on it);
 * - saving goes back with "Annulla", which deletes the new movement; "Salva e aggiungi un'altra"
 *   keeps account and date for the next one.
 */
@Component({
  selector: 'app-transaction-form-page',
  imports: [
    AccountSelect,
    AmountInput,
    CategorySelect,
    DateInput,
    EmptyState,
    ErrorState,
    FormField,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MovementKindSwitch,
    NgTemplateOutlet,
    RecentCategories,
    RouterLink,
    TagsInput,
  ],
  templateUrl: './transaction-form-page.html',
  styleUrl: './transaction-form-page.scss',
})
export class TransactionFormPage {
  protected readonly store = inject(TransactionsStore);
  protected readonly accounts = inject(AccountsDirectory);
  protected readonly categories = inject(CategoriesDirectory);
  private readonly recent = inject(RecentChoices);
  private readonly notifier = inject(Notifier);
  private readonly exit = inject(PageExit);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly injector = inject(Injector);

  /** From the route: absent for a new movement. */
  readonly id = input<string>();
  /** From the query string of a new movement: `income`, otherwise an expense. */
  readonly kind = input<string>();

  protected readonly transactionsPath = TRANSACTIONS_PATH;
  protected readonly isNew = computed(() => !this.id());

  /** The movement being changed, read from the API (with its version). */
  protected readonly existing = resource({
    params: () => this.id(),
    loader: ({ params }) => this.store.load(params),
  });

  protected readonly movementKind = computed<CategoryKind>(() => {
    const existing = this.existing.hasValue() ? this.existing.value() : undefined;
    if (existing) {
      return existing.kind === 'income' ? 'income' : 'expense';
    }
    return this.kind() === 'income' ? 'income' : 'expense';
  });
  protected readonly texts = computed(() => TEXTS[this.movementKind()]);

  /** The id of the next movement created; a new one after each save. */
  private readonly nextId = signal(newId());
  /** The date kept by "Salva e aggiungi un'altra". */
  private readonly keptDate = signal<IsoDate | null>(null);
  /** The movement changed elsewhere while the form was open (`412`). */
  protected readonly changedElsewhere = signal(false);

  protected readonly ready = computed(() =>
    this.isNew()
      ? this.accounts.all() !== undefined && this.categories.all() !== undefined
      : this.existing.hasValue(),
  );

  private readonly start = computed<Start>(
    () => {
      const existing = this.existing.hasValue() ? this.existing.value() : undefined;
      if (existing) {
        return {
          key: `${existing.id}:${existing.version}`,
          model: {
            amount: existing.amount,
            categoryId: existing.categoryId,
            accountId: existing.accountId,
            date: existing.date,
            description: existing.description ?? '',
            tags: [...existing.tags],
          },
        };
      }
      return {
        key: `new:${this.movementKind()}:${this.nextId()}:${this.ready()}`,
        model: {
          amount: null,
          categoryId: null,
          accountId: this.defaultAccount(),
          date: untracked(this.keptDate) ?? todayIso(),
          description: '',
          tags: [],
        },
      };
    },
    { equal: (a, b) => a.key === b.key },
  );

  protected readonly model = linkedSignal(() => this.start().model);
  protected readonly movementForm = form(this.model, (path) => {
    required(path.amount, { message: $localize`:Movement form error:Inserisci l'importo.` });
    validate(path.amount, ({ value }) =>
      value() !== null && value()! <= 0
        ? {
            kind: 'positive',
            message: $localize`:Movement form error:L'importo deve essere maggiore di zero.`,
          }
        : undefined,
    );
    required(path.categoryId, { message: $localize`:Movement form error:Scegli la categoria.` });
    required(path.accountId, { message: $localize`:Movement form error:Scegli il conto.` });
    required(path.date, { message: $localize`:Movement form error:Inserisci la data.` });
    maxLength(path.description, DESCRIPTION_MAX_LENGTH, {
      message: $localize`:Movement form error:Al massimo ${DESCRIPTION_MAX_LENGTH}:count: caratteri.`,
    });
  });

  /** Description and tags are folded away until needed, or when the movement has them. */
  protected readonly showDetails = linkedSignal(
    () => this.start().model.description !== '' || this.start().model.tags.length > 0,
  );

  /** The last categories used for this kind that still exist and are active. */
  protected readonly recentCategories = computed(() =>
    this.recent
      .categories(this.movementKind())
      .map((id) => this.categories.byId(id))
      .filter((c) => c !== undefined && !c.isArchived && c.kind === this.movementKind())
      .map((c) => c!),
  );

  /** The currency of the amount: the chosen account's. */
  protected readonly currency = computed(
    () => this.accounts.byId(this.model().accountId)?.currency ?? 'EUR',
  );

  constructor() {
    // A transfer leg is changed from its transfer.
    effect(() => {
      const existing = this.existing.hasValue() ? this.existing.value() : undefined;
      if (existing?.transferId) {
        void this.router.navigate(['/transfers', existing.transferId], { replaceUrl: true });
      }
    });
    // The amount gets the focus when a new form is ready, so the keyboard opens on it.
    effect(() => {
      if (this.isNew() && this.ready()) {
        this.start();
        untracked(() => this.focusAmount());
      }
    });
  }

  protected chooseCategory(id: string | null): void {
    this.movementForm.categoryId().value.set(id);
  }

  protected async save(another: boolean, event?: Event): Promise<void> {
    event?.preventDefault();
    this.changedElsewhere.set(false);
    await submit(this.movementForm, {
      action: async () => {
        const existing = this.existing.hasValue() ? this.existing.value() : undefined;
        let saved: TransactionResponse;
        try {
          saved = existing
            ? await this.store.update(existing, this.updateBody())
            : await this.store.create({
                id: this.nextId(),
                kind: this.movementKind(),
                ...this.updateBody(),
              });
        } catch (error) {
          if (error instanceof ApiError && error.status === 412) {
            this.changedElsewhere.set(true);
            this.existing.reload();
            return undefined;
          }
          const errors = apiFormErrors(this.movementForm, error, {
            fieldsByCode: {
              'amount.not_positive': 'amount',
              'amount.too_many_decimals': 'amount',
              'amount.out_of_range': 'amount',
              'account.archived': 'accountId',
              'account.not_found': 'accountId',
              'category.archived': 'categoryId',
              'category.not_found': 'categoryId',
              'category.kind_mismatch': 'categoryId',
              'description.too_long': 'description',
              'name.required': 'tags',
              'name.too_long': 'tags',
            },
          });
          if (errors) {
            return errors;
          }
          throw error;
        }
        this.recent.remember({
          kind: this.movementKind(),
          categoryId: saved.categoryId,
          accountId: saved.accountId,
        });
        if (existing) {
          void this.notifier.show($localize`:Snackbar after saving a movement:Movimento salvato.`);
          await this.exit.leave(TRANSACTIONS_PATH);
        } else {
          void this.notifier.undoable(this.texts().saved, () => void this.store.delete(saved));
          if (another) {
            this.startAnother(saved.date);
          } else {
            await this.exit.leave(TRANSACTIONS_PATH);
          }
        }
        return undefined;
      },
      onInvalid: (field) => focusFirstError(field),
    });
  }

  protected async delete(): Promise<void> {
    const existing = this.existing.hasValue() ? this.existing.value() : undefined;
    if (!existing) {
      return;
    }
    const confirmed = await confirmAction(this.dialog, {
      title: $localize`:Title of the movement delete confirmation:Eliminare questo movimento?`,
      message: $localize`:Message of the movement delete confirmation:L'eliminazione non si può annullare.`,
      confirm: $localize`:Button that confirms a delete:Elimina`,
      cancel: $localize`:Button that closes a confirmation:Annulla`,
    });
    if (!confirmed) {
      return;
    }
    await this.store.delete(existing);
    void this.notifier.show($localize`:Snackbar after deleting a movement:Movimento eliminato.`);
    await this.exit.leave(TRANSACTIONS_PATH);
  }

  private updateBody() {
    const { amount, categoryId, accountId, date, description, tags } = this.model();
    return {
      amount,
      categoryId,
      accountId,
      date,
      description: description.trim() || null,
      tags,
    };
  }

  /** "Salva e aggiungi un'altra": an empty form with the same account and date, a new id. */
  private startAnother(date: IsoDate): void {
    this.keptDate.set(date);
    this.nextId.set(newId());
    this.movementForm().reset();
    this.focusAmount();
  }

  private defaultAccount(): string | null {
    const active = this.accounts.active() ?? [];
    const last = this.recent.account();
    return active.find((a) => a.id === last)?.id ?? active[0]?.id ?? null;
  }

  private focusAmount(): void {
    afterNextRender(() => this.movementForm.amount().focusBoundControl(), {
      injector: this.injector,
    });
  }
}
