import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { disabled, form, FormField } from '@angular/forms/signals';
import { MatSelectHarness } from '@angular/material/select/testing';
import { CategoryKind } from '..';
import { aCategory, flushCategories, someCategories } from '../testing';
import { CategorySelect } from './category-select';

@Component({
  imports: [CategorySelect, FormField],
  template: `<app-category-select
    label="Categoria"
    [kind]="kind()"
    [topLevelOnly]="topLevelOnly()"
    [excludeId]="excludeId()"
    [noneLabel]="noneLabel()"
    [formField]="entryForm.categoryId"
  />`,
})
class Host {
  readonly model = signal<{ categoryId: string | null }>({ categoryId: null });
  readonly kind = signal<CategoryKind>('expense');
  readonly topLevelOnly = signal(false);
  readonly excludeId = signal<string | null>(null);
  readonly noneLabel = signal<string | undefined>(undefined);
  readonly locked = signal(false);
  readonly entryForm = form(this.model, (path) => {
    disabled(path.categoryId, () => this.locked());
  });
}

describe('CategorySelect', () => {
  let fixture: ComponentFixture<Host>;
  let host: Host;
  let backend: HttpTestingController;
  let loader: HarnessLoader;
  const categories = someCategories();
  const [, trasporti, , carburante, svago] = categories;

  beforeEach(() => {
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

  async function optionTexts(): Promise<string[]> {
    flushCategories(backend, categories);
    await fixture.whenStable();
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    return Promise.all((await select.getOptions()).map((o) => o.getText()));
  }

  it('offers the active categories of the kind, each parent before its subcategories', async () => {
    expect(await optionTexts()).toEqual(['Spesa', 'Trasporti', 'Auto', 'Carburante']);
    const indented = Array.from(document.querySelectorAll('mat-option.sub')).map((o) =>
      o.textContent?.trim(),
    );
    expect(indented).toEqual(['Auto', 'Carburante']);
  });

  it('offers the income categories for kind income', async () => {
    host.kind.set('income');
    expect(await optionTexts()).toEqual(['Stipendio', 'Altro']);
  });

  it('shows a subcategory with its parent once chosen', async () => {
    flushCategories(backend, categories);
    await fixture.whenStable();
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    await (await select.getOptions({ text: 'Carburante' }))[0].click();

    expect(host.model().categoryId).toBe(carburante.id);
    expect(await select.getValueText()).toBe('Trasporti › Carburante');
  });

  it('offers only top-level categories, without the excluded one, and a "none" choice', async () => {
    host.topLevelOnly.set(true);
    host.excludeId.set(trasporti.id);
    host.noneLabel.set('Nessuna');

    expect(await optionTexts()).toEqual(['Nessuna', 'Spesa']);
    const select = await loader.getHarness(MatSelectHarness);
    await (await select.getOptions({ text: 'Spesa' }))[0].click();
    await select.open();
    await (await select.getOptions({ text: 'Nessuna' }))[0].click();
    expect(host.model().categoryId).toBeNull();
  });

  it('shows an archived category only when it is the current value', async () => {
    host.model.set({ categoryId: svago.id });

    expect(await optionTexts()).toContain('Svago (archiviata)');
  });

  it('follows the disabled state of its field', async () => {
    flushCategories(backend, [aCategory()]);
    await fixture.whenStable();
    const select = await loader.getHarness(MatSelectHarness);
    expect(await select.isDisabled()).toBe(false);

    host.locked.set(true);
    await fixture.whenStable();
    expect(await select.isDisabled()).toBe(true);
  });
});
