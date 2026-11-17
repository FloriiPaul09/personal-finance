import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatAutocompleteHarness } from '@angular/material/autocomplete/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { provideRouter, Router } from '@angular/router';
import { Notifier } from '../../../core';
import { anAccount, flushAccounts, problem, provideApiHttpTesting } from '../../../core/testing';
import { AccountFormPage } from './account-form-page';

describe('AccountFormPage', () => {
  let fixture: ComponentFixture<AccountFormPage>;
  let backend: HttpTestingController;
  let loader: HarnessLoader;
  let navigate: ReturnType<typeof vi.spyOn>;
  let open: ReturnType<typeof vi.fn>;

  const account = anAccount({
    name: 'Conto corrente',
    initialBalance: 100,
    hasMovements: false,
    version: 7,
  });

  beforeEach(() => {
    open = vi.fn();
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [
        provideApiHttpTesting(),
        provideRouter([]),
        { provide: Notifier, useValue: { show: open } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    fixture = TestBed.createComponent(AccountFormPage);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  });

  afterEach(() => backend.verify());

  async function openNew(): Promise<void> {
    fixture.detectChanges();
    flushAccounts(backend, []);
    await fixture.whenStable();
  }

  async function openExisting(accounts = [account], id = account.id): Promise<void> {
    fixture.componentRef.setInput('id', id);
    fixture.detectChanges();
    flushAccounts(backend, accounts);
    await fixture.whenStable();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const input = (label: string) =>
    Array.from(el().querySelectorAll('mat-form-field'))
      .find((field) => field.querySelector('mat-label')?.textContent?.trim() === label)!
      .querySelector('input')!;

  async function type(label: string, text: string): Promise<void> {
    const field = input(label);
    field.value = text;
    field.dispatchEvent(new Event('input'));
    field.dispatchEvent(new Event('blur'));
    await fixture.whenStable();
  }

  async function save(): Promise<void> {
    el()
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { cancelable: true }));
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  /** Lets the answer of a write reach the page. */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  const errors = () =>
    Array.from(el().querySelectorAll('mat-error')).map((e) => e.textContent!.trim());

  it('starts a new account as a checking account in euro with balance 0', async () => {
    await openNew();

    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Nuovo conto');
    expect(input('Nome').value).toBe('');
    expect(await (await loader.getHarness(MatSelectHarness)).getValueText()).toBe('Conto corrente');
    expect(input('Valuta').value).toBe('EUR');
    expect(input('Saldo iniziale').value).toBe('0');
  });

  it('creates the account with the name trimmed and the currency in upper case', async () => {
    await openNew();
    await type('Nome', '  Contanti ');
    const type_ = await loader.getHarness(MatSelectHarness);
    await type_.open();
    await type_.clickOptions({ text: 'Contanti' });
    await type('Valuta', 'chf');
    await type('Saldo iniziale', '-12,5');

    await save();
    const request = backend.expectOne('/api/accounts');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Contanti',
      type: 'cash',
      currency: 'CHF',
      initialBalance: -12.5,
    });
    request.flush(anAccount({ name: 'Contanti' }));
    await settle();
    flushAccounts(backend, [anAccount({ name: 'Contanti' })]);
    await settle();

    expect(open).toHaveBeenCalledWith('Conto creato.');
    expect(navigate).toHaveBeenCalledWith('/accounts');
  });

  it('checks the fields before calling the API and moves to the first error', async () => {
    await openNew();
    await type('Valuta', 'euro');
    await type('Saldo iniziale', '');

    await save();

    backend.expectNone('/api/accounts');
    expect(errors()).toEqual([
      'Inserisci il nome del conto.',
      'Usa un codice di tre lettere, per esempio EUR.',
      'Inserisci il saldo iniziale, anche 0.',
    ]);
    expect(document.activeElement).toBe(input('Nome'));
  });

  it('refuses a name made only of spaces', async () => {
    await openNew();
    await type('Nome', '   ');

    expect(errors()).toEqual(['Inserisci il nome del conto.']);
  });

  it('suggests currencies by code or name', async () => {
    await openNew();
    const currency = await loader.getHarness(MatAutocompleteHarness);
    await currency.clear();
    await currency.enterText('fran');

    const options = await currency.getOptions();
    const texts = await Promise.all(options.map((o) => o.getText()));
    expect(texts).toContain('CHF franco svizzero');

    await currency.selectOption({ text: 'CHF franco svizzero' });
    expect(input('Valuta').value).toBe('CHF');
    expect(el().querySelector('app-amount-input .currency')!.textContent!.trim()).toBe('CHF');
  });

  it('edits an account and sends its version', async () => {
    await openExisting();
    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Modifica conto');
    expect(input('Nome').value).toBe('Conto corrente');
    expect(input('Saldo iniziale').value).toBe('100');

    await type('Nome', 'Conto principale');
    await save();
    const request = backend.expectOne(`/api/accounts/${account.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('If-Match')).toBe('"7"');
    expect(request.request.body.name).toBe('Conto principale');
    request.flush({ ...account, name: 'Conto principale', version: 8 });
    await settle();
    flushAccounts(backend, [{ ...account, name: 'Conto principale', version: 8 }]);
    await settle();

    expect(open).toHaveBeenCalledWith('Conto salvato.');
  });

  it('shows a name already used under the name field', async () => {
    await openExisting();
    await type('Nome', 'Contanti');

    await save();
    backend.expectOne(`/api/accounts/${account.id}`).flush(...problem(409, 'account.name_taken'));
    await settle();
    flushAccounts(backend, [account]);
    await settle();

    expect(errors()).toEqual(['Esiste già un conto con questo nome.']);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('locks the currency of an account with movements, and says why', async () => {
    await openExisting([{ ...account, hasMovements: true }]);

    expect(input('Valuta').disabled).toBe(true);
    expect(el().textContent).toContain('Non si può cambiare: il conto ha già dei movimenti.');
  });

  it('shows the current data when the account changed elsewhere (412)', async () => {
    await openExisting();
    await type('Nome', 'Mio nome');

    await save();
    backend
      .expectOne(`/api/accounts/${account.id}`)
      .flush(...problem(412, 'concurrency.version_mismatch'));
    await settle();
    flushAccounts(backend, [{ ...account, name: 'Nome di altrove', version: 9 }]);
    await settle();

    expect(el().querySelector('.form-error')!.textContent).toContain(
      'Il conto è stato modificato altrove',
    );
    expect(input('Nome').value).toBe('Nome di altrove');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('says when the account does not exist', async () => {
    await openExisting([account], 'unknown-id');

    expect(el().querySelector('app-empty-state')!.textContent).toContain('Conto non trovato');
    expect(el().querySelector('form')).toBeNull();
  });
});
