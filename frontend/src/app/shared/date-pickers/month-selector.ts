import { Component, computed, inject, input, LOCALE_ID, model } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { addMonthsToMonth, currentMonth, formatMonth, IsoMonth } from '../dates';

/**
 * The month a page shows: `<app-month-selector [(month)]="month" />` → ‹ Novembre 2026 ›.
 * Budgets and the dashboard always send the month they show to the API (`?month=2026-11`), so
 * the page keeps it in its URL and passes it here. "Mese corrente" appears when another month
 * is shown. `max` stops the arrows at a month (e.g. no dashboard of the future).
 */
@Component({
  selector: 'app-month-selector',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './month-selector.html',
  styleUrl: './month-selector.scss',
})
export class MonthSelector {
  private readonly locale = inject(LOCALE_ID);

  readonly month = model.required<IsoMonth>();
  readonly max = input<IsoMonth>();

  protected readonly current = currentMonth();
  protected readonly label = computed(() => capitalize(formatMonth(this.month(), this.locale)));
  protected readonly isCurrent = computed(() => this.month() === this.current);
  protected readonly atMax = computed(() => {
    const max = this.max();
    return max !== undefined && this.month() >= max;
  });

  protected move(months: number): void {
    this.month.set(addMonthsToMonth(this.month(), months));
  }

  protected backToCurrent(): void {
    this.month.set(this.current);
  }
}

function capitalize(text: string): string {
  return text.charAt(0).toLocaleUpperCase() + text.slice(1);
}
