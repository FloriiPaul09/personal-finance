import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { provideRouter } from '@angular/router';
import { Notifier, PageExit, RECENT_STORAGE, SessionStore } from '../../../core';
import {
  anAccount,
  aTransfer,
  flushAccounts,
  problem,
  provideApiHttpTesting,
} from '../../../core/testing';
import { todayIso } from '../../../shared/dates';
import { TransferFormPage } from './transfer-form-page';

describe('TransferFormPage', () => {
  let fixture: ComponentFixture<TransferFormPage>;
  let backend: HttpTestingController;
  let loader: HarnessLoader;
  let show: ReturnType<typeof vi.fn>;
  let undoable: ReturnType<typeof vi.fn>;
  let leave: ReturnType<typeof vi.fn>;

  const checking = anAccount({ name: 'Conto corrente' });
  const savings = anAccount({ name: 'Risparmi', type: 'savings' });
  const swiss = anAccount({ name: 'Conto svizzero', currency: 'CHF' });

  beforeEach(() => {
    show = vi.fn().mockResolvedValue(undefined);
    undoable = vi.fn().mockResolvedValue(undefined);
    leave = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [
        provideApiHttpTesting(),
        provideRouter([]),
        { provide: Notifier, useValue: { show, undoable } },
        { provide: PageExit, useValue: { leave } },
        { provide: SessionStore, useValue: { user: signal({ id: 'u1', email: 'a@b.c' }) } },
        { provide: RECENT_STORAGE, useValue: null },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TransferFormPage);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  });

  afterEach(() => {
    backend.match('/api/accounts?includeArchived=true');
    backend.verify();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const component = () =>
    fixture.componentInstance as unknown as {
      model: { (): Record<string, unknown>; update: (fn: (m: object) => object) => void };
      nextId: () => string;
    };

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  async function openNew(): Promise<void> {
    fixture.detectChanges();
    flushAccounts(backend, [checking, savings, swiss]);
    await settle();
    await settle();
  }

  async function openExisting(transfer = aTransfer()): Promise<void> {
    fixture.componentRef.setInput('id', transfer.id);
    fixture.detectChanges();
    flushAccounts(backend, [checking, savings, swiss]);
    backend.expectOne(`/api/transfers/${transfer.id}`).flush(transfer);
    await settle();
    await settle();
  }

  async function fill(values: Record<string, unknown>): Promise<void> {
    component().model.update((m) => ({ ...m, ...values }));
    await settle();
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

  it('opens with the transfer tab, the first account as source and today', async () => {
    await openNew();

    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Nuovo trasferimento');
    expect(el().querySelector('.mdc-tab--active')!.textContent!.trim()).toBe('Trasferimento');
    expect(component().model()).toEqual(
      expect.objectContaining({ fromAccountId: checking.id, toAccountId: null, date: todayIso() }),
    );
  });

  it('creates the transfer with the id chosen when the form opened', async () => {
    await openNew();
    const id = component().nextId();
    await fill({ toAccountId: savings.id, amount: 300, description: '  Risparmio ' });

    await submit();
    const request = backend.expectOne('/api/transfers');
    expect(request.request.body).toEqual({
      id,
      fromAccountId: checking.id,
      toAccountId: savings.id,
      amount: 300,
      date: todayIso(),
      description: 'Risparmio',
    });
    request.flush(aTransfer({ id }));
    await settle();

    expect(undoable).toHaveBeenCalledWith('Trasferimento salvato.', expect.any(Function));
    expect(leave).toHaveBeenCalledWith('/transactions');
  });

  it('refuses two accounts with different currencies before calling the API', async () => {
    await openNew();
    await fill({ toAccountId: swiss.id, amount: 10 });

    await submit();

    backend.expectNone('/api/transfers');
    expect(errors()).toEqual(['I due conti devono avere la stessa valuta.']);
  });

  it('asks for the destination and the amount', async () => {
    await openNew();

    await submit();

    expect(errors()).toEqual(["Inserisci l'importo.", 'Scegli il conto di destinazione.']);
  });

  it('edits a transfer with its version and shows refused accounts on the field', async () => {
    const existing = aTransfer({ fromAccountId: checking.id, toAccountId: savings.id, version: 3 });
    await openExisting(existing);
    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Modifica trasferimento');
    expect(el().querySelector('app-movement-kind-switch')).toBeNull();

    await submit();
    const refused = backend.expectOne(`/api/transfers/${existing.id}`);
    expect(refused.request.method).toBe('PUT');
    expect(refused.request.headers.get('If-Match')).toBe('"3"');
    refused.flush(...problem(409, 'account.archived'));
    await settle();
    expect(errors()).toEqual(['Il conto è archiviato e non accetta nuovi movimenti.']);
  });

  it('shows the current data after a 412', async () => {
    const existing = aTransfer({ fromAccountId: checking.id, toAccountId: savings.id });
    await openExisting(existing);
    await fill({ amount: 5 });

    await submit();
    backend
      .expectOne(`/api/transfers/${existing.id}`)
      .flush(...problem(412, 'concurrency.version_mismatch'));
    await settle();
    backend
      .expectOne(`/api/transfers/${existing.id}`)
      .flush({ ...existing, amount: 250, version: existing.version + 1 });
    await settle();
    await settle();

    expect(el().querySelector('.form-error')!.textContent).toContain('modificato altrove');
    expect(component().model()['amount']).toBe(250);
  });

  it('deletes a transfer after confirming', async () => {
    const existing = aTransfer({ fromAccountId: checking.id, toAccountId: savings.id });
    await openExisting(existing);

    el().querySelector<HTMLButtonElement>('[data-testid="delete-transfer"]')!.click();
    expect(await (await loader.getHarness(MatDialogHarness)).getTitleText()).toBe(
      'Eliminare questo trasferimento?',
    );
    document.querySelector<HTMLButtonElement>('mat-dialog-actions .confirm')!.click();

    const request = await nextRequest(`/api/transfers/${existing.id}`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle();
    expect(show).toHaveBeenCalledWith('Trasferimento eliminato.');
    expect(leave).toHaveBeenCalledWith('/transactions');
  });

  it('says when the transfer does not exist', async () => {
    fixture.componentRef.setInput('id', 'gone');
    fixture.detectChanges();
    backend.expectOne('/api/transfers/gone').flush(...problem(404, 'resource.not_found'));
    await settle();

    expect(el().querySelector('app-empty-state')!.textContent).toContain(
      'Trasferimento non trovato',
    );
  });
});
