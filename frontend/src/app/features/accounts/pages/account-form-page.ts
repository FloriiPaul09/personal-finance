import { Component, computed, inject, input, linkedSignal, LOCALE_ID, signal } from '@angular/core';
import {
  disabled,
  form,
  FormField,
  maxLength,
  required,
  submit,
  validate,
} from '@angular/forms/signals';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import {
  AccountRequest,
  AccountResponse,
  AccountType,
  ApiError,
  ErrorState,
  Notifier,
} from '../../../core';
import { apiFormErrors, focusFirstError } from '../../../core/forms';
import {
  AmountInput,
  isCurrencyCode,
  normalizeCurrency,
  searchCurrencies,
} from '../../../shared/money';
import { EmptyState } from '../../../shared/ui';
import { ACCOUNT_TYPES } from '../data/account-types';
import { AccountsStore } from '../data/accounts-store';

/** Longest name the API accepts. */
export const ACCOUNT_NAME_MAX_LENGTH = 100;

interface AccountModel {
  name: string;
  type: AccountType;
  currency: string;
  initialBalance: number | null;
}

const NEW_ACCOUNT: AccountModel = {
  name: '',
  type: 'checking',
  currency: 'EUR',
  initialBalance: 0,
};

function toModel(account: AccountResponse | undefined): AccountModel {
  return account
    ? {
        name: account.name,
        type: account.type,
        currency: account.currency,
        initialBalance: account.initialBalance,
      }
    : { ...NEW_ACCOUNT };
}

/** Same account at the same version: a list read again with nothing changed resets nothing. */
function sameVersion(a: AccountResponse | undefined, b: AccountResponse | undefined): boolean {
  return a?.id === b?.id && a?.version === b?.version;
}

const ACCOUNTS_PATH = '/accounts';

/**
 * New account (`/accounts/new`) and changes to one (`/accounts/:id`). The account comes from the
 * list in core, with its version for `If-Match`. When it changed elsewhere (`412`) the list is
 * read again and the form shows the current data, so the person decides again
 * (docs/frontend/state-management.md#versions).
 */
@Component({
  selector: 'app-account-form-page',
  imports: [
    AmountInput,
    EmptyState,
    ErrorState,
    FormField,
    MatAutocompleteModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    RouterLink,
  ],
  templateUrl: './account-form-page.html',
  styleUrl: './account-form-page.scss',
})
export class AccountFormPage {
  protected readonly store = inject(AccountsStore);
  private readonly router = inject(Router);
  private readonly notifier = inject(Notifier);
  private readonly locale = inject(LOCALE_ID);

  /** From the route: absent for a new account. */
  readonly id = input<string>();

  protected readonly accountsPath = ACCOUNTS_PATH;
  protected readonly types = ACCOUNT_TYPES;
  protected readonly isNew = computed(() => !this.id());
  protected readonly account = computed(() => this.store.byId(this.id()), {
    equal: sameVersion,
  });
  protected readonly notFound = computed(
    () => !this.isNew() && this.store.all() !== undefined && !this.account(),
  );
  /** The account changed elsewhere while the form was open (`412`). */
  protected readonly changedElsewhere = signal(false);

  protected readonly model = linkedSignal(() => toModel(this.account()));
  protected readonly accountForm = form(this.model, (path) => {
    required(path.name, { message: $localize`:Account form error:Inserisci il nome del conto.` });
    validate(path.name, ({ value }) =>
      value().trim() === ''
        ? { kind: 'blank', message: $localize`:Account form error:Inserisci il nome del conto.` }
        : undefined,
    );
    maxLength(path.name, ACCOUNT_NAME_MAX_LENGTH, {
      message: $localize`:Account form error:Al massimo ${ACCOUNT_NAME_MAX_LENGTH}:count: caratteri.`,
    });
    required(path.currency, { message: $localize`:Account form error:Inserisci la valuta.` });
    validate(path.currency, ({ value }) =>
      value() === '' || isCurrencyCode(value())
        ? undefined
        : {
            kind: 'currency',
            message: $localize`:Account form error:Usa un codice di tre lettere, per esempio EUR.`,
          },
    );
    // The API refuses a new currency once a movement uses the account.
    disabled(path.currency, () => this.account()?.hasMovements ?? false);
    required(path.initialBalance, {
      message: $localize`:Account form error:Inserisci il saldo iniziale, anche 0.`,
    });
  });

  protected readonly currencies = computed(() =>
    searchCurrencies(this.model().currency, this.locale),
  );
  /** The currency of the amount field: the one typed, once it is a valid code. */
  protected readonly amountCurrency = computed(() => {
    const currency = this.model().currency;
    return isCurrencyCode(currency) ? normalizeCurrency(currency) : 'EUR';
  });

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.changedElsewhere.set(false);
    await submit(this.accountForm, {
      action: async () => {
        const account = this.account();
        try {
          if (account) {
            await this.store.update(account, this.request());
          } else {
            await this.store.create(this.request());
          }
        } catch (error) {
          if (error instanceof ApiError && error.status === 412) {
            // The store reads the list again: the form shows the current data.
            this.changedElsewhere.set(true);
            return undefined;
          }
          const errors = apiFormErrors(this.accountForm, error, {
            fieldsByCode: {
              'account.name_taken': 'name',
              'account.currency_locked': 'currency',
              'currency.invalid': 'currency',
              'amount.too_many_decimals': 'initialBalance',
              'amount.out_of_range': 'initialBalance',
            },
          });
          if (errors) {
            return errors;
          }
          throw error;
        }
        void this.notifier.show(
          account
            ? $localize`:Snackbar after saving an account:Conto salvato.`
            : $localize`:Snackbar after creating an account:Conto creato.`,
        );
        await this.router.navigateByUrl(ACCOUNTS_PATH);
        return undefined;
      },
      onInvalid: (field) => focusFirstError(field),
    });
  }

  private request(): AccountRequest {
    const { name, type, currency, initialBalance } = this.model();
    return {
      name: name.trim(),
      type,
      currency: normalizeCurrency(currency),
      initialBalance,
    };
  }
}
