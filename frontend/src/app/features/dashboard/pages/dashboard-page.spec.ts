import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { CategoryResponse, CategorySpendingResponse, DashboardResponse } from '../../../core';
import {
  aBudgetProgress,
  aCategory,
  anAccount,
  aDashboard,
  problem,
  provideApiHttpTesting,
} from '../../../core/testing';
import { CHART_OTHER_COLOR } from '../../../shared/charts';
import { addMonthsToMonth, currentMonth } from '../../../shared/dates';
import { DashboardPage } from './dashboard-page';

describe('DashboardPage', () => {
  let fixture: ComponentFixture<DashboardPage>;
  let backend: HttpTestingController;

  const month = currentMonth();
  const names = ['Spesa', 'Carburante', 'Casa', 'Svago', 'Salute', 'Regali'];
  const categories = names.map((name) => aCategory({ name }));
  const spendingOf = (list: CategoryResponse[], currency = 'EUR'): CategorySpendingResponse[] =>
    list.map((category, index) => ({ categoryId: category.id, currency, spent: 300 - index * 50 }));

  beforeEach(() => {
    // jsdom has no canvas: the donut is configured but never drawn.
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [provideApiHttpTesting(), provideRouter([])],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DashboardPage);
  });

  afterEach(() => backend.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const all = (selector: string) => Array.from(el().querySelectorAll<HTMLElement>(selector));
  const one = (selector: string) => el().querySelector<HTMLElement>(selector)!;
  const text = (element: Element | null) => element?.textContent?.replace(/\s+/g, ' ').trim();
  const testId = (id: string) => all(`[data-testid="${id}"]`);

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  async function open(
    data: DashboardResponse,
    urlMonth: string | undefined = month,
    sent: string = month,
  ): Promise<void> {
    fixture.componentRef.setInput('month', urlMonth);
    fixture.detectChanges();
    backend.expectOne(`/api/dashboard?month=${sent}`).flush(data);
    await settle();
    backend
      .match((r) => r.url === '/api/categories')
      .forEach((request) => request.flush({ items: categories }));
    await settle();
    await settle();
  }

  it('invites to add the first account when there is none', async () => {
    await open(aDashboard({ accounts: [], balances: [] }));

    expect(text(one('[data-testid="first-use"]'))).toContain('Aggiungi il tuo primo conto');
    expect(one('[data-testid="first-use"] a').getAttribute('href')).toBe('/accounts/new');
    expect(el().querySelector('app-month-selector')).toBeNull();
    expect(el().querySelector('[data-testid="totals"]')).toBeNull();
  });

  it('shows income, expenses and net of the month', async () => {
    await open(
      aDashboard({ totals: [{ currency: 'EUR', income: 2000, expense: 180.5, net: 1819.5 }] }),
    );

    const [row] = testId('totals-row');
    expect(text(row.querySelector('[data-testid="income"]'))).toBe('2.000,00 €');
    expect(text(row.querySelector('[data-testid="expense"]'))).toBe('180,50 €');
    expect(text(row.querySelector('[data-testid="net"]'))).toBe('+1.819,50 €');
    expect(row.querySelector('[data-testid="net"]')!.classList).not.toContain('pf-expense');
  });

  it('shows a negative net in the expense color', async () => {
    await open(aDashboard({ totals: [{ currency: 'EUR', income: 0, expense: 40, net: -40 }] }));

    const net = one('[data-testid="net"]');
    expect(text(net)).toBe('-40,00 €');
    expect(net.classList).toContain('pf-expense');
  });

  it('invites to add an expense when the month has no movements', async () => {
    await open(aDashboard());

    const totals = one('[data-testid="totals"]');
    expect(text(totals)).toContain('Nessun movimento in questo mese.');
    expect(totals.querySelector('.empty a')!.getAttribute('href')).toBe('/transactions/new');
    const spending = one('[data-testid="spending"]');
    expect(text(spending)).toContain('Nessuna spesa in questo mese.');
    expect(el().querySelector('app-donut-chart')).toBeNull();
  });

  describe('budgets', () => {
    const progress = (index: number, ratio: number) =>
      aBudgetProgress({ categoryId: categories[index].id, ratio, isOverBudget: ratio > 1 });

    it('shows the three closest to their limit, and a link to all of them', async () => {
      await open(
        aDashboard({
          budgets: [progress(0, 0.2), progress(1, 1.3), progress(2, 0.95), progress(3, 0.5)],
        }),
      );

      const rows = testId('budget-row');
      expect(rows.map((row) => text(row.querySelector('.name')))).toEqual([
        'Carburante',
        'Casa',
        'Svago',
      ]);
      expect(
        rows.map((row) => row.querySelector('app-budget-progress')!.getAttribute('data-state')),
      ).toEqual(['over', 'warning', 'ok']);
      expect(rows[0].getAttribute('href')).toBe(
        `/transactions?categoryId=${categories[1].id}&month=${month}`,
      );
      const link = one('[data-testid="all-budgets"]');
      expect(text(link)).toContain('Vedi tutti (4)');
      expect(link.getAttribute('href')).toBe(`/budgets?month=${month}`);
    });

    it('links to the budgets page when every budget is shown', async () => {
      await open(aDashboard({ budgets: [progress(0, 0.2)] }));

      expect(text(one('[data-testid="all-budgets"]'))).toContain('Vai ai budget');
    });

    it('invites to set a budget for the month when there is none', async () => {
      await open(aDashboard());

      const section = one('[data-testid="budgets"]');
      expect(text(section)).toContain('Nessun budget in questo mese.');
      expect(section.querySelector('.empty a')!.getAttribute('href')).toBe(
        `/budgets/new?month=${month}`,
      );
      expect(el().querySelector('[data-testid="all-budgets"]')).toBeNull();
    });
  });

  describe('spending', () => {
    const chart = () =>
      fixture.debugElement
        .query(By.directive(BaseChartDirective))
        .injector.get(BaseChartDirective) as BaseChartDirective<'doughnut'>;
    const swatches = () =>
      testId('spending-row').map(
        (row) => row.querySelector<HTMLElement>('.swatch')!.style.background,
      );

    it('lists the first five categories, largest first, each opening its expenses', async () => {
      await open(aDashboard({ spending: spendingOf(categories) }));

      const rows = testId('spending-row');
      expect(rows.map((row) => text(row.querySelector('.name')))).toEqual(names.slice(0, 5));
      expect(text(rows[0].querySelector('.pf-amount'))).toBe('300,00 €');
      expect(rows[0].getAttribute('href')).toBe(
        `/transactions?categoryId=${categories[0].id}&month=${month}`,
      );
      expect(swatches()).toEqual([1, 2, 3, 4, 5].map((slot) => `var(--pf-chart-${slot})`));
    });

    it('draws every category in the donut, the ones after the fifth in the neutral color', async () => {
      await open(aDashboard({ spending: spendingOf(categories) }));

      expect(chart().data!.labels).toEqual(names);
      expect(chart().data!.datasets[0].data).toEqual([300, 250, 200, 150, 100, 50]);
      expect((chart().data!.datasets[0].backgroundColor as string[])[5]).toBe(CHART_OTHER_COLOR);
      expect(one('canvas').getAttribute('aria-label')).toContain('per categoria in EUR');
    });

    it('shows the other categories on request, with their colors unchanged', async () => {
      await open(aDashboard({ spending: spendingOf(categories) }));
      const before = swatches();

      const more = one('[data-testid="spending-more"]');
      expect(text(more)).toContain('Mostra tutte (6)');
      expect(more.getAttribute('aria-expanded')).toBe('false');
      more.click();
      fixture.detectChanges();

      expect(testId('spending-row')).toHaveLength(6);
      expect(swatches().slice(0, 5)).toEqual(before);
      expect(swatches()[5]).toBe(CHART_OTHER_COLOR);
      expect(text(more)).toContain('Mostra meno');
      expect(more.getAttribute('aria-expanded')).toBe('true');
    });

    it('has no "Mostra tutte" with five categories or fewer', async () => {
      await open(aDashboard({ spending: spendingOf(categories.slice(0, 5)) }));

      expect(testId('spending-more')).toHaveLength(0);
    });
  });

  describe('accounts', () => {
    it('lists the accounts with their current balance and the total', async () => {
      const checking = anAccount({ name: 'Conto corrente', balance: 1520.5 });
      const card = anAccount({ name: 'Carta', type: 'creditCard', balance: -30 });
      await open(
        aDashboard({
          accounts: [card, checking],
          balances: [{ currency: 'EUR', balance: 1490.5, archivedBalance: 0 }],
        }),
      );

      const rows = testId('account-row');
      expect(rows.map((row) => text(row.querySelector('.name')))).toEqual([
        'Carta',
        'Conto corrente',
      ]);
      expect(text(rows[0].querySelector('.pf-amount'))).toBe('-30,00 €');
      expect(rows[0].querySelector('.pf-amount')!.classList).toContain('pf-expense');
      expect(rows[0].querySelector('mat-icon')!.getAttribute('data-mat-icon-name')).toBe(
        'credit_card',
      );
      expect(rows[1].getAttribute('href')).toBe(
        `/transactions?accountId=${checking.id}&month=${month}`,
      );
      const [total] = testId('balance-total');
      expect(text(total.querySelector('.name'))).toBe('Totale');
      expect(text(total.querySelector('.pf-amount'))).toBe('1.490,50 €');
      expect(text(one('[data-testid="accounts"] h2'))).toBe('Saldi attuali');
    });

    it('says how much of the total sits on archived accounts', async () => {
      await open(
        aDashboard({
          accounts: [],
          balances: [{ currency: 'EUR', balance: 50, archivedBalance: 50 }],
        }),
      );

      expect(text(one('[data-testid="accounts"]'))).toContain('Nessun conto attivo.');
      expect(text(one('[data-testid="balance-total"]'))).toContain(
        'di cui 50,00 € su conti archiviati',
      );
      expect(el().querySelector('[data-testid="first-use"]')).toBeNull();
    });
  });

  it('keeps currencies apart: a row of figures, a donut and a total each', async () => {
    const usd = anAccount({ name: 'Wise', currency: 'USD', balance: 200 });
    await open(
      aDashboard({
        accounts: [anAccount({ balance: 100 }), usd],
        balances: [
          { currency: 'EUR', balance: 100, archivedBalance: 0 },
          { currency: 'USD', balance: 200, archivedBalance: 0 },
        ],
        totals: [
          { currency: 'EUR', income: 0, expense: 10, net: -10 },
          { currency: 'USD', income: 0, expense: 20, net: -20 },
        ],
        spending: [
          ...spendingOf(categories.slice(0, 1)),
          ...spendingOf(categories.slice(1, 2), 'USD'),
        ],
      }),
    );

    expect(testId('totals-row').map((row) => row.dataset['currency'])).toEqual(['EUR', 'USD']);
    expect(all('[data-testid="spending"] h3').map(text)).toEqual(['EUR', 'USD']);
    expect(all('app-donut-chart')).toHaveLength(2);
    expect(testId('balance-total').map((total) => text(total.querySelector('.name')))).toEqual([
      'Totale in EUR',
      'Totale in USD',
    ]);
    expect(testId('balance-total').map((total) => total.dataset['currency'])).toEqual([
      'EUR',
      'USD',
    ]);
  });

  describe('month', () => {
    it.each([undefined, 'not-a-month', addMonthsToMonth(currentMonth(), 1)])(
      'reads the current month when the URL has %s',
      async (urlMonth) => {
        await open(aDashboard(), urlMonth);

        expect(text(one('app-month-selector'))).not.toContain('Mese corrente');
      },
    );

    it('reads a past month from the URL and offers to go back to the current one', async () => {
      const past = addMonthsToMonth(month, -2);
      await open(aDashboard(), past, past);

      expect(text(one('app-month-selector'))).toContain('Mese corrente');
    });

    it('writes the month chosen in the URL, replacing the entry', async () => {
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
      await open(aDashboard());

      one('app-month-selector button').click();

      expect(navigate).toHaveBeenCalledWith(
        [],
        expect.objectContaining({
          queryParams: { month: addMonthsToMonth(month, -1) },
          replaceUrl: true,
        }),
      );
    });
  });

  it('shows the error with a retry that reads again', async () => {
    fixture.componentRef.setInput('month', month);
    fixture.detectChanges();
    backend.expectOne(`/api/dashboard?month=${month}`).flush(...problem(500, 'server.error'));
    await settle();

    expect(el().querySelector('app-error-state')).not.toBeNull();
    one('app-error-state button').click();
    fixture.detectChanges();
    backend.expectOne(`/api/dashboard?month=${month}`).flush(aDashboard());
    await settle();
    backend
      .match((r) => r.url === '/api/categories')
      .forEach((request) => request.flush({ items: [] }));
    await settle();
    expect(el().querySelector('app-error-state')).toBeNull();
    expect(el().querySelector('[data-testid="totals"]')).not.toBeNull();
  });
});
