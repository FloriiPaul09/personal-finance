import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { MatMenuHarness } from '@angular/material/menu/testing';
import { MatSlideToggleHarness } from '@angular/material/slide-toggle/testing';
import { MatSnackBarHarness } from '@angular/material/snack-bar/testing';
import { MatTabNavBarHarness } from '@angular/material/tabs/testing';
import { provideRouter } from '@angular/router';
import { CategoryResponse } from '../../../core';
import {
  aCategory,
  flushCategories,
  provideApiHttpTesting,
  someCategories,
} from '../../../core/testing';
import { CategoriesPage } from './categories-page';

describe('CategoriesPage', () => {
  let fixture: ComponentFixture<CategoriesPage>;
  let backend: HttpTestingController;
  let loader: HarnessLoader;
  const categories = someCategories();
  const [spesa, trasporti, auto, , svago] = categories;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [provideApiHttpTesting(), provideRouter([])],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CategoriesPage);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  });

  afterEach(() => backend.verify());

  async function show(items: CategoryResponse[] = categories, kind?: string): Promise<void> {
    if (kind) {
      fixture.componentRef.setInput('kind', kind);
    }
    fixture.detectChanges();
    flushCategories(backend, items);
    await fixture.whenStable();
  }

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
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

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () =>
    Array.from(el().querySelectorAll('[data-testid="category-row"]')).map(
      (row) => (row.classList.contains('sub') ? '  ' : '') + row.textContent!.trim(),
    );

  async function openMenu(name: string): Promise<MatMenuHarness> {
    const menu = await loader.getHarness(
      MatMenuHarness.with({ selector: `[aria-label="Azioni per ${name}"]` }),
    );
    await menu.open();
    return menu;
  }

  it('shows the expense categories, subcategories indented under their parent', async () => {
    await show();

    expect(rows()).toEqual(['Spesa', 'Trasporti', '  Auto', '  Carburante']);
    const tabs = await loader.getHarness(MatTabNavBarHarness);
    expect(await (await tabs.getActiveLink()).getLabel()).toBe('Uscite');
  });

  it('shows the income categories on their tab', async () => {
    await show(categories, 'income');

    expect(rows()).toEqual(['Stipendio', 'Altro']);
    const tabs = await loader.getHarness(MatTabNavBarHarness);
    expect(await (await tabs.getActiveLink()).getLabel()).toBe('Entrate');
    const add = el().querySelector<HTMLAnchorElement>('[data-testid="new-category"]')!;
    expect(add.getAttribute('href')).toBe('/new?kind=income');
  });

  it('shows the archived categories on request', async () => {
    await show();

    await (await loader.getHarness(MatSlideToggleHarness)).check();

    expect(rows()).toEqual(['Spesa', 'Trasporti', '  Auto', '  Carburante', 'SvagoArchiviata']);
  });

  it('offers to add a subcategory to a top-level category only', async () => {
    await show();

    const parentMenu = await openMenu('Trasporti');
    const [add] = await parentMenu.getItems({ text: /Aggiungi sottocategoria/ });
    expect(await (await add.host()).getAttribute('href')).toBe(
      `/new?kind=expense&parent=${trasporti.id}`,
    );
    await parentMenu.close();

    const childMenu = await openMenu('Auto');
    expect(await childMenu.getItems({ text: /Aggiungi sottocategoria/ })).toEqual([]);
  });

  it('says why a category cannot be deleted', async () => {
    await show();

    const used = await openMenu('Spesa');
    const [deleteUsed] = await used.getItems({ text: /Elimina/ });
    expect(await deleteUsed.isDisabled()).toBe(true);
    expect(await deleteUsed.getText()).toContain('È usata: puoi archiviarla');
    await used.close();

    const parent = await openMenu('Trasporti');
    const [deleteParent] = await parent.getItems({ text: /Elimina/ });
    expect(await deleteParent.isDisabled()).toBe(true);
    expect(await deleteParent.getText()).toContain('Ha delle sottocategorie');
  });

  it('archives a parent and "Annulla" restores it with the subcategories it archived', async () => {
    const archivedAuto = aCategory({ name: 'Ricambi', parentId: trasporti.id, isArchived: true });
    const [, , , carburante] = categories;
    await show([...categories, archivedAuto]);

    const menu = await openMenu('Trasporti');
    expect(await (await menu.getItems({ text: /Archivia/ }))[0].getText()).toContain(
      'Anche le sottocategorie',
    );
    await menu.clickItem({ text: /Archivia/ });
    backend
      .expectOne(`/api/categories/${trasporti.id}/archive`)
      .flush({ ...trasporti, isArchived: true });
    await settle();
    flushCategories(backend, categories);
    await settle();

    const snackBar = await loader.getHarness(MatSnackBarHarness);
    expect(await snackBar.getMessage()).toBe('Categoria «Trasporti» archiviata.');
    await snackBar.dismissWithAction();
    backend.expectOne(`/api/categories/${trasporti.id}/unarchive`).flush(trasporti);
    await settle();
    flushCategories(backend, categories);
    backend.expectOne(`/api/categories/${auto.id}/unarchive`).flush(auto);
    await settle();
    flushCategories(backend, categories);
    backend.expectOne(`/api/categories/${carburante.id}/unarchive`).flush(carburante);
    await settle();
    flushCategories(backend, categories);
    // The subcategory archived before stays archived.
    backend.expectNone(`/api/categories/${archivedAuto.id}/unarchive`);
  });

  it('restores a subcategory only after its parent', async () => {
    const casa = aCategory({ name: 'Casa', isArchived: true, hasSubcategories: true });
    const bollette = aCategory({ name: 'Bollette', parentId: casa.id, isArchived: true });
    await show([spesa, casa, bollette]);
    await (await loader.getHarness(MatSlideToggleHarness)).check();

    const childMenu = await openMenu('Bollette');
    const [restoreChild] = await childMenu.getItems({ text: /Ripristina/ });
    expect(await restoreChild.isDisabled()).toBe(true);
    expect(await restoreChild.getText()).toContain('Ripristina prima «Casa»');
    await childMenu.close();

    const parentMenu = await openMenu('Casa');
    await parentMenu.clickItem({ text: /Ripristina/ });
    backend.expectOne(`/api/categories/${casa.id}/unarchive`).flush({ ...casa, isArchived: false });
    await settle();
    flushCategories(backend, [spesa, { ...casa, isArchived: false }, bollette]);
    await settle();
    expect(await (await loader.getHarness(MatSnackBarHarness)).getMessage()).toBe(
      'Categoria «Casa» ripristinata.',
    );
  });

  it('deletes an unused category after confirming', async () => {
    await show(categories, 'income');
    const menu = await openMenu('Altro');

    await menu.clickItem({ text: /Elimina/ });
    expect(await (await loader.getHarness(MatDialogHarness)).getTitleText()).toBe(
      'Eliminare «Altro»?',
    );
    document.querySelector<HTMLButtonElement>('mat-dialog-actions .confirm')!.click();

    const [, , , , , , altro] = categories;
    const request = await nextRequest(`/api/categories/${altro.id}`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle();
    flushCategories(backend, categories.slice(0, 6));
    await settle();
    expect(await (await loader.getHarness(MatSnackBarHarness)).getMessage()).toBe(
      'Categoria «Altro» eliminata.',
    );
  });

  it('says when every category of the kind is archived, or when there is none', async () => {
    await show([svago]);
    expect(el().querySelector('app-empty-state')!.textContent).toContain(
      'Tutte le categorie sono archiviate',
    );

    fixture.componentRef.setInput('kind', 'income');
    await fixture.whenStable();
    expect(el().querySelector('app-empty-state')!.textContent).toContain('Nessuna categoria');
  });
});
