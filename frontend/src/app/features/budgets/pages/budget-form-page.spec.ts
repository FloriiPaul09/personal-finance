import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { provideRouter } from '@angular/router';
import { BudgetResponse, Notifier, PageExit } from '../../../core';
import {
  aBudget,
  aCategory,
  flushCategories,
  problem,
  provideApiHttpTesting,
} from '../../../core/testing';
import { BudgetFormPage } from './budget-form-page';

describe('BudgetFormPage', () => {
  let fixture: ComponentFixture<BudgetFormPage>;
  let backend: HttpTestingController;
  let loader: HarnessLoader;
  let show: ReturnType<typeof vi.fn>;
  let leave: ReturnType<typeof vi.fn>;

  const spesa = aCategory({ name: 'Spesa' });
  const casa = aCategory({ name: 'Casa', hasSubcategories: true });
  const affitto = aCategory({ name: 'Affitto', parentId: casa.id });
  const trasporti = aCategory({ name: 'Trasporti', hasSubcategories: true });
  const carburante = aCategory({ name: 'Carburante', parentId: trasporti.id });
  const stipendio = aCategory({ name: 'Stipendio', kind: 'income' });
  const categories = [spesa, casa, affitto, trasporti, carburante, stipendio];

  const casaJanuary = aBudget({ categoryId: casa.id, from: '2026-01', amount: 300 });
  const casaJune = aBudget({ categoryId: casa.id, from: '2026-06', amount: 350, version: 9 });
  const casaStop = aBudget({ categoryId: casa.id, from: '2026-09', amount: null });
  const carburanteBudget = aBudget({ categoryId: carburante.id, from: '2026-02', amount: 80 });

  beforeEach(() => {
    show = vi.fn().mockResolvedValue(undefined);
    leave = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [
        provideApiHttpTesting(),
        provideRouter([]),
        { provide: Notifier, useValue: { show, undoable: vi.fn() } },
        { provide: PageExit, useValue: { leave } },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(BudgetFormPage);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  });

  afterEach(() => {
    // Reads that follow a write (entries, categories in use) are not the subject here.
    backend.match('/api/budgets');
    backend.match('/api/categories?includeArchived=true');
    backend.verify();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const component = () =>
    fixture.componentInstance as unknown as {
      model: { (): Record<string, unknown>; update: (fn: (m: object) => object) => void };
    };

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  async function open(
    inputs: { categoryId?: string; month?: string },
    entries: BudgetResponse[] = [casaJanuary, casaJune, casaStop, carburanteBudget],
  ): Promise<void> {
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    flushCategories(backend, categories);
    backend.expectOne('/api/budgets').flush({ items: entries });
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

  async function nextRequest(url: string, method: string): Promise<TestRequest> {
    for (let attempt = 0; attempt < 50; attempt++) {
      const [request] = backend.match((r) => r.urlWithParams === url && r.method === method);
      if (request) {
        return request;
      }
      await new Promise((resolve) => setTimeout(resolve, 10));
    }
    throw new Error(`No ${method} ${url}`);
  }

  const errors = () =>
    Array.from(el().querySelectorAll('mat-error')).map((e) => e.textContent!.trim());
  const formErrors = () =>
    Array.from(el().querySelectorAll('.form-error')).map((e) => e.textContent!.trim());

  describe('new budget', () => {
    it('starts from the month of the budgets page, with the focus on the limit', async () => {
      await open({ month: '2026-11' });

      expect(el().querySelector('h1')!.textContent!.trim()).toBe('Nuovo budget');
      expect(component().model()).toEqual({ categoryId: null, from: '2026-11', amount: null });
      expect(el().querySelector('.month')!.textContent).toContain('Novembre 2026');
      expect(document.activeElement?.closest('app-amount-input')).not.toBeNull();
      expect(el().querySelector('[data-testid="budget-entry"]')).toBeNull();
    });

    it('offers expense categories only, those that cannot get a budget disabled with the reason', async () => {
      await open({ month: '2026-11' });
      const select = await loader.getHarness(MatSelectHarness);
      await select.open();

      const options = await select.getOptions();
      const disabled = await Promise.all(options.map((o) => o.isDisabled()));
      const shown = Array.from(document.querySelectorAll('mat-option')).map((option, i) => [
        option.querySelector('.option-text > span')!.textContent!.trim(),
        option.querySelector('.reason')?.textContent!.trim() ?? null,
        disabled[i],
      ]);
      expect(shown).toEqual([
        ['Spesa', null, false],
        ['Casa', 'Ha già un budget: cambialo dalla pagina Budget', true],
        ['Affitto', 'Il budget è su «Casa»', true],
        ['Trasporti', 'Il budget è sulle sue sottocategorie', true],
        ['Carburante', 'Ha già un budget: cambialo dalla pagina Budget', true],
      ]);
    });

    it('says what is missing', async () => {
      await open({ month: '2026-11' });

      await submit();

      expect(errors()).toEqual(['Scegli la categoria.', 'Inserisci il limite.']);
      await fill({ amount: 0 });
      expect(errors()).toContain('Il limite deve essere maggiore di zero.');
    });

    it('adds the entry of the month and goes back to the budgets of that month', async () => {
      await open({ month: '2026-11' });
      await fill({ categoryId: spesa.id, amount: 400 });

      await submit();
      (await nextRequest(`/api/budgets?categoryId=${spesa.id}`, 'GET')).flush({ items: [] });
      const post = await nextRequest('/api/budgets', 'POST');
      expect(post.request.body).toEqual({ categoryId: spesa.id, from: '2026-11', amount: 400 });
      post.flush(aBudget({ categoryId: spesa.id, from: '2026-11', amount: 400 }));
      await settle();

      expect(show).toHaveBeenCalledWith('Budget salvato.');
      expect(leave).toHaveBeenCalledWith('/budgets?month=2026-11');
    });

    it('shows a refusal about the category on its field', async () => {
      await open({ month: '2026-11' });
      await fill({ categoryId: spesa.id, amount: 400 });

      await submit();
      (await nextRequest(`/api/budgets?categoryId=${spesa.id}`, 'GET')).flush({ items: [] });
      (await nextRequest('/api/budgets', 'POST')).flush(...problem(409, 'budget.branch_conflict'));
      await settle();

      expect(errors()).toEqual([
        'Un budget sta sulla categoria madre oppure sulle sue sottocategorie, non su entrambe.',
      ]);
      expect(leave).not.toHaveBeenCalled();
    });
  });

  describe('budget of a category', () => {
    it('shows the limit in force in the month and the history, latest first', async () => {
      await open({ categoryId: casa.id, month: '2026-07' });

      expect(el().querySelector('h1')!.textContent!.trim()).toBe('Budget di Casa');
      expect(el().querySelector('app-category-select')).toBeNull();
      expect(component().model()).toEqual({ categoryId: casa.id, from: '2026-07', amount: 350 });
      const entries = Array.from(el().querySelectorAll('[data-testid="budget-entry"]')).map((e) =>
        Array.from(e.querySelectorAll('.entry-month, .entry-value'))
          .map((part) => part.textContent!.trim())
          .join(' ')
          .replace(/\s+/g, ' '),
      );
      expect(entries).toEqual([
        'Da settembre 2026 Fermato',
        'Da giugno 2026 350,00 €',
        'Da gennaio 2026 300,00 €',
      ]);
    });

    it('starts empty in a month after the budget was stopped', async () => {
      await open({ categoryId: casa.id, month: '2026-11' });

      expect(component().model()['amount']).toBeNull();
    });

    it('changes the entry of the month chosen when it exists', async () => {
      await open({ categoryId: casa.id, month: '2026-07' });
      el().querySelector<HTMLButtonElement>('.month app-month-selector button')!.click();
      await settle();
      expect(component().model()['from']).toBe('2026-06');
      await fill({ amount: 360 });

      await submit();
      (await nextRequest(`/api/budgets?categoryId=${casa.id}`, 'GET')).flush({
        items: [casaJanuary, casaJune, casaStop],
      });
      const put = await nextRequest(`/api/budgets/${casaJune.id}`, 'PUT');
      expect(put.request.headers.get('If-Match')).toBe('"9"');
      expect(put.request.body).toEqual({ amount: 360 });
      put.flush({ ...casaJune, amount: 360, version: 10 });
      await settle();

      expect(leave).toHaveBeenCalledWith('/budgets?month=2026-06');
    });

    it('shows a message when the entry changed meanwhile', async () => {
      await open({ categoryId: casa.id, month: '2026-06' });

      await submit();
      (await nextRequest(`/api/budgets?categoryId=${casa.id}`, 'GET')).flush({
        items: [casaJune],
      });
      (await nextRequest(`/api/budgets/${casaJune.id}`, 'PUT')).flush(
        ...problem(412, 'concurrency.version_mismatch'),
      );
      await settle();

      expect(formErrors()[0]).toContain('Il budget è stato modificato altrove');
      expect(leave).not.toHaveBeenCalled();
    });

    it('deletes a wrong entry after a confirmation', async () => {
      await open({ categoryId: casa.id, month: '2026-07' });
      const buttons = el().querySelectorAll<HTMLButtonElement>('[data-testid="delete-entry"]');
      expect(buttons[0].getAttribute('aria-label')).toBe('Elimina la voce di settembre 2026');

      buttons[0].click();
      const dialog = await loader.getHarness(MatDialogHarness);
      expect(await dialog.getText()).toContain('Serve a correggere un errore');
      await (await dialog.getHarness(MatButtonHarness.with({ text: 'Elimina' }))).click();

      const del = await nextRequest(`/api/budgets/${casaStop.id}`, 'DELETE');
      expect(del.request.headers.get('If-Match')).toBe(`"${casaStop.version}"`);
      del.flush(null, { status: 204, statusText: 'No Content' });
      await settle();
      expect(show).toHaveBeenCalledWith('Voce eliminata.');
    });

    it('says when the category does not exist', async () => {
      await open({ categoryId: 'unknown', month: '2026-11' });

      expect(el().querySelector('app-empty-state')!.textContent).toContain('Categoria non trovata');
      expect(el().querySelector('form')).toBeNull();
    });
  });
});
