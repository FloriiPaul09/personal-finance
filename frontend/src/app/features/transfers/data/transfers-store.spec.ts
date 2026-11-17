import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DataChanges } from '../../../core';
import { aTransfer, provideApiHttpTesting } from '../../../core/testing';
import { TransfersStore } from './transfers-store';

describe('TransfersStore', () => {
  let store: TransfersStore;
  let changes: DataChanges;
  let backend: HttpTestingController;
  const transfer = aTransfer({ version: 7 });
  const body = {
    fromAccountId: 'a',
    toAccountId: 'b',
    date: '2026-11-17',
    amount: 300,
    description: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideApiHttpTesting()] });
    store = TestBed.inject(TransfersStore);
    changes = TestBed.inject(DataChanges);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('reads, creates, updates and deletes, telling that transfers changed', async () => {
    const loaded = store.load(transfer.id);
    backend.expectOne(`/api/transfers/${transfer.id}`).flush(transfer);
    expect(await loaded).toEqual(transfer);

    const created = store.create({ id: 'new-id', ...body });
    const post = backend.expectOne('/api/transfers');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ id: 'new-id', ...body });
    post.flush(transfer);
    await created;

    const updated = store.update(transfer, body);
    const put = backend.expectOne(`/api/transfers/${transfer.id}`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.headers.get('If-Match')).toBe('"7"');
    put.flush(transfer);
    await updated;

    const deleted = store.delete(transfer);
    const del = backend.expectOne(`/api/transfers/${transfer.id}`);
    expect(del.request.method).toBe('DELETE');
    expect(del.request.headers.get('If-Match')).toBe('"7"');
    del.flush(null, { status: 204, statusText: 'No Content' });
    await deleted;

    expect(changes.version('transfers')).toBe(3);
    expect(changes.version('transactions')).toBe(0);
  });
});
