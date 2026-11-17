import { Component, computed, input } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AmountPipe } from '../money';

/** From this share of the limit on, the progress shows "attenzione". */
export const BUDGET_WARNING_RATIO = 0.9;

export type BudgetState = 'ok' | 'warning' | 'over';

/** What a budget looks like this month, as the API computes it (no arithmetic here). */
export interface BudgetFigures {
  readonly limit: number;
  readonly spent: number;
  readonly remaining: number;
  /** `spent / limit`, above 1 when over budget. */
  readonly ratio: number;
  readonly isOverBudget: boolean;
}

export function budgetState(figures: Pick<BudgetFigures, 'ratio' | 'isOverBudget'>): BudgetState {
  if (figures.isOverBudget) {
    return 'over';
  }
  return figures.ratio >= BUDGET_WARNING_RATIO ? 'warning' : 'ok';
}

/**
 * The progress of a budget in a month: "120,00 € di 350,00 €", a bar, then what is left or by
 * how much it went over. Normal up to 90% of the limit, "attenzione" (`--pf-warning`, amber)
 * from there, red above the limit. Used by the budgets page and, later, the dashboard; it takes the
 * figures of the API and never adds amounts itself.
 */
@Component({
  selector: 'app-budget-progress',
  imports: [AmountPipe, MatProgressBarModule],
  template: `
    <div class="figures">
      <span class="pf-amount spent">{{ figures().spent | amount: currency() }}</span>
      <span class="limit" i18n="Limit of a budget, after what was spent"
        >di {{ figures().limit | amount: currency() }}</span
      >
    </div>
    <mat-progress-bar
      mode="determinate"
      [value]="percent()"
      [attr.aria-label]="label()"
      [attr.aria-valuetext]="valueText()"
    />
    @if (state() === 'over') {
      <span class="note pf-expense" data-testid="budget-note" i18n="Budget over its limit"
        >Superato di {{ overBy() | amount: currency() }}</span
      >
    } @else {
      <span class="note" data-testid="budget-note" i18n="What is left of a budget"
        >Restano {{ figures().remaining | amount: currency() }}</span
      >
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 4px;
      --mat-progress-bar-track-height: 8px;
      --mat-progress-bar-active-indicator-height: 8px;
      --mat-progress-bar-track-shape: 4px;
    }

    :host(.warning) {
      --mat-progress-bar-active-indicator-color: var(--pf-warning);
      --mat-progress-bar-track-color: var(--pf-warning-container);
    }

    :host(.over) {
      --mat-progress-bar-active-indicator-color: var(--mat-sys-error);
      --mat-progress-bar-track-color: var(--mat-sys-error-container);
    }

    .figures {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0 6px;
    }

    .spent {
      font: var(--mat-sys-title-medium);
    }

    .limit,
    .note {
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
    }

    .note.pf-expense {
      color: var(--pf-expense);
    }
  `,
  host: {
    '[class.warning]': "state() === 'warning'",
    '[class.over]': "state() === 'over'",
    '[attr.data-state]': 'state()',
  },
})
export class BudgetProgress {
  readonly figures = input.required<BudgetFigures>();
  readonly currency = input.required<string>();
  /** What the bar is about, for screen readers (e.g. the category). */
  readonly label = input.required<string>();

  protected readonly state = computed(() => budgetState(this.figures()));
  protected readonly percent = computed(() =>
    Math.min(100, Math.max(0, this.figures().ratio * 100)),
  );
  /** How much above the limit (the API's `remaining` is negative then). */
  protected readonly overBy = computed(() => -this.figures().remaining);
  protected readonly valueText = computed(() => `${Math.round(this.figures().ratio * 100)}%`);
}
