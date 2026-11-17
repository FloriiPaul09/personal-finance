import { JsonPipe } from '@angular/common';
import { Component, signal } from '@angular/core';
import { form, FormField, required, submit, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ApiError, CategoryKind, ErrorState } from '../../../core';
import { focusFirstError } from '../../../core/forms';
import { AccountSelect, CategorySelect } from '../../../core/selectors';
import { chartColor, DonutChart, DonutSlice } from '../../../shared/charts';
import { DateInput, MonthSelector } from '../../../shared/date-pickers';
import { currentMonth, formatIsoDate, IsoMonth, monthRange, todayIso } from '../../../shared/dates';
import { AmountInput, AmountPipe } from '../../../shared/money';
import { BudgetFigures, BudgetProgress, EmptyState } from '../../../shared/ui';

/**
 * Catalog of the shared components, to try them on a phone and a desktop with real data shapes
 * (docs/frontend/testing.md). Only in development: the route is not matched in a production build.
 * Texts are not translated: nobody but developers sees this page.
 */
@Component({
  selector: 'app-components-page',
  imports: [
    AccountSelect,
    AmountInput,
    CategorySelect,
    AmountPipe,
    BudgetProgress,
    DateInput,
    DonutChart,
    EmptyState,
    ErrorState,
    FormField,
    JsonPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MonthSelector,
  ],
  templateUrl: './components-page.html',
  styleUrl: './components-page.scss',
})
export class ComponentsPage {
  protected readonly currencies = ['EUR', 'USD', 'CHF'];
  protected readonly currency = signal('EUR');

  /** The three states of a budget: normal, from 90% ("attenzione"), over the limit. */
  protected readonly budgets: (BudgetFigures & { label: string })[] = [
    {
      label: 'Normale',
      limit: 350,
      spent: 120.25,
      remaining: 229.75,
      ratio: 0.3436,
      isOverBudget: false,
    },
    {
      label: 'Attenzione',
      limit: 200,
      spent: 186,
      remaining: 14,
      ratio: 0.93,
      isOverBudget: false,
    },
    {
      label: 'Superato',
      limit: 100,
      spent: 130.5,
      remaining: -30.5,
      ratio: 1.305,
      isOverBudget: true,
    },
  ];

  /** Seven parts: the five chart colors in their order, then two in the neutral color. */
  protected readonly slices: DonutSlice[] = [
    ['Spesa', 412.3],
    ['Casa', 350],
    ['Carburante', 120.5],
    ['Svago', 86],
    ['Salute', 45.9],
    ['Regali', 30],
    ['Altro', 12],
  ].map(([label, value], index) => ({
    label: label as string,
    value: value as number,
    color: chartColor(index),
  }));
  protected readonly formatSlice = (value: number) => `${value} ${this.currency()}`;

  protected readonly entry = signal<{
    amount: number | null;
    balance: number | null;
    date: string | null;
  }>({
    amount: null,
    balance: 0,
    date: todayIso(),
  });
  protected readonly entryForm = form(this.entry, (path) => {
    required(path.amount, { message: "Inserisci l'importo." });
    validate(path.amount, ({ value }) =>
      value() !== null && value()! <= 0
        ? { kind: 'positive', message: "L'importo deve essere maggiore di zero." }
        : undefined,
    );
    required(path.date, { message: 'Inserisci la data.' });
  });

  protected readonly month = signal<IsoMonth>(currentMonth());
  protected readonly max = currentMonth();
  protected readonly monthRange = monthRange;

  protected readonly samples = [1234.5, 0.1, -12, 999999999999.99];
  protected readonly today = todayIso();
  protected readonly formatIsoDate = formatIsoDate;

  protected readonly offline = new ApiError(0, 'network.unavailable');
  protected readonly serverError = new ApiError(
    500,
    'server.error',
    {},
    '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01',
  );
  protected readonly loading = signal(true);

  protected readonly kind = signal<CategoryKind>('expense');
  protected readonly choice = signal<{
    accountId: string | null;
    categoryId: string | null;
    parentId: string | null;
  }>({ accountId: null, categoryId: null, parentId: null });
  protected readonly choiceForm = form(this.choice, (path) => {
    required(path.accountId, { message: 'Scegli il conto.' });
    required(path.categoryId, { message: 'Scegli la categoria.' });
  });

  protected async choose(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.choiceForm, {
      action: async () => undefined,
      onInvalid: (field) => focusFirstError(field),
    });
  }

  /** What a page would send to the API (nothing is sent here). */
  protected readonly sent = signal<unknown>(null);

  protected async send(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.entryForm, async () => {
      this.sent.set(this.entry());
      return undefined;
    });
  }

  protected reset(): void {
    this.entryForm().reset({ amount: null, balance: 0, date: todayIso() });
    this.sent.set(null);
  }
}
