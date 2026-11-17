import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { form, FormField, required } from '@angular/forms/signals';
import { MatDatepickerToggleHarness } from '@angular/material/datepicker/testing';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { DateInput } from './date-input';
import { provideIsoDateAdapter } from '../dates';
import { TIME_ZONES, useTimeZone } from '../testing';

@Component({
  imports: [DateInput, FormField],
  template: `<app-date-input label="Data" [formField]="entry.date" />`,
})
class Host {
  readonly model = signal<{ date: string | null }>({ date: '2026-11-17' });
  readonly entry = form(this.model, (path) => {
    required(path.date, { message: 'Inserisci la data.' });
  });
}

describe('DateInput', () => {
  describe.each(TIME_ZONES)('in %s', (timeZone) => {
    useTimeZone(timeZone);

    let fixture: ComponentFixture<Host>;
    let host: Host;
    let input: HTMLInputElement;

    beforeEach(async () => {
      TestBed.configureTestingModule({
        imports: [MatIconTestingModule],
        providers: [provideIsoDateAdapter()],
      });
      fixture = TestBed.createComponent(Host);
      host = fixture.componentInstance;
      await fixture.whenStable();
      input = fixture.nativeElement.querySelector('input');
    });

    async function type(text: string): Promise<void> {
      input.value = text;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    }

    async function leave(): Promise<void> {
      input.dispatchEvent(new Event('blur'));
      await fixture.whenStable();
    }

    const error = () =>
      fixture.nativeElement.querySelector('mat-error')?.textContent.trim() ?? null;

    it('shows the date of the form the Italian way', () => {
      expect(input.value).toBe('17/11/2026');
      expect(fixture.nativeElement.querySelector('mat-label').textContent).toContain('Data');
    });

    it('puts the day typed in the model as YYYY-MM-DD, and leaves the text alone while typing', async () => {
      await type('1/2/26');

      expect(host.model().date).toBe('2026-02-01');
      expect(input.value).toBe('1/2/26');

      await type('1/2/2026');
      expect(host.model().date).toBe('2026-02-01');
      expect(input.value).toBe('1/2/2026');
    });

    it('makes the form invalid with an Italian message when the text is not a day', async () => {
      await type('31/02/2026');

      expect(host.entry.date().valid()).toBe(false);
      expect(error()).toBeNull();

      await leave();

      expect(error()).toBe('Data non valida, ad esempio 17/11/2026.');
      expect(input.getAttribute('aria-invalid')).toBe('true');
    });

    it('becomes valid again when the text is corrected', async () => {
      await type('31/02/2026');
      await leave();
      await type('28/02/2026');

      expect(host.entry.date().valid()).toBe(true);
      expect(error()).toBeNull();
      expect(host.model().date).toBe('2026-02-28');
    });

    it('empties the model when the text is cleared, and shows the required message', async () => {
      await type('');
      await leave();

      expect(host.model().date).toBeNull();
      expect(error()).toBe('Inserisci la data.');
    });

    it('shows a date set by the page', async () => {
      host.model.set({ date: '2028-02-29' });
      await fixture.whenStable();

      expect(input.value).toBe('29/02/2028');
    });

    it('puts the day picked in the calendar in the model, without moving it', async () => {
      const loader = TestbedHarnessEnvironment.loader(fixture);
      const toggle = await loader.getHarness(MatDatepickerToggleHarness);

      await toggle.openCalendar();
      const calendar = await toggle.getCalendar();
      await calendar!.selectCell({ text: '30' });
      await fixture.whenStable();

      expect(host.model().date).toBe('2026-11-30');
      expect(input.value).toBe('30/11/2026');
    });
  });

  it('speaks Italian in the calendar', async () => {
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
      providers: [provideIsoDateAdapter()],
    });
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();

    const toggle = fixture.nativeElement.querySelector('mat-datepicker-toggle button');
    expect(toggle.getAttribute('aria-label')).toBe('Apri il calendario');
  });
});
