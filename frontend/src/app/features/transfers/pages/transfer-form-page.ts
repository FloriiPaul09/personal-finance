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
import { RouterLink } from '@angular/router';
import {
  AccountsDirectory,
  ApiError,
  ErrorState,
  newId,
  Notifier,
  PageExit,
  RecentChoices,
  TransferResponse,
} from '../../../core';
import { apiFormErrors, focusFirstError } from '../../../core/forms';
import { AccountSelect, MovementKindSwitch } from '../../../core/selectors';
import { DateInput } from '../../../shared/date-pickers';
import { IsoDate, todayIso } from '../../../shared/dates';
import { AmountInput } from '../../../shared/money';
import { confirmAction, EmptyState } from '../../../shared/ui';
import { TransfersStore } from '../data/transfers-store';

/** Longest description the API accepts. */
export const DESCRIPTION_MAX_LENGTH = 200;

const TRANSACTIONS_PATH = '/transactions';

interface TransferModel {
  fromAccountId: string | null;
  toAccountId: string | null;
  amount: number | null;
  date: IsoDate | null;
  description: string;
}

interface Start {
  readonly key: string;
  readonly model: TransferModel;
}

/**
 * A transfer between two accounts of the same currency (`/transfers/new`, `/transfers/:id`):
 * one form for both legs, which the API writes together (docs/backend/api-endpoints.md#transfers).
 * Same behavior as the movement form: id chosen when the form opens, "Annulla" after saving,
 * delete with a confirmation, current data after a `412`.
 */
@Component({
  selector: 'app-transfer-form-page',
  imports: [
    AccountSelect,
    AmountInput,
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
    RouterLink,
  ],
  templateUrl: './transfer-form-page.html',
  styleUrl: './transfer-form-page.scss',
})
export class TransferFormPage {
  private readonly store = inject(TransfersStore);
  private readonly accounts = inject(AccountsDirectory);
  private readonly recent = inject(RecentChoices);
  private readonly notifier = inject(Notifier);
  private readonly exit = inject(PageExit);
  private readonly dialog = inject(MatDialog);
  private readonly injector = inject(Injector);

  /** From the route: absent for a new transfer. */
  readonly id = input<string>();

  protected readonly transactionsPath = TRANSACTIONS_PATH;
  protected readonly isNew = computed(() => !this.id());
  protected readonly existing = resource({
    params: () => this.id(),
    loader: ({ params }) => this.store.load(params),
  });
  private readonly nextId = signal(newId());
  protected readonly changedElsewhere = signal(false);

  protected readonly ready = computed(() =>
    this.isNew() ? this.accounts.all() !== undefined : this.existing.hasValue(),
  );

  private readonly start = computed<Start>(
    () => {
      const existing = this.existing.hasValue() ? this.existing.value() : undefined;
      if (existing) {
        return {
          key: `${existing.id}:${existing.version}`,
          model: {
            fromAccountId: existing.fromAccountId,
            toAccountId: existing.toAccountId,
            amount: existing.amount,
            date: existing.date,
            description: existing.description ?? '',
          },
        };
      }
      return {
        key: `new:${this.nextId()}:${this.ready()}`,
        model: {
          fromAccountId: this.defaultAccount(),
          toAccountId: null,
          amount: null,
          date: todayIso(),
          description: '',
        },
      };
    },
    { equal: (a, b) => a.key === b.key },
  );

