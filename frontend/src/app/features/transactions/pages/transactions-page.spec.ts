import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { provideRouter, Router } from '@angular/router';
import { DataChanges } from '../../../core';
import {
  aCategory,
  anAccount,
  aTransaction,
  flushAccounts,
  flushCategories,
  provideApiHttpTesting,
} from '../../../core/testing';
import { currentMonth, monthRange, todayIso } from '../../../shared/dates';
import { TransactionsPage } from './transactions-page';

describe('TransactionsPage', () => {
  let fixture: ComponentFixture<TransactionsPage>;
  let backend: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;

  const account = anAccount({ name: 'Conto corrente' });
  const spesa = aCategory({ name: 'Spesa' });
  const stipendio = aCategory({ name: 'Stipendio', kind: 'income' });
  const today = todayIso();

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [provideApiHttpTesting(), provideRouter([])],
    });
    backend = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(TransactionsPage);
  });

  afterEach(() => {
    backend.match('/api/tags');
    backend.verify();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const listRequest = () =>
    backend.expectOne((r) => r.url === '/api/transactions' && r.method === 'GET');

  async function open(query: Record<string, string> = {}): Promise<TestRequest> {
    for (const [key, value] of Object.entries(query)) {
      fixture.componentRef.setInput(key, value);
    }
    fixture.detectChanges();
    flushAccounts(backend, [account]);
    flushCategories(backend, [spesa, stipendio]);
    return listRequest();
  }

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  const rows = () =>
    Array.from(el().querySelectorAll('[data-testid="movement-row"]')).map((row) =>
      row.textContent!.replace(/\s+/g, ' ').replace(/−/g, '-').trim(),
    );
  const headings = () => Array.from(el().querySelectorAll('h2')).map((h) => h.textContent!.trim());

  it('asks the movements of the current month', async () => {
    const request = await open();

    const { from, to } = monthRange(currentMonth());
    expect(request.request.params.get('from')).toBe(from);
    expect(request.request.params.get('to')).toBe(to);
    expect(request.request.params.has('cursor')).toBe(false);
    request.flush({ items: [], nextCursor: null });
  });

  it('groups the movements by day with category, description, account and signed amount', async () => {
    (await open()).flush({
      items: [
        aTransaction({
          date: today,
          accountId: account.id,
          categoryId: spesa.id,
          signedAmount: -12.5,
        }),
        aTransaction({
          date: today,
          kind: 'income',
          accountId: account.id,
          categoryId: stipendio.id,
          amount: 1500,
          signedAmount: 1500,
          description: null,
        }),
        aTransaction({
          date: '2026-01-05',
          kind: 'transfer',
          categoryId: null,
          transferId: 'tr-1',
          accountId: account.id,
          signedAmount: -300,
          description: 'Risparmio',
        }),
      ],
      nextCursor: null,
    });
    await settle();

    expect(headings()[0]).toBe('Oggi');
    expect(rows()).toEqual([
      'Spesa Pranzo · Conto corrente -12,50 €',
      'Stipendio Conto corrente +1.500,00 €',
      'Trasferimento in uscita Risparmio · Conto corrente -300,00 €',
    ]);
    const links = Array.from(
      el().querySelectorAll<HTMLAnchorElement>('[data-testid="movement-row"] a'),
    );
    expect(links[2].getAttribute('href')).toBe('/transfers/tr-1');
    expect(el().querySelector('.pf-expense')).not.toBeNull();
    expect(el().querySelector('.pf-income')).not.toBeNull();
  });

  it('loads the next page with the cursor', async () => {
    (await open()).flush({ items: [aTransaction({ date: today })], nextCursor: 'c2' });
    await settle();

    el().querySelector<HTMLButtonElement>('[data-testid="load-more"]')!.click();
    const next = listRequest();
    expect(next.request.params.get('cursor')).toBe('c2');
    next.flush({ items: [aTransaction({ date: today })], nextCursor: null });
    await settle();

    expect(rows().length).toBe(2);
    expect(el().querySelector('[data-testid="load-more"]')).toBeNull();
  });

  it('sends the filters of the URL and shows them as chips that remove themselves', async () => {
    const request = await open({ accountId: account.id, kind: 'expense', q: 'pizza' });
    expect(request.request.params.get('accountId')).toBe(account.id);
    expect(request.request.params.get('kind')).toBe('expense');
    expect(request.request.params.get('q')).toBe('pizza');
    request.flush({ items: [], nextCursor: null });
    await settle();

    const chips = Array.from(el().querySelectorAll('mat-chip')).map((c) => c.textContent!.trim());
    expect(chips).toEqual(['Uscite', 'Conto corrente', '«pizza»']);
    expect(el().querySelector('app-empty-state')!.textContent).toContain(
      'Nessun movimento con questi filtri',
    );

    el().querySelector<HTMLButtonElement>('mat-chip button')!.click();
    expect(navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { kind: null }, queryParamsHandling: 'merge' }),
    );
  });

  it('writes a filter chosen in the panel to the URL', async () => {
    (await open()).flush({ items: [], nextCursor: null });
    await settle();

    el().querySelector<HTMLButtonElement>('[data-testid="toggle-filters"]')!.click();
    await settle();
    const search = el().querySelector<HTMLInputElement>('[data-testid="search"]')!;
    search.value = 'cena';
    search.dispatchEvent(new Event('change'));

    expect(navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { q: 'cena' }, replaceUrl: true }),
    );
  });

  it('shows a custom period as a chip instead of the month', async () => {
    (await open({ from: '2026-11-01', to: '2026-11-15' })).flush({ items: [], nextCursor: null });
    await settle();

    expect(el().querySelector('app-month-selector')).toBeNull();
    expect(el().querySelector('mat-chip')!.textContent).toContain('1 nov 2026 – 15 nov 2026');
  });

  it('invites to add an expense when the period is empty', async () => {
    (await open()).flush({ items: [], nextCursor: null });
    await settle();

    expect(el().querySelector('app-empty-state')!.textContent).toContain(
      'Nessun movimento in questo periodo',
    );
  });

  it('reads the list again after a write to movements anywhere', async () => {
    (await open()).flush({ items: [], nextCursor: null });
    await settle();

    TestBed.inject(DataChanges).notify('transfers');
    fixture.detectChanges();
    flushAccounts(backend, [account]); // balances change with transfers too
    listRequest().flush({ items: [aTransaction({ date: today })], nextCursor: null });
    await settle();

    expect(rows().length).toBe(1);
  });

  it('shows the error of the first page with a retry', async () => {
    (await open()).flush(null, { status: 0, statusText: 'Unknown Error' });
    await settle();

    el().querySelector<HTMLButtonElement>('app-error-state button')!.click();
    listRequest().flush({ items: [], nextCursor: null });
  });
});
