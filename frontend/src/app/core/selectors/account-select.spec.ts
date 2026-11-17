import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { form, FormField, required } from '@angular/forms/signals';
import { MatSelectHarness } from '@angular/material/select/testing';
import { anAccount, flushAccounts } from '../testing';
import { AccountSelect } from './account-select';

@Component({
  imports: [AccountSelect, FormField],
  template: `<app-account-select
    label="Conto"
    [excludeId]="excludeId()"
    [formField]="entryForm.accountId"
  />`,
})
class Host {
  readonly model = signal<{ accountId: string | null }>({ accountId: null });
  readonly excludeId = signal<string | null>(null);
  readonly entryForm = form(this.model, (path) => {
    required(path.accountId, { message: 'Scegli il conto.' });
  });
}

describe('AccountSelect', () => {
  let fixture: ComponentFixture<Host>;
  let host: Host;
  let backend: HttpTestingController;
  let loader: HarnessLoader;

  const checking = anAccount({ name: 'Conto corrente', balance: 1234.5 });
  const cash = anAccount({ name: 'Contanti', type: 'cash', balance: -20, currency: 'CHF' });
  const old = anAccount({ name: 'Vecchio conto', isArchived: true });

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Host);
    host = fixture.componentInstance;
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
    fixture.detectChanges();
  });

  afterEach(() => backend.verify());

  async function load(accounts = [checking, cash, old]): Promise<MatSelectHarness> {
    flushAccounts(backend, accounts);
    await fixture.whenStable();
    return loader.getHarness(MatSelectHarness);
  }

  /** Option texts with ordinary spaces and minus signs (Intl writes NBSP and U+2212). */
  const plain = (texts: string[]) =>
    texts.map((t) =>
      t
        .replace(/\s+/g, ' ')
        .replace(/\u2212/g, '-')
        .trim(),
    );

  const text = (selector: string) =>
    fixture.nativeElement.querySelector(selector)?.textContent.trim() ?? null;

  it('offers the active accounts with their balance, and puts the id in the model', async () => {
    const select = await load();
    await select.open();
    const options = await select.getOptions();

    expect(plain(await Promise.all(options.map((o) => o.getText())))).toEqual([
      'Conto corrente 1.234,50 €',
      'Contanti -20,00 CHF',
    ]);

    await options[1].click();
    expect(host.model().accountId).toBe(cash.id);
    expect(await select.getValueText()).toBe('Contanti');
  });

  it('shows an archived account only when it is the current value', async () => {
    host.model.set({ accountId: old.id });
    const select = await load();

    expect(await select.getValueText()).toBe('Vecchio conto');
    await select.open();
    const texts = await Promise.all((await select.getOptions()).map((o) => o.getText()));
    expect(plain(texts)).toEqual([
      'Conto corrente 1.234,50 €',
      'Contanti -20,00 CHF',
      'Vecchio conto (archiviato)1.339,85 €',
    ]);
  });

  it('leaves out the excluded account', async () => {
    host.excludeId.set(checking.id);
    const select = await load();
    await select.open();

    expect((await select.getOptions()).length).toBe(1);
  });

  it('shows the error of the form once the list is closed, in red', async () => {
    const select = await load();
    expect(text('mat-error')).toBeNull();

    await select.open();
    await select.close();
    await fixture.whenStable();

    expect(text('mat-error')).toBe('Scegli il conto.');
    expect(await (await select.host()).hasClass('mat-mdc-select-invalid')).toBe(true);
    expect(host.entryForm.accountId().touched()).toBe(true);
  });

  it('says when there is no active account', async () => {
    await load([old]);

    expect(text('mat-hint')).toBe('Nessun conto attivo: aggiungilo in Conti.');
  });

  it('says when the accounts could not be read', async () => {
    backend
      .expectOne('/api/accounts?includeArchived=true')
      .flush(null, { status: 0, statusText: 'Unknown Error' });
    await fixture.whenStable();

    expect(text('mat-hint')).not.toBeNull();
  });
});
