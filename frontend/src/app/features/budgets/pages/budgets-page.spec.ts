import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { MatMenuHarness } from '@angular/material/menu/testing';
import { provideRouter, Router } from '@angular/router';
import { Notifier } from '../../../core';
import {
  aBudget,
  aBudgetProgress,
  aCategory,
  flushCategories,
  provideApiHttpTesting,
} from '../../../core/testing';
import { currentMonth, isIsoMonth } from '../../../shared/dates';
import { BudgetsPage } from './budgets-page';

describe('BudgetsPage', () => {
  let fixture: ComponentFixture<BudgetsPage>;
  let backend: HttpTestingController;
  let loader: HarnessLoader;
  let undoable: ReturnType<typeof vi.fn>;

  const casa = aCategory({ name: 'Casa', hasSubcategories: true });
  const affitto = aCategory({ name: 'Affitto', parentId: casa.id });
  const svago = aCategory({ name: 'Svago' });
  const affittoBudget = aBudgetProgress({ categoryId: affitto.id, month: '2026-11' });
  const svagoBudget = aBudgetProgress({
    categoryId: svago.id,
    month: '2026-11',
    limit: 100,
    spent: 130,
    remaining: -30,
    ratio: 1.3,
    isOverBudget: true,
  });

  beforeEach(() => {
    undoable = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [
        provideApiHttpTesting(),
        provideRouter([]),
        { provide: Notifier, useValue: { show: vi.fn(), undoable } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(BudgetsPage);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  });

  afterEach(() => backend.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(el().querySelectorAll<HTMLElement>('[data-testid="budget-row"]'));

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  async function open(month: string | undefined, items = [affittoBudget, svagoBudget]) {
    fixture.componentRef.setInput('month', month);
    fixture.detectChanges();
    flushCategories(backend, [casa, affitto, svago]);
    backend.expectOne('/api/budgets').flush({ items: [] });
    const sent = isIsoMonth(month) ? month : currentMonth();
    backend.expectOne(`/api/budgets/progress?month=${sent}`).flush({ items });
    await settle();
    await settle();
  }

  async function nextRequest(url: string, method: string): Promise<TestRequest> {
    for (let attempt = 0; attempt < 50; attempt++) {
      const [request] = backend.match((r) => r.urlWithParams === url && r.method === method);
      if (request) {
        return request;
      }
      await new Promise((resolve) => setTimeout(resolve, 10));
    }
    throw new Error(`No ${method} ${url}`);
  }

  it('shows a row per budget of the month, with its category and progress', async () => {
    await open('2026-11');

    expect(rows().map((row) => row.querySelector('.name')!.textContent!.trim())).toEqual([
      'Casa › Affitto',
      'Svago',
    ]);
    const states = rows().map(
      (row) => row.querySelector<HTMLElement>('app-budget-progress')!.dataset['state'],
    );
    expect(states).toEqual(['ok', 'over']);
    expect(el().querySelector('app-month-selector')!.textContent).toContain('Novembre 2026');
  });

  it('opens the expenses of the category in the month from the row', async () => {
    await open('2026-11');

    const link = rows()[0].querySelector<HTMLAnchorElement>('a.main')!;
    expect(link.getAttribute('href')).toBe(`/transactions?categoryId=${affitto.id}&month=2026-11`);
  });

  it('shows the current month when the URL has none or an invalid one', async () => {
    await open('not-a-month');

    expect(rows()).toHaveLength(2);
  });

  it('suggests setting a budget when the month has none', async () => {
    await open('2026-11', []);

    expect(el().querySelector('app-empty-state')!.textContent).toContain(
      'Nessun budget in questo mese',
    );
    const link = el().querySelector<HTMLAnchorElement>('app-empty-state a')!;
    expect(link.getAttribute('href')).toBe('/new?month=2026-11');
  });

  it('writes another month in the URL', async () => {
    await open('2026-11');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    el().querySelector<HTMLButtonElement>('app-month-selector button')!.click();

    expect(navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { month: '2026-10' }, replaceUrl: true }),
    );
  });

  it('stops a budget from the month shown, and "Annulla" takes the stop back', async () => {
    await open('2026-11');
    const menu = await loader.getHarness(
      MatMenuHarness.with({ selector: '[aria-label="Azioni per Svago"]' }),
    );
    await menu.open();
    const items = await menu.getItems();
    expect(await Promise.all(items.map((item) => item.getText()))).toEqual([
      'Modifica',
      'Ferma da novembre 2026',
    ]);
    await items[1].click();

    (await nextRequest(`/api/budgets?categoryId=${svago.id}`, 'GET')).flush({
      items: [aBudget({ categoryId: svago.id, from: '2026-01', amount: 100 })],
    });
    const post = await nextRequest('/api/budgets', 'POST');
    expect(post.request.body).toEqual({ categoryId: svago.id, from: '2026-11', amount: null });
    const stop = aBudget({ categoryId: svago.id, from: '2026-11', amount: null, version: 4 });
    post.flush(stop);
    await settle();

    expect(undoable).toHaveBeenCalledWith(
      'Budget di Svago fermato da novembre 2026.',
      expect.any(Function),
    );
    undoable.mock.calls[0][1]();
    const del = await nextRequest(`/api/budgets/${stop.id}`, 'DELETE');
    expect(del.request.headers.get('If-Match')).toBe('"4"');
    del.flush(null, { status: 204, statusText: 'No Content' });

    // Each write has the month and the entries read again.
    await settle();
    backend.match('/api/budgets/progress?month=2026-11');
    backend.match('/api/budgets');
    backend.match('/api/categories?includeArchived=true');
  });
});
