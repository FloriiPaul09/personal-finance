import { HttpTestingController } from '@angular/common/http/testing';
import { ApplicationRef, Injector, runInInjectionContext, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApiError, DataChanges } from '../../../core';
import { aBudget, aBudgetProgress, problem, provideApiHttpTesting } from '../../../core/testing';
import { IsoMonth } from '../../../shared/dates';
import { BudgetsStore } from './budgets-store';

describe('BudgetsStore', () => {
  let store: BudgetsStore;
  let changes: DataChanges;
  let backend: HttpTestingController;

  const casa = 'casa-id';
  const june = aBudget({ categoryId: casa, from: '2026-06', amount: 350, version: 7 });
  const january = aBudget({ categoryId: casa, from: '2026-01', amount: 300 });
  const svago = aBudget({ categoryId: 'svago-id', from: '2026-03', amount: null });

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideApiHttpTesting()] });
    store = TestBed.inject(BudgetsStore);
    changes = TestBed.inject(DataChanges);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  async function flushEntries(items = [june, january, svago]): Promise<void> {
    TestBed.tick();
    backend.expectOne('/api/budgets').flush({ items });
    await settle();
  }

  it('reads every entry once and tells which categories have a budget', async () => {
    await flushEntries();

    expect(store.entries()).toEqual([june, january, svago]);
    expect(store.budgetedIds()).toEqual(new Set([casa, 'svago-id']));
    expect(store.entriesOf(casa)).toEqual([january, june]);
    expect(store.entriesOf('other')).toEqual([]);
  });

  it('reads the entries again after a budget write only', async () => {
    await flushEntries();
    expect(store.entries()).toHaveLength(3); // read, as a page does, so the list is kept

    changes.notify('transactions');
    TestBed.tick();
    backend.expectNone('/api/budgets');

    changes.notify('budgets');
    TestBed.tick();
    expect(store.entries()).toEqual([june, january, svago]);
    backend.expectOne('/api/budgets').flush({ items: [june] });
    await settle();
    expect(store.entries()).toEqual([june]);
  });

  describe('progress', () => {
    const month = signal<IsoMonth>('2026-11');
    const item = aBudgetProgress({ categoryId: casa });

    function progress() {
      return runInInjectionContext(TestBed.inject(Injector), () => store.progressOf(month));
    }

    beforeEach(async () => {
      month.set('2026-11');
      // The store reads its entries when it starts: answered, so `whenStable()` does not wait.
      await flushEntries([]);
    });

    it('reads the month it is given and again after writes that change what is spent', async () => {
      const list = progress();
      TestBed.tick();
      expect(list.loading()).toBe(true);
      backend.expectOne('/api/budgets/progress?month=2026-11').flush({ items: [item] });
      await settle();
      expect(list.items()).toEqual([item]);
      expect(list.loading()).toBe(false);

      for (const resource of ['budgets', 'transactions', 'categories'] as const) {
        changes.notify(resource);
        TestBed.tick();
        // The rows stay while the same month is read again.
        expect(list.items()).toEqual([item]);
        backend.expectOne('/api/budgets/progress?month=2026-11').flush({ items: [item] });
        // A budget write also has the entries read again.
        backend.match('/api/budgets').forEach((entries) => entries.flush({ items: [] }));
        await settle();
      }

      changes.notify('accounts');
      TestBed.tick();
      backend.expectNone('/api/budgets/progress?month=2026-11');
    });

    it('shows nothing of the previous month while another one loads', async () => {
      const list = progress();
      TestBed.tick();
      backend.expectOne('/api/budgets/progress?month=2026-11').flush({ items: [item] });
      await settle();

      month.set('2026-12');
      TestBed.tick();
      expect(list.items()).toBeUndefined();
      expect(list.loading()).toBe(true);
      backend.expectOne('/api/budgets/progress?month=2026-12').flush({ items: [] });
      await settle();
      expect(list.items()).toEqual([]);
    });

    it('exposes the error and reads again on retry', async () => {
      const list = progress();
      TestBed.tick();
      backend
        .expectOne('/api/budgets/progress?month=2026-11')
        .flush(...problem(500, 'server.error'));
      await settle();

      expect(list.error()).toBeInstanceOf(ApiError);
      expect(list.loading()).toBe(false);
      list.reload();
      TestBed.tick();
      backend.expectOne('/api/budgets/progress?month=2026-11').flush({ items: [] });
      await settle();
      expect(list.error()).toBeUndefined();
    });
  });

  describe('setFrom', () => {
    async function answerList(items = [january, june]): Promise<void> {
      await Promise.resolve();
      backend.expectOne(`/api/budgets?categoryId=${casa}`).flush({ items });
      await Promise.resolve();
    }

    it('changes the entry of that month when there is one, and "Annulla" puts back its amount', async () => {
      const changed = store.setFrom(casa, '2026-06', 400);
      await answerList();
      const put = backend.expectOne(`/api/budgets/${june.id}`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.headers.get('If-Match')).toBe('"7"');
      expect(put.request.body).toEqual({ amount: 400 });
      const updated = { ...june, amount: 400, version: 8 };
      put.flush(updated);
      const change = await changed;
      expect(change.entry).toEqual(updated);
      expect(changes.version('budgets')).toBe(1);

      const undone = change.undo();
      const back = backend.expectOne(`/api/budgets/${june.id}`);
      expect(back.request.headers.get('If-Match')).toBe('"8"');
      expect(back.request.body).toEqual({ amount: 350 });
      back.flush(june);
      await undone;
      expect(changes.version('budgets')).toBe(2);
    });

    it('adds an entry for a month without one, and "Annulla" deletes it', async () => {
      const stopped = store.setFrom(casa, '2026-11', null);
      await answerList();
      const post = backend.expectOne('/api/budgets');
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual({ categoryId: casa, from: '2026-11', amount: null });
      const created = aBudget({ categoryId: casa, from: '2026-11', amount: null, version: 3 });
      post.flush(created);
      const change = await stopped;

      const undone = change.undo();
      const del = backend.expectOne(`/api/budgets/${created.id}`);
      expect(del.request.method).toBe('DELETE');
      expect(del.request.headers.get('If-Match')).toBe('"3"');
      del.flush(null, { status: 204, statusText: 'No Content' });
      await undone;
      expect(changes.version('budgets')).toBe(2);
    });

    it('tells that budgets changed after a refused write too', async () => {
      const changed = store.setFrom(casa, '2026-11', 100);
      await answerList([]);
      backend.expectOne('/api/budgets').flush(...problem(409, 'budget.branch_conflict'));

      await expect(changed).rejects.toMatchObject({ code: 'budget.branch_conflict' });
      expect(changes.version('budgets')).toBe(1);
    });
  });

  it('deletes an entry with its version', async () => {
    const deleted = store.delete(june);
    const del = backend.expectOne(`/api/budgets/${june.id}`);
    expect(del.request.method).toBe('DELETE');
    expect(del.request.headers.get('If-Match')).toBe('"7"');
    del.flush(null, { status: 204, statusText: 'No Content' });
    await deleted;
    expect(changes.version('budgets')).toBe(1);
  });
});
