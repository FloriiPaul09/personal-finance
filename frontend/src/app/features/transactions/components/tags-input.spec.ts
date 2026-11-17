import { HarnessLoader, TestKey } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { form, FormField } from '@angular/forms/signals';
import { MatAutocompleteHarness } from '@angular/material/autocomplete/testing';
import { MatChipGridHarness } from '@angular/material/chips/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { MAX_TAGS, TagsInput } from './tags-input';

@Component({
  imports: [FormField, TagsInput],
  template: `<app-tags-input label="Tag" [suggestions]="suggestions" [formField]="tagForm.tags" />`,
})
class Host {
  readonly suggestions = ['vacanza', 'lavoro', 'casa'];
  readonly model = signal<{ tags: string[] }>({ tags: [] });
  readonly tagForm = form(this.model);
}

describe('TagsInput', () => {
  let fixture: ComponentFixture<Host>;
  let loader: HarnessLoader;

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [MatIconTestingModule] });
    fixture = TestBed.createComponent(Host);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
    await fixture.whenStable();
  });

  const tags = () => fixture.componentInstance.model().tags;

  async function typeAndEnter(text: string): Promise<void> {
    const input = await (await loader.getHarness(MatChipGridHarness)).getInput();
    await input!.setValue(text);
    await input!.sendSeparatorKey(TestKey.ENTER);
  }

  it('adds a typed tag on Enter, trimmed, and never twice regardless of case', async () => {
    await typeAndEnter('  Vacanza ');
    await typeAndEnter('vacanza');
    await typeAndEnter('   ');

    expect(tags()).toEqual(['Vacanza']);
  });

  it('splits the text at each comma, as phone keyboards type it', async () => {
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'Amici, cena,';
    input.dispatchEvent(new Event('input'));
    input.value += 'amici,lav';
    input.dispatchEvent(new Event('input'));

    expect(tags()).toEqual(['Amici', 'cena']);
    expect(input.value).toBe('lav');
  });

  it('suggests existing tags that are not chosen yet, filtered by the text', async () => {
    fixture.componentInstance.model.set({ tags: ['casa'] });
    const autocomplete = await loader.getHarness(MatAutocompleteHarness);

    await autocomplete.enterText('a');
    const options = await Promise.all((await autocomplete.getOptions()).map((o) => o.getText()));
    expect(options).toEqual(['vacanza', 'lavoro']);

    await autocomplete.selectOption({ text: 'lavoro' });
    expect(tags()).toEqual(['casa', 'lavoro']);
  });

  it('removes a tag with its button', async () => {
    fixture.componentInstance.model.set({ tags: ['casa', 'lavoro'] });
    await fixture.whenStable();
    const grid = await loader.getHarness(MatChipGridHarness);

    await (await grid.getRows({ text: /casa/ }))[0].remove();

    expect(tags()).toEqual(['lavoro']);
  });

  it(`stops at ${MAX_TAGS} tags and says so`, async () => {
    fixture.componentInstance.model.set({
      tags: Array.from({ length: MAX_TAGS }, (_, i) => `t${i}`),
    });
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('mat-hint').textContent).toContain('20 tag');
    expect(fixture.nativeElement.querySelector('input').disabled).toBe(true);
  });
});
