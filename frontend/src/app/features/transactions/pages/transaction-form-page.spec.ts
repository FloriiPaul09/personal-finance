import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { provideRouter, Router } from '@angular/router';
import { Notifier, PageExit, RECENT_STORAGE, SessionStore } from '../../../core';
import {
  aCategory,
  anAccount,
  aTransaction,
  flushAccounts,
  flushCategories,
  problem,
  provideApiHttpTesting,
} from '../../../core/testing';
import { todayIso } from '../../../shared/dates';
import { TransactionFormPage } from './transaction-form-page';
import { signal } from '@angular/core';

describe('TransactionFormPage', () => {
  let fixture: ComponentFixture<TransactionFormPage>;
  let backend: HttpTestingController;
  let loader: HarnessLoader;
  let show: ReturnType<typeof vi.fn>;
  let undoable: ReturnType<typeof vi.fn>;
  let leave: ReturnType<typeof vi.fn>;
  let storage: Map<string, string>;

  const checking = anAccount({ name: 'Conto corrente' });
  const cash = anAccount({ name: 'Contanti', currency: 'CHF' });
  const spesa = aCategory({ name: 'Spesa' });
  const svago = aCategory({ name: 'Svago' });
  const stipendio = aCategory({ name: 'Stipendio', kind: 'income' });

  beforeEach(() => {
    show = vi.fn().mockResolvedValue(undefined);
    undoable = vi.fn().mockResolvedValue(undefined);
    leave = vi.fn().mockResolvedValue(true);
    storage = new Map();
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [
        provideApiHttpTesting(),
        provideRouter([]),
        { provide: Notifier, useValue: { show, undoable } },
        { provide: PageExit, useValue: { leave } },
        { provide: SessionStore, useValue: { user: signal({ id: 'u1', email: 'a@b.c' }) } },
        {
          provide: RECENT_STORAGE,
          useValue: {
            getItem: (key: string) => storage.get(key) ?? null,
            setItem: (key: string, value: string) => storage.set(key, value),
          },
        },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TransactionFormPage);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  });

  afterEach(() => {
    backend.match('/api/tags');
    backend.match('/api/accounts?includeArchived=true');
    backend.match('/api/categories?includeArchived=true');
    backend.verify();
  });

  const el = () => fixture.nativeElement as HTMLElement;

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  async function openNew(kind?: string): Promise<void> {
    if (kind) {
      fixture.componentRef.setInput('kind', kind);
    }
    fixture.detectChanges();
    flushAccounts(backend, [checking, cash]);
    flushCategories(backend, [spesa, svago, stipendio]);
    flushTags();
    await settle();
    await settle();
  }

  /** The tag suggestions, read by the store when the page opens. */
  function flushTags(): void {
    TestBed.tick();
    backend.match('/api/tags').forEach((r) => r.flush({ items: [] }));
  }

  async function openExisting(transaction = aTransaction()): Promise<void> {
    fixture.componentRef.setInput('id', transaction.id);
    fixture.detectChanges();
    flushAccounts(backend, [checking, cash]);
    flushCategories(backend, [spesa, svago, stipendio]);
    backend.expectOne(`/api/transactions/${transaction.id}`).flush(transaction);
    flushTags();
    await settle();
    await settle();
  }

  function field(selector: string): HTMLInputElement {
    return el().querySelector<HTMLInputElement>(`${selector} input`)!;
  }

  async function type(input: HTMLInputElement, text: string): Promise<void> {
    input.value = text;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    await settle();
  }

  function chip(name: string): HTMLElement {
    return Array.from(el().querySelectorAll<HTMLElement>('mat-chip-option')).find(
      (c) => c.textContent?.trim() === name,
    )!;
  }

  async function submit(): Promise<void> {
    el()
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { cancelable: true }));
    await settle();
  }

  async function nextRequest(url: string): Promise<TestRequest> {
    for (let attempt = 0; attempt < 50; attempt++) {
      const [request] = backend.match(url);
      if (request) {
        return request;
      }
      await new Promise((resolve) => setTimeout(resolve, 10));
    }
    throw new Error(`No request to ${url}`);
  }

  const errors = () =>
    Array.from(el().querySelectorAll('mat-error')).map((e) => e.textContent!.trim());

  it('opens an expense with today, the first account, the kind tabs and the amount focused', async () => {
    await openNew();

    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Nuova spesa');
    expect(el().querySelector('app-movement-kind-switch')).not.toBeNull();
    expect(field('app-date-input').value).toBe(todayIso().split('-').reverse().join('/'));
    expect(el().querySelector('app-account-select mat-select')!.textContent).toContain(
      'Conto corrente',
    );
    expect(document.activeElement).toBe(field('app-amount-input'));
    expect(el().querySelector('app-tags-input')).toBeNull();
  });

  it('creates the expense with the id chosen when the form opened, then goes back with "Annulla"', async () => {
    await openNew();
    const id = (fixture.componentInstance as unknown as { nextId: () => string }).nextId();
    await type(field('app-amount-input'), '12,5');
    fixture.componentInstance['chooseCategory'](spesa.id);
    await settle();

    await submit();
    const request = backend.expectOne('/api/transactions');
    expect(request.request.body).toEqual({
      id,
      kind: 'expense',
      amount: 12.5,
      categoryId: spesa.id,
      accountId: checking.id,
      date: todayIso(),
      description: null,
      tags: [],
    });
    const saved = aTransaction({ id, categoryId: spesa.id, accountId: checking.id });
    request.flush(saved);
    await settle();

    expect(undoable).toHaveBeenCalledWith('Spesa salvata.', expect.any(Function));
    expect(leave).toHaveBeenCalledWith('/transactions');
    expect(JSON.parse(storage.get('pf:u1:recent')!)).toEqual({
      expense: [spesa.id],
      income: [],
      account: checking.id,
    });

    undoable.mock.calls[0][1]();
    const undo = backend.expectOne(`/api/transactions/${id}`);
    expect(undo.request.method).toBe('DELETE');
    undo.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('offers the categories used last as chips and the account used last', async () => {
    storage.set(
      'pf:u1:recent',
      JSON.stringify({ expense: [svago.id, 'deleted-id', spesa.id], income: [], account: cash.id }),
    );
    await openNew();

    const chips = Array.from(el().querySelectorAll('mat-chip-option')).map((c) =>
      c.textContent!.trim(),
    );
    expect(chips).toEqual(['Svago', 'Spesa']);
    expect(el().querySelector('app-account-select mat-select')!.textContent).toContain('Contanti');
    expect(el().querySelector('app-amount-input .currency')!.textContent!.trim()).toBe('CHF');

    chip('Svago').querySelector('button')!.click();
    await settle();
    expect(fixture.componentInstance['model']().categoryId).toBe(svago.id);
  });

  it('checks the form before calling the API and moves to the first error', async () => {
    await openNew();

    await submit();

    backend.expectNone('/api/transactions');
    expect(errors()).toEqual(["Inserisci l'importo.", 'Scegli la categoria.']);
    expect(document.activeElement).toBe(field('app-amount-input'));
  });

  it('keeps account and date for "Salva e aggiungi un\'altra", with a new id', async () => {
    await openNew();
    const first = fixture.componentInstance['nextId']();
    await type(field('app-amount-input'), '3');
    fixture.componentInstance['chooseCategory'](spesa.id);
    await settle();

    el().querySelector<HTMLButtonElement>('[data-testid="save-another"]')!.click();
    await settle();
    backend.expectOne('/api/transactions').flush(aTransaction({ id: first, date: '2026-11-10' }));
    await settle();
    await settle();

    expect(leave).not.toHaveBeenCalled();
    expect(fixture.componentInstance['nextId']()).not.toBe(first);
    const model = fixture.componentInstance['model']();
    expect(model).toEqual(
      expect.objectContaining({ amount: null, categoryId: null, accountId: checking.id }),
    );
    expect(model.date).toBe('2026-11-10');
    expect(errors()).toEqual([]);
  });

  it('opens an income with income categories and the income tab', async () => {
    await openNew('income');

    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Nuova entrata');
    expect(el().querySelector('.mdc-tab--active')!.textContent!.trim()).toBe('Entrata');
  });

  it('shows description and tags on request', async () => {
    await openNew();

    el().querySelector<HTMLButtonElement>('[data-testid="show-details"]')!.click();
    await settle();

    expect(el().querySelector('app-tags-input')).not.toBeNull();
    backend.match('/api/tags');
  });

  it('edits a movement with its version, details open when it has them', async () => {
    const existing = aTransaction({
      accountId: checking.id,
      categoryId: spesa.id,
      tags: ['lavoro'],
      version: 5,
    });
    await openExisting(existing);

    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Modifica spesa');
    expect(el().querySelector('app-movement-kind-switch')).toBeNull();
    expect(el().querySelector('app-tags-input')!.textContent).toContain('lavoro');
    await type(field('app-amount-input'), '20');

    await submit();
    const request = backend.expectOne(`/api/transactions/${existing.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('If-Match')).toBe('"5"');
    expect(request.request.body).toEqual({
      amount: 20,
      categoryId: spesa.id,
      accountId: checking.id,
      date: existing.date,
      description: 'Pranzo',
      tags: ['lavoro'],
    });
    request.flush({ ...existing, amount: 20, version: 6 });
    await settle();

    expect(show).toHaveBeenCalledWith('Movimento salvato.');
    expect(leave).toHaveBeenCalledWith('/transactions');
  });

  it('shows the current data when the movement changed elsewhere (412)', async () => {
    const existing = aTransaction({ accountId: checking.id, categoryId: spesa.id });
    await openExisting(existing);
    await type(field('app-amount-input'), '99');

    await submit();
    backend
      .expectOne(`/api/transactions/${existing.id}`)
      .flush(...problem(412, 'concurrency.version_mismatch'));
    await settle();
    backend
      .expectOne(`/api/transactions/${existing.id}`)
      .flush({ ...existing, amount: 7, version: existing.version + 1 });
    await settle();
    await settle();

    expect(el().querySelector('.form-error')!.textContent).toContain('modificato altrove');
    expect(field('app-amount-input').value).toBe('7');
  });

  it('puts an archived category refused by the API under the category', async () => {
    const existing = aTransaction({ accountId: checking.id, categoryId: spesa.id });
    await openExisting(existing);

    await submit();
    backend
      .expectOne(`/api/transactions/${existing.id}`)
      .flush(...problem(409, 'category.archived'));
    await settle();

    expect(errors()).toEqual(['La categoria è archiviata.']);
  });

  it('deletes a movement after confirming', async () => {
    const existing = aTransaction({ accountId: checking.id, categoryId: spesa.id });
    await openExisting(existing);

    el().querySelector<HTMLButtonElement>('[data-testid="delete-movement"]')!.click();
    const dialog = await loader.getHarness(MatDialogHarness);
    expect(await dialog.getTitleText()).toBe('Eliminare questo movimento?');
    document.querySelector<HTMLButtonElement>('mat-dialog-actions .confirm')!.click();

    const request = await nextRequest(`/api/transactions/${existing.id}`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle();
    expect(show).toHaveBeenCalledWith('Movimento eliminato.');
    expect(leave).toHaveBeenCalledWith('/transactions');
  });

  it('opens the transfer of a transfer leg', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await openExisting(
      aTransaction({
        kind: 'transfer',
        categoryId: null,
        transferId: 'tr-9',
        accountId: checking.id,
      }),
    );

    expect(navigate).toHaveBeenCalledWith(['/transfers', 'tr-9'], { replaceUrl: true });
  });

  it('says when the movement does not exist', async () => {
    fixture.componentRef.setInput('id', 'gone');
    fixture.detectChanges();
    backend.expectOne('/api/transactions/gone').flush(...problem(404, 'resource.not_found'));
    await settle();

    expect(el().querySelector('app-empty-state')!.textContent).toContain('Movimento non trovato');
  });
});
