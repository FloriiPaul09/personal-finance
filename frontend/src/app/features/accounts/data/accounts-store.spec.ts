import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiError, DataChanges } from '../../../core';
import { anAccount, problem, provideApiHttpTesting } from '../../../core/testing';
import { AccountsStore } from './accounts-store';

describe('AccountsStore', () => {
  let store: AccountsStore;
  let changes: DataChanges;
  let backend: HttpTestingController;
  const account = anAccount({ version: 42 });
  const body = { name: 'Contanti', type: 'cash' as const, currency: 'EUR', initialBalance: 0 };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideApiHttpTesting()],
    });
    store = TestBed.inject(AccountsStore);
    changes = TestBed.inject(DataChanges);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    // The list in core may have been read meanwhile: not what these tests are about.
    backend.match('/api/accounts?includeArchived=true');
    backend.verify();
  });

  it('creates an account with POST, then has the accounts read again', async () => {
    const created = store.create(body);
    const request = backend.expectOne('/api/accounts');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush(anAccount({ name: 'Contanti' }));

    expect((await created).name).toBe('Contanti');
    expect(changes.version('accounts')).toBe(1);
  });

  it('updates with PUT and the version in If-Match', async () => {
    const updated = store.update(account, body);
    const request = backend.expectOne(`/api/accounts/${account.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('If-Match')).toBe('"42"');
    request.flush(account);
    await updated;
  });

  it('archives and restores without a version', async () => {
    const archived = store.archive(account);
    const archive = backend.expectOne(`/api/accounts/${account.id}/archive`);
    expect(archive.request.method).toBe('POST');
    expect(archive.request.headers.has('If-Match')).toBe(false);
    archive.flush({ ...account, isArchived: true });
    await archived;

    const restored = store.unarchive(account);
    backend.expectOne(`/api/accounts/${account.id}/unarchive`).flush(account);
    await restored;
    expect(changes.version('accounts')).toBe(2);
  });

  it('deletes with the version in If-Match', async () => {
    const deleted = store.delete(account);
    const request = backend.expectOne(`/api/accounts/${account.id}`);
    expect(request.request.method).toBe('DELETE');
    expect(request.request.headers.get('If-Match')).toBe('"42"');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await deleted;
  });

  it('has the accounts read again after a failed write too (a 412 means newer data)', async () => {
    const updated = store.update(account, body);
    backend
      .expectOne(`/api/accounts/${account.id}`)
      .flush(...problem(412, 'concurrency.version_mismatch'));

    await expect(updated).rejects.toBeInstanceOf(ApiError);
    expect(changes.version('accounts')).toBe(1);
  });
});
