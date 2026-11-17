import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BudgetFigures, BudgetProgress, budgetState } from './budget-progress';

@Component({
  imports: [BudgetProgress],
  template: `<app-budget-progress [figures]="figures()" currency="EUR" label="Ristoranti" />`,
})
class Host {
  readonly figures = signal<BudgetFigures>({
    limit: 350,
    spent: 120.25,
    remaining: 229.75,
    ratio: 0.3436,
    isOverBudget: false,
  });
}

describe('BudgetProgress', () => {
  let fixture: ComponentFixture<Host>;

  beforeEach(() => {
    fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
  });

  const progress = () => fixture.nativeElement.querySelector('app-budget-progress') as HTMLElement;
  // The parts, as on screen (the gaps between them come from the CSS).
  const text = () =>
    Array.from(progress().querySelectorAll('.spent, .limit, .note'))
      .map((part) => part.textContent!.trim())
      .join(' ')
      .replace(/\s+/g, ' '); // Intl puts a no-break space before the currency
  const bar = () => progress().querySelector('mat-progress-bar') as HTMLElement;

  async function show(figures: BudgetFigures): Promise<void> {
    fixture.componentInstance.figures.set(figures);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('shows what was spent of the limit and what is left', () => {
    expect(text()).toBe('120,25 € di 350,00 € Restano 229,75 €');
    expect(bar().getAttribute('aria-valuenow')).toBe('34.36');
    expect(bar().getAttribute('aria-label')).toBe('Ristoranti');
    expect(bar().getAttribute('aria-valuetext')).toBe('34%');
    expect(progress().dataset['state']).toBe('ok');
  });

  it('warns from 90% of the limit', async () => {
    await show({ limit: 100, spent: 95, remaining: 5, ratio: 0.95, isOverBudget: false });

    expect(progress().dataset['state']).toBe('warning');
    expect(progress().classList).toContain('warning');
    expect(text()).toContain('Restano 5,00 €');
  });

  it('shows by how much it went over, with the bar full', async () => {
    await show({ limit: 100, spent: 130, remaining: -30, ratio: 1.3, isOverBudget: true });

    expect(progress().dataset['state']).toBe('over');
    expect(text()).toBe('130,00 € di 100,00 € Superato di 30,00 €');
    expect(bar().getAttribute('aria-valuenow')).toBe('100');
    expect(bar().getAttribute('aria-valuetext')).toBe('130%');
    expect(progress().querySelector('.note')!.classList).toContain('pf-expense');
  });

  it.each([
    [0, false, 'ok'],
    [0.899, false, 'ok'],
    [0.9, false, 'warning'],
    [1, false, 'warning'],
    [1.01, true, 'over'],
  ] as const)('ratio %s (over: %s) is %s', (ratio, isOverBudget, state) => {
    expect(budgetState({ ratio, isOverBudget })).toBe(state);
  });
});