  protected readonly model = linkedSignal(() => this.start().model);
  protected readonly transferForm = form(this.model, (path) => {
    required(path.fromAccountId, {
      message: $localize`:Transfer form error:Scegli il conto di origine.`,
    });
    required(path.toAccountId, {
      message: $localize`:Transfer form error:Scegli il conto di destinazione.`,
    });
    validate(path.toAccountId, ({ value, valueOf }) => {
      const from = this.accounts.byId(valueOf(path.fromAccountId));
      const to = this.accounts.byId(value());
      if (!from || !to) {
        return undefined;
      }
      if (from.id === to.id) {
        return { kind: 'same', message: $localize`:Transfer form error:Scegli due conti diversi.` };
      }
      return from.currency === to.currency
        ? undefined
        : {
            kind: 'currency',
            message: $localize`:Transfer form error:I due conti devono avere la stessa valuta.`,
          };
    });
    required(path.amount, { message: $localize`:Transfer form error:Inserisci l'importo.` });
    validate(path.amount, ({ value }) =>
      value() !== null && value()! <= 0
        ? {
            kind: 'positive',
            message: $localize`:Transfer form error:L'importo deve essere maggiore di zero.`,
          }
        : undefined,
    );
    required(path.date, { message: $localize`:Transfer form error:Inserisci la data.` });
    maxLength(path.description, DESCRIPTION_MAX_LENGTH, {
      message: $localize`:Transfer form error:Al massimo ${DESCRIPTION_MAX_LENGTH}:count: caratteri.`,
    });
  });

  protected readonly currency = computed(
    () => this.accounts.byId(this.model().fromAccountId)?.currency ?? 'EUR',
  );

  constructor() {
    effect(() => {
      if (this.isNew() && this.ready()) {
        untracked(() =>
          afterNextRender(() => this.transferForm.amount().focusBoundControl(), {
            injector: this.injector,
          }),
        );
      }
    });
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.changedElsewhere.set(false);
    await submit(this.transferForm, {
      action: async () => {
        const existing = this.existing.hasValue() ? this.existing.value() : undefined;
        const { fromAccountId, toAccountId, amount, date, description } = this.model();
        const body = {
          fromAccountId,
          toAccountId,
          amount,
          date,
          description: description.trim() || null,
        };
        let saved: TransferResponse;
        try {
          saved = existing
            ? await this.store.update(existing, body)
            : await this.store.create({ id: this.nextId(), ...body });
        } catch (error) {
          if (error instanceof ApiError && error.status === 412) {
            this.changedElsewhere.set(true);
            this.existing.reload();
            return undefined;
          }
          const errors = apiFormErrors(this.transferForm, error, {
            fieldsByCode: {
              'transfer.same_account': 'toAccountId',
              'transfer.currency_mismatch': 'toAccountId',
              'account.archived': 'toAccountId',
              'amount.not_positive': 'amount',
              'amount.too_many_decimals': 'amount',
              'amount.out_of_range': 'amount',
              'description.too_long': 'description',
            },
          });
          if (errors) {
            return errors;
          }
          throw error;
        }
        this.recent.remember({ accountId: saved.fromAccountId });
        if (existing) {
          void this.notifier.show(
            $localize`:Snackbar after saving a transfer:Trasferimento salvato.`,
          );
        } else {
          void this.notifier.undoable(
            $localize`:Snackbar after saving a new transfer:Trasferimento salvato.`,
            () => void this.store.delete(saved),
          );
        }
        await this.exit.leave(TRANSACTIONS_PATH);
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
      title: $localize`:Title of the transfer delete confirmation:Eliminare questo trasferimento?`,
      message: $localize`:Message of the transfer delete confirmation:Spariscono entrambi i movimenti e i saldi tornano come prima. L'eliminazione non si può annullare.`,
      confirm: $localize`:Button that confirms a delete:Elimina`,
      cancel: $localize`:Button that closes a confirmation:Annulla`,
    });
    if (!confirmed) {
      return;
    }
    await this.store.delete(existing);
    void this.notifier.show(
      $localize`:Snackbar after deleting a transfer:Trasferimento eliminato.`,
    );
    await this.exit.leave(TRANSACTIONS_PATH);
  }

  private defaultAccount(): string | null {
    const active = this.accounts.active() ?? [];
    const last = this.recent.account();
    return active.find((a) => a.id === last)?.id ?? active[0]?.id ?? null;
  }
}
