import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { MatMenuHarness } from '@angular/material/menu/testing';
import { MatSlideToggleHarness } from '@angular/material/slide-toggle/testing';
import { MatSnackBarHarness } from '@angular/material/snack-bar/testing';
import { provideRouter } from '@angular/router';
import { AccountResponse } from '../../../core';
import { anAccount, flushAccounts, provideApiHttpTesting } from '../../../core/testing';
import { AccountsPage } from './accounts-page';

describe('AccountsPage', () => {
  let fixture: ComponentFixture<AccountsPage>;
  let backend: HttpTestingController;
  let loader: HarnessLoader;

  const checking = anAccount({ name: 'Conto corrente', balance: 1234.5 });
  const card = anAccount({ name: 'Carta', type: 'creditCard', balance: -80, hasMovements: false });
  const old = anAccount({ name: 'Vecchio conto', isArchived: true });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [provideApiHttpTesting(), provideRouter([])],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AccountsPage);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
    fixture.detectChanges();
  });

  afterEach(() => backend.verify());

  async function show(accounts: AccountResponse[]): Promise<void> {
    flushAccounts(backend, accounts);
    await fixture.whenStable();
  }

  /** Lets a write finish and its effects run, without waiting for the requests they start. */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  /** The request, once the dialog has closed and the page has sent it. */
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

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () =>
    Array.from(el().querySelectorAll('[data-testid="account-row"]')).map((row) =>
      row.textContent!.replace(/\s+/g, ' ').replace(/−/g, '-').trim(),
    );

  async function openMenu(name: string): Promise<MatMenuHarness> {
    const menu = await loader.getHarness(
      MatMenuHarness.with({ selector: `[aria-label="Azioni per ${name}"]` }),
    );
    await menu.open();
    return menu;
  }

  it('lists the active accounts with type and balance, each opening its form', async () => {
    await show([checking, card, old]);

    expect(rows()).toEqual([
      'Conto corrente Conto corrente 1.234,50 €',
      'Carta Carta di credito -80,00 €',
    ]);
    const link = el().querySelector<HTMLAnchorElement>('[data-testid="account-row"] a')!;
    expect(link.getAttribute('href')).toBe(`/${checking.id}`);
    expect(el().querySelector('.balance.pf-expense')!.textContent).toContain('80,00');
  });

  it('shows the archived accounts on request', async () => {
    await show([checking, old]);
    const toggle = await loader.getHarness(MatSlideToggleHarness);

    await toggle.check();

    expect(rows()[1]).toBe('Vecchio conto Conto corrente · Archiviato1.339,85 €');
  });

  it('has no archived toggle when nothing is archived', async () => {
    await show([checking]);

    expect(await loader.getAllHarnesses(MatSlideToggleHarness)).toEqual([]);
  });

  it('invites to add the first account when there is none', async () => {
    await show([]);

    expect(el().querySelector('app-empty-state')!.textContent).toContain('Nessun conto');
    expect(el().querySelector('app-empty-state a')!.getAttribute('href')).toBe('/new');
  });

  it('says when every account is archived', async () => {
    await show([old]);

    expect(el().querySelector('app-empty-state')!.textContent).toContain(
      'Tutti i conti sono archiviati',
    );
  });

  it('shows the error of the list with a retry', async () => {
    backend
      .expectOne('/api/accounts?includeArchived=true')
      .flush(null, { status: 0, statusText: 'Unknown Error' });
    await fixture.whenStable();

    expect(el().querySelector('app-error-state')).not.toBeNull();
  });

  it('archives without asking, and "Annulla" restores the account', async () => {
    await show([checking, card]);
    const menu = await openMenu('Conto corrente');

    await menu.clickItem({ text: /Archivia/ });
    backend
      .expectOne(`/api/accounts/${checking.id}/archive`)
      .flush({ ...checking, isArchived: true });
    await settle();
    flushAccounts(backend, [card, { ...checking, isArchived: true }]);
    await fixture.whenStable();

    const snackBar = await loader.getHarness(MatSnackBarHarness);
    expect(await snackBar.getMessage()).toBe('Conto «Conto corrente» archiviato.');
    expect(rows()).toEqual(['Carta Carta di credito -80,00 €']);

    await snackBar.dismissWithAction();
    await settle();
    backend.expectOne(`/api/accounts/${checking.id}/unarchive`).flush(checking);
    await settle();
    flushAccounts(backend, [checking, card]);
    await fixture.whenStable();
    expect(rows().length).toBe(2);
  });

  it('restores an archived account', async () => {
    await show([checking, old]);
    await (await loader.getHarness(MatSlideToggleHarness)).check();
    const menu = await openMenu('Vecchio conto');

    await menu.clickItem({ text: /Ripristina/ });
    backend.expectOne(`/api/accounts/${old.id}/unarchive`).flush({ ...old, isArchived: false });
    await settle();
    flushAccounts(backend, [checking, { ...old, isArchived: false }]);
    await fixture.whenStable();

    expect(await (await loader.getHarness(MatSnackBarHarness)).getMessage()).toBe(
      'Conto «Vecchio conto» ripristinato.',
    );
  });

  it('does not offer to delete an account with movements, and says why', async () => {
    await show([checking]);
    const menu = await openMenu('Conto corrente');
    const [item] = await menu.getItems({ text: /Elimina/ });

    expect(await item.isDisabled()).toBe(true);
    expect(await item.getText()).toContain('Ha dei movimenti: puoi archiviarlo');
  });

  it('deletes an account without movements after confirming', async () => {
    await show([checking, card]);
    const menu = await openMenu('Carta');

    await menu.clickItem({ text: /Elimina/ });
    const dialog = await loader.getHarness(MatDialogHarness);
    expect(await dialog.getTitleText()).toBe('Eliminare «Carta»?');
    const confirm = document.querySelector<HTMLButtonElement>('mat-dialog-actions .confirm')!;
    confirm.click();

    const request = await nextRequest(`/api/accounts/${card.id}`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle();
    flushAccounts(backend, [checking]);
    await fixture.whenStable();
    expect(rows().length).toBe(1);
  });

  it('deletes nothing when the confirmation is cancelled', async () => {
    await show([card]);
    const menu = await openMenu('Carta');

    await menu.clickItem({ text: /Elimina/ });
    await (await loader.getHarness(MatDialogHarness)).close();
    await fixture.whenStable();

    backend.expectNone(`/api/accounts/${card.id}`);
  });
});
