import { HttpTestingController } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ApiError, DataChanges } from '../../../core';
import { aTransaction, problem, provideApiHttpTesting } from '../../../core/testing';
import { TransactionsStore } from './transactions-store';

describe('TransactionsStore', () => {
  let store: TransactionsStore;
  let changes: DataChanges;
  let backend: HttpTestingController;
  const transaction = aTransaction({ version: 42 });

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideApiHttpTesting()] });
    store = TestBed.inject(TransactionsStore);
    changes = TestBed.inject(DataChanges);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.match('/api/tags');
    backend.verify();
  });

  it('lists a page with the filters set, the page size and the cursor', async () => {
    const page = firstValueFrom(
      store.list(
        {
          from: '2026-11-01',
          to: '2026-11-30',
          accountId: 'a',
          categoryId: null,
          kind: 'expense',
          q: '',
        },
        'next',
      ),
    );
    const request = backend.expectOne((r) => r.url === '/api/transactions');
    expect(request.request.params.keys().sort()).toEqual([
      'accountId',
      'cursor',
      'from',
      'kind',
      'limit',
      'to',
    ]);
    expect(request.request.params.get('limit')).toBe('50');
    expect(request.request.params.get('cursor')).toBe('next');
    request.flush({ items: [transaction], nextCursor: null });

    expect((await page).items).toEqual([transaction]);
  });

  it('creates with POST and the id of the form, and tells that transactions changed', async () => {
    const body = {
      id: '0193a5c4-0000-7000-8000-00000000abcd',
      kind: 'expense' as const,
      accountId: 'a',
      categoryId: 'c',
      date: '2026-11-17',
      amount: 12.5,
      description: null,
      tags: [],
    };
    const created = store.create(body);
    const request = backend.expectOne('/api/transactions');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush(transaction);
    await created;

    expect(changes.version('transactions')).toBe(1);
    expect(changes.version('transfers')).toBe(0);
  });

  it('reads, updates and deletes one transaction with its version', async () => {
    const loaded = store.load(transaction.id);
    backend.expectOne(`/api/transactions/${transaction.id}`).flush(transaction);
    expect(await loaded).toEqual(transaction);

    const updated = store.update(transaction, {
      accountId: 'a',
      categoryId: 'c',
      date: '2026-11-17',
      amount: 1,
      description: null,
      tags: ['x'],
    });
    const put = backend.expectOne(`/api/transactions/${transaction.id}`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.headers.get('If-Match')).toBe('"42"');
    put.flush(transaction);
    await updated;

    const deleted = store.delete(transaction);
    const del = backend.expectOne(`/api/transactions/${transaction.id}`);
    expect(del.request.method).toBe('DELETE');
    expect(del.request.headers.get('If-Match')).toBe('"42"');
    del.flush(null, { status: 204, statusText: 'No Content' });
    await deleted;
  });

  it('tells that transactions changed after a failed write too', async () => {
    const deleted = store.delete(transaction);
    backend
      .expectOne(`/api/transactions/${transaction.id}`)
      .flush(...problem(412, 'concurrency.version_mismatch'));

    await expect(deleted).rejects.toBeInstanceOf(ApiError);
    expect(changes.version('transactions')).toBe(1);
  });

  it('offers the tag names, the most used first, and reads them again after a write', async () => {
    TestBed.tick();
    backend.expectOne('/api/tags').flush({
      items: [
        { id: '1', name: 'casa', transactionCount: 2, version: 1 },
        { id: '2', name: 'vacanza', transactionCount: 9, version: 1 },
        { id: '3', name: 'auto', transactionCount: 2, version: 1 },
      ],
    });
    await TestBed.inject(ApplicationRef).whenStable();

    expect(store.tagNames()).toEqual(['vacanza', 'auto', 'casa']);

    changes.notify('transactions');
    TestBed.tick();
    backend.expectOne('/api/tags').flush({ items: [] });
  });

  it('has a list version that follows transactions and transfers', () => {
    const before = store.listVersion();
    changes.notify('transfers');
    expect(store.listVersion()).toBe(before + 1);
  });
});
