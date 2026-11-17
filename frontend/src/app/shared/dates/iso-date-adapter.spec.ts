import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form, FormField } from '@angular/forms/signals';
import { DateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDatepickerInputHarness } from '@angular/material/datepicker/testing';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { INVALID_DATE, IsoDateAdapter, provideIsoDateAdapter } from './iso-date-adapter';
import { TIME_ZONES, useTimeZone } from './testing/time-zones';

describe('IsoDateAdapter', () => {
  let adapter: DateAdapter<string>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideIsoDateAdapter()] });
    adapter = TestBed.inject(DateAdapter);
  });

  it('is the date adapter of the app, in the app locale', () => {
    expect(adapter).toBeInstanceOf(IsoDateAdapter);
    expect(adapter.getMonthNames('long')[10]).toBe('novembre');
    expect(adapter.getDayOfWeekNames('long')[0]).toBe('domenica');
    expect(adapter.getDateNames()[16]).toBe('17');
  });

  it('starts the week on Monday, as in Italy', () => {
    expect(adapter.getFirstDayOfWeek()).toBe(1);
  });

  describe.each(TIME_ZONES)('in %s', (timeZone) => {
    useTimeZone(timeZone);

    it('builds and reads dates as YYYY-MM-DD strings, months counted from 0', () => {
      const date = adapter.createDate(2026, 10, 17);

      expect(date).toBe('2026-11-17');
      expect(adapter.getYear(date)).toBe(2026);
      expect(adapter.getMonth(date)).toBe(10);
      expect(adapter.getDate(date)).toBe(17);
      expect(adapter.getDayOfWeek(date)).toBe(2);
      expect(adapter.getNumDaysInMonth('2028-02-10')).toBe(29);
      expect(adapter.toIso8601(date)).toBe('2026-11-17');
    });

    it('reads dates typed the Italian way, day first', () => {
      expect(adapter.parse('17/11/2026', null)).toBe('2026-11-17');
      expect(adapter.parse('1/2/26', null)).toBe('2026-02-01');
      expect(adapter.parse('17-11-2026', null)).toBe('2026-11-17');
      expect(adapter.parse('17.11.2026', null)).toBe('2026-11-17');
      expect(adapter.parse(' 2026-11-17 ', null)).toBe('2026-11-17');
      expect(adapter.parse('', null)).toBeNull();
    });

    it('turns text that is not a day into an invalid date', () => {
      expect(adapter.parse('31/02/2026', null)).toBe(INVALID_DATE);
      expect(adapter.parse('ieri', null)).toBe(INVALID_DATE);
      expect(adapter.isValid(INVALID_DATE)).toBe(false);
      expect(adapter.isDateInstance(INVALID_DATE)).toBe(true);
    });

    it('writes dates for the field and for screen readers', () => {
      expect(
        adapter.format('2026-01-05', { day: '2-digit', month: '2-digit', year: 'numeric' }),
      ).toBe('05/01/2026');
      expect(adapter.format('2026-11-17', { day: 'numeric', month: 'long', year: 'numeric' })).toBe(
        '17 novembre 2026',
      );
      expect(adapter.getYearName('2026-11-17')).toBe('2026');
    });

    it('moves dates like the calendar', () => {
      expect(adapter.addCalendarDays('2026-03-28', 2)).toBe('2026-03-30');
      expect(adapter.addCalendarMonths('2026-01-31', 1)).toBe('2026-02-28');
      expect(adapter.addCalendarYears('2028-02-29', 1)).toBe('2029-02-28');
    });

    it('compares dates', () => {
      expect(adapter.compareDate('2026-11-17', '2026-11-18')).toBeLessThan(0);
      expect(adapter.sameDate('2026-11-17', '2026-11-17')).toBe(true);
      expect(adapter.clampDate('2026-12-01', '2026-11-01', '2026-11-30')).toBe('2026-11-30');
    });
  });

  it('accepts ISO dates from bindings and the API', () => {
    expect(adapter.deserialize('2026-11-17')).toBe('2026-11-17');
    expect(adapter.deserialize('')).toBeNull();
    expect(adapter.deserialize(null)).toBeNull();
    expect(adapter.deserialize('2026-11-17T10:00:00Z')).toBe(INVALID_DATE);
  });

  it('refuses days that do not exist when a date is built', () => {
    expect(() => adapter.createDate(2026, 1, 29)).toThrow();
    expect(() => adapter.createDate(2026, 12, 1)).toThrow();
  });

  it('cannot format an invalid date', () => {
    expect(() => adapter.format(INVALID_DATE, {})).toThrow();
  });
});

@Component({
  imports: [MatDatepickerModule, MatFormFieldModule, MatInputModule, FormField],
  template: `<mat-form-field>
    <mat-label>Data</mat-label>
    <input matInput [matDatepicker]="picker" [formField]="entry.date" />
    <mat-datepicker #picker />
  </mat-form-field>`,
})
class DateFieldHost {
  readonly model = signal({ date: '2026-11-17' });
  readonly entry = form(this.model);
}

describe('A date picker with the ISO adapter', () => {
  describe.each(TIME_ZONES)('in %s', (timeZone) => {
    useTimeZone(timeZone);

    beforeEach(() => {
      TestBed.configureTestingModule({
        providers: [provideIsoDateAdapter(), provideHttpClient(), provideHttpClientTesting()],
      });
    });

    it('shows the date of the form and puts back exactly the day picked or typed', async () => {
      const fixture = TestBed.createComponent(DateFieldHost);
      await fixture.whenStable();
      const loader = TestbedHarnessEnvironment.loader(fixture);
      const input = await loader.getHarness(MatDatepickerInputHarness);

      expect(await input.getValue()).toBe('17/11/2026');

      await input.openCalendar();
      const calendar = await input.getCalendar();
      await calendar.selectCell({ text: '1' });
      expect(fixture.componentInstance.model().date).toBe('2026-11-01');

      await input.setValue('31/12/2026');
      await input.blur();
      expect(fixture.componentInstance.model().date).toBe('2026-12-31');
    });
  });
});
