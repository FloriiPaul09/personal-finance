import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatButtonToggleGroupHarness } from '@angular/material/button-toggle/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { provideRouter, Router } from '@angular/router';
import { CategoryResponse, Notifier } from '../../../core';
import {
  aCategory,
  flushCategories,
  problem,
  provideApiHttpTesting,
  someCategories,
} from '../../../core/testing';
import { CategoryFormPage } from './category-form-page';

describe('CategoryFormPage', () => {
  let fixture: ComponentFixture<CategoryFormPage>;
  let backend: HttpTestingController;
  let loader: HarnessLoader;
  let navigate: ReturnType<typeof vi.spyOn>;
  let open: ReturnType<typeof vi.fn>;
  const categories = someCategories();
  const [spesa, trasporti, auto, carburante, , , altro] = categories;

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
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(CategoryFormPage);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  });

  afterEach(() => backend.verify());

  async function openForm(
    inputs: { id?: string; kind?: string; parent?: string } = {},
    items: CategoryResponse[] = categories,
  ): Promise<void> {
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    fixture.detectChanges();
    flushCategories(backend, items);
    await fixture.whenStable();
  }

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  async function save(): Promise<void> {
    el()
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { cancelable: true }));
    await settle();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const name = () => el().querySelector<HTMLInputElement>('input')!;

  async function typeName(text: string): Promise<void> {
    name().value = text;
    name().dispatchEvent(new Event('input'));
    name().dispatchEvent(new Event('blur'));
    await fixture.whenStable();
  }

  const kindGroup = () => loader.getHarness(MatButtonToggleGroupHarness);
  const parentSelect = () => loader.getHarness(MatSelectHarness);

  async function checkedKind(): Promise<string> {
    const [checked] = await (await kindGroup()).getToggles({ checked: true });
    return checked.getText();
  }

  it('creates an expense category by default, top-level', async () => {
    await openForm();
    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Nuova categoria');
    expect(await checkedKind()).toBe('Uscita');

    await typeName(' Animali ');
    await save();
    const request = backend.expectOne('/api/categories');
    expect(request.request.body).toEqual({ name: 'Animali', kind: 'expense', parentId: null });
    request.flush(aCategory({ name: 'Animali' }));
    await settle();
    flushCategories(backend, categories);
    await settle();

    expect(open).toHaveBeenCalledWith('Categoria creata.');
    expect(navigate).toHaveBeenCalledWith(['/categories'], { queryParams: { kind: 'expense' } });
  });

  it('starts a subcategory from the query string, choosing among top-level parents', async () => {
    await openForm({ kind: 'expense', parent: trasporti.id });
    const parent = await parentSelect();

    expect(await parent.getValueText()).toBe('Trasporti');
    await parent.open();
    const options = await Promise.all((await parent.getOptions()).map((o) => o.getText()));
    expect(options).toEqual(['Nessuna: è una categoria principale', 'Spesa', 'Trasporti']);
  });

  it('starts an income category from the query string', async () => {
    await openForm({ kind: 'income' });

    expect(await checkedKind()).toBe('Entrata');
  });

  it('makes a subcategory top-level when its kind changes', async () => {
    await openForm({ kind: 'expense', parent: trasporti.id });

    await (await (await kindGroup()).getToggles({ text: 'Entrata' }))[0].check();
    await typeName('Rimborso benzina');
    await save();

    const request = backend.expectOne('/api/categories');
    expect(request.request.body).toEqual({
      name: 'Rimborso benzina',
      kind: 'income',
      parentId: null,
    });
    request.flush(aCategory({ kind: 'income' }));
    await settle();
    flushCategories(backend, categories);
    await settle();
    expect(navigate).toHaveBeenCalledWith(['/categories'], { queryParams: { kind: 'income' } });
  });

  it('edits a category with its version, leaving itself out of the parents', async () => {
    await openForm({ id: altro.id });
    expect(el().querySelector('h1')!.textContent!.trim()).toBe('Modifica categoria');
    expect(name().value).toBe('Altro');
    const parent = await parentSelect();
    await parent.open();
    expect(await Promise.all((await parent.getOptions()).map((o) => o.getText()))).toEqual([
      'Nessuna: è una categoria principale',
      'Stipendio',
    ]);
    await parent.close();

    await typeName('Altre entrate');
    await save();
    const request = backend.expectOne(`/api/categories/${altro.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('If-Match')).toBe(`"${altro.version}"`);
    request.flush({ ...altro, name: 'Altre entrate' });
    await settle();
    flushCategories(backend, categories);
    await settle();
    expect(open).toHaveBeenCalledWith('Categoria salvata.');
  });

  it('locks the kind of a used category and says why', async () => {
    await openForm({ id: carburante.id });

    expect(await (await kindGroup()).isDisabled()).toBe(true);
    expect(el().textContent).toContain('Non si può cambiare: la categoria è già usata.');
    expect(await (await parentSelect()).isDisabled()).toBe(false);
  });

  it('keeps a category with subcategories top-level, with its kind', async () => {
    await openForm({ id: trasporti.id });

    expect(await (await kindGroup()).isDisabled()).toBe(true);
    expect(el().textContent).toContain('Non si può cambiare: ha delle sottocategorie.');
    expect(await (await parentSelect()).isDisabled()).toBe(true);
    expect(el().textContent).toContain('Ha delle sottocategorie: resta una categoria principale.');
  });

  it('shows a name already used under the name field', async () => {
    await openForm({ id: auto.id });
    await typeName('Carburante');

    await save();
    backend.expectOne(`/api/categories/${auto.id}`).flush(...problem(409, 'category.name_taken'));
    await settle();
    flushCategories(backend, categories);
    await settle();

    expect(el().querySelector('mat-error')!.textContent!.trim()).toBe(
      'Esiste già una categoria con questo nome.',
    );
  });

  it('shows a refused parent under the parent field', async () => {
    await openForm({ id: spesa.id });
    const parent = await parentSelect();
    await parent.open();
    await parent.clickOptions({ text: 'Trasporti' });

    await save();
    backend
      .expectOne(`/api/categories/${spesa.id}`)
      .flush(...problem(409, 'budget.branch_conflict'));
    await settle();
    flushCategories(backend, categories);
    await settle();

    expect(el().querySelector('app-category-select mat-error')!.textContent!.trim()).toContain(
      'Un budget sta sulla categoria madre',
    );
  });

  it('shows the current data when the category changed elsewhere (412)', async () => {
    await openForm({ id: altro.id });
    await typeName('Mio nome');

    await save();
    backend
      .expectOne(`/api/categories/${altro.id}`)
      .flush(...problem(412, 'concurrency.version_mismatch'));
    await settle();
    flushCategories(backend, [...categories.slice(0, 6), { ...altro, name: 'Varie', version: 2 }]);
    await settle();

    expect(el().querySelector('.form-error')!.textContent).toContain(
      'La categoria è stata modificata altrove',
    );
    expect(name().value).toBe('Varie');
  });

  it('asks for a name before calling the API', async () => {
    await openForm();

    await save();

    backend.expectNone('/api/categories');
    expect(el().querySelector('mat-error')!.textContent!.trim()).toBe(
      'Inserisci il nome della categoria.',
    );
    expect(document.activeElement).toBe(name());
  });

  it('says when the category does not exist', async () => {
    await openForm({ id: 'unknown-id' });

    expect(el().querySelector('app-empty-state')!.textContent).toContain('Categoria non trovata');
  });
});
