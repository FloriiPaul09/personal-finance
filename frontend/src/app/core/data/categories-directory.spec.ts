import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { aCategory, flushCategories, someCategories } from '../testing';
import { CategoriesDirectory } from './categories-directory';
import { DataChanges } from './data-changes';

describe('CategoriesDirectory', async () => {
  let directory: CategoriesDirectory;
  let changes: DataChanges;
  let backend: HttpTestingController;
  const categories = someCategories();
  const [spesa, trasporti, auto, carburante, svago, stipendio, altro] = categories;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    directory = TestBed.inject(CategoriesDirectory);
    changes = TestBed.inject(DataChanges);
    backend = TestBed.inject(HttpTestingController);
    TestBed.tick();
  });

  afterEach(() => backend.verify());

  /** Lets the resource take the answer just flushed. */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('reads every category once, archived ones included', async () => {
    backend.expectOne('/api/categories?includeArchived=true').flush({ items: categories });
    await settle();

    expect(directory.all()).toEqual(categories);
    expect(directory.byId(carburante.id)).toEqual(carburante);
  });

  it('groups each kind by parent, without archived categories unless asked', async () => {
    flushCategories(backend, categories);
    await settle();

    expect(directory.groups('expense')).toEqual([
      { parent: spesa, children: [] },
      { parent: trasporti, children: [auto, carburante] },
    ]);
    expect(directory.groups('expense', { includeArchived: true }).map((g) => g.parent)).toEqual([
      spesa,
      trasporti,
      svago,
    ]);
    expect(directory.groups('income')).toEqual([
      { parent: stipendio, children: [] },
      { parent: altro, children: [] },
    ]);
  });

  it('keeps archived subcategories under their archived parent', async () => {
    const casa = aCategory({ name: 'Casa', isArchived: true, hasSubcategories: true });
    const bollette = aCategory({ name: 'Bollette', parentId: casa.id, isArchived: true });
    flushCategories(backend, [casa, bollette]);
    await settle();

    expect(directory.groups('expense')).toEqual([]);
    expect(directory.groups('expense', { includeArchived: true })).toEqual([
      { parent: casa, children: [bollette] },
    ]);
  });

  it('labels a subcategory with its parent', async () => {
    flushCategories(backend, categories);
    await settle();

    expect(directory.label(carburante)).toBe('Trasporti › Carburante');
    expect(directory.label(spesa)).toBe('Spesa');
  });

  it('reads again after a write to categories, transactions or budgets', async () => {
    flushCategories(backend, categories);
    await settle();

    for (const resource of ['categories', 'transactions', 'budgets'] as const) {
      changes.notify(resource);
      TestBed.tick();
      flushCategories(backend, categories);
      await settle();
    }
    changes.notify('accounts', 'transfers');
    TestBed.tick();
    backend.expectNone('/api/categories?includeArchived=true');
  });
});
