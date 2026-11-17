import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApiError } from '../api/api-error';
import { apiErrorInterceptor } from '../http/api-error-interceptor';
import { anAccount, flushAccounts } from '../testing';
import { AccountsDirectory } from './accounts-directory';
import { DataChanges } from './data-changes';

describe('AccountsDirectory', async () => {
  let directory: AccountsDirectory;
  let changes: DataChanges;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    directory = TestBed.inject(AccountsDirectory);
    changes = TestBed.inject(DataChanges);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  /** Lets the resource take the answer just flushed. */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  const checking = anAccount({ name: 'Conto corrente' });
  const cash = anAccount({ name: 'Contanti', type: 'cash' });
  const old = anAccount({ name: 'Vecchio conto', isArchived: true });

  it('reads every account once, archived ones included', async () => {
    TestBed.tick();
    expect(directory.loading()).toBe(true);
    const request = backend.expectOne('/api/accounts?includeArchived=true');
    request.flush({ items: [checking, cash, old] });
    await settle();

    expect(directory.loading()).toBe(false);
    expect(directory.all()).toEqual([checking, cash, old]);
    expect(directory.active()).toEqual([checking, cash]);
    expect(directory.byId(old.id)).toEqual(old);
    expect(directory.byId('unknown')).toBeUndefined();
    expect(directory.byId(null)).toBeUndefined();
  });

  it('reads again after a write to accounts, transactions or transfers, keeping the list meanwhile', async () => {
    TestBed.tick();
    flushAccounts(backend, [checking]);
    await settle();
    let shown = [checking];
    expect(directory.all()).toEqual(shown);

    for (const resource of ['accounts', 'transactions', 'transfers'] as const) {
      changes.notify(resource);
      TestBed.tick();
      expect(directory.all()).toEqual(shown);
      expect(directory.loading()).toBe(false);
      shown = [...shown, anAccount({ name: resource })];
      flushAccounts(backend, shown);
      await settle();
      expect(directory.all()).toEqual(shown);
    }

    changes.notify('categories');
    TestBed.tick();
    backend.expectNone('/api/accounts?includeArchived=true');
  });

  it('cancels a read still running when another write happens', async () => {
    TestBed.tick();
    flushAccounts(backend, [checking]);
    await settle();
    expect(directory.all()).toEqual([checking]);
    changes.notify('accounts');
    TestBed.tick();
    const first = backend.expectOne('/api/accounts?includeArchived=true');

    changes.notify('accounts');
    TestBed.tick();

    expect(first.cancelled).toBe(true);
    flushAccounts(backend, [checking, cash]);
    await settle();
    expect(directory.all()).toEqual([checking, cash]);
  });

  it('exposes the ApiError of a failed read, and reads again on reload', async () => {
    TestBed.tick();
    backend
      .expectOne('/api/accounts?includeArchived=true')
      .flush({ code: 'server.error', status: 500 }, { status: 500, statusText: 'Error' });
    await settle();

    expect(directory.error()).toBeInstanceOf(ApiError);
    expect(directory.error()?.code).toBe('server.error');
    expect(directory.loading()).toBe(false);

    directory.reload();
    TestBed.tick();
    flushAccounts(backend, [checking]);
    await settle();
    expect(directory.error()).toBeUndefined();
    expect(directory.all()).toEqual([checking]);
  });
});
