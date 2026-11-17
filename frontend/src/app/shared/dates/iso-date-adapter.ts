import { inject, LOCALE_ID, Provider, Service } from '@angular/core';
import {
  DateAdapter,
  MAT_DATE_FORMATS,
  MAT_DATE_LOCALE,
  MatDateFormats,
} from '@angular/material/core';
import {
  addDays,
  addMonths,
  dateParts,
  dayOfWeek,
  daysInMonth,
  formatIsoDate,
  IsoDate,
  isIsoDate,
  isoDate,
  todayIso,
} from './iso-date';

/** The value of an invalid date typed in the field (the date picker needs one). */
export const INVALID_DATE = 'invalid-date';

/**
 * Material date adapter whose dates are `YYYY-MM-DD` strings, the format of the API.
 * A form field with a date picker holds exactly what is sent: no `Date`, so no time zone can
 * move the day (docs/frontend/ui-components.md#dates-and-amounts). Typed dates are read the
 * Italian way, day first: `17/11/2026`, `17/11/26`, `17-11-2026`, `17.11.2026`, `2026-11-17`.
 */
@Service({ autoProvided: false })
export class IsoDateAdapter extends DateAdapter<IsoDate, string> {
  constructor() {
    super();
    const matDateLocale: unknown = inject(MAT_DATE_LOCALE, { optional: true });
    super.setLocale(typeof matDateLocale === 'string' ? matDateLocale : inject(LOCALE_ID));
  }

  getYear(date: IsoDate): number {
    return dateParts(date).year;
  }

  /** 0–11, as Material expects. */
  getMonth(date: IsoDate): number {
    return dateParts(date).month - 1;
  }

  getDate(date: IsoDate): number {
    return dateParts(date).day;
  }

  getDayOfWeek(date: IsoDate): number {
    return dayOfWeek(date);
  }

  getMonthNames(style: 'long' | 'short' | 'narrow'): string[] {
    return range(12, (i) => this.formatUtc(isoDate(2017, i + 1, 1), { month: style }));
  }

  getDateNames(): string[] {
    return range(31, (i) => this.formatUtc(isoDate(2017, 1, i + 1), { day: 'numeric' }));
  }

  /** Starting on Sunday, as Material expects (1 January 2017 was a Sunday). */
  getDayOfWeekNames(style: 'long' | 'short' | 'narrow'): string[] {
    return range(7, (i) => this.formatUtc(isoDate(2017, 1, i + 1), { weekday: style }));
  }

  getYearName(date: IsoDate): string {
    return this.formatUtc(date, { year: 'numeric' });
  }

  /** From the locale (Monday in Italy); Sunday when the browser cannot tell. */
  getFirstDayOfWeek(): number {
    const locale = new Intl.Locale(this.locale) as Intl.Locale & {
      getWeekInfo?: () => { firstDay: number };
      weekInfo?: { firstDay: number };
    };
    const firstDay = (locale.getWeekInfo?.() ?? locale.weekInfo)?.firstDay;
    return firstDay === undefined ? 0 : firstDay % 7;
  }

  getNumDaysInMonth(date: IsoDate): number {
    const { year, month } = dateParts(date);
    return daysInMonth(year, month);
  }

  clone(date: IsoDate): IsoDate {
    return date;
  }

  /** `month` is 0–11; a day or month that does not exist is a programming error. */
  createDate(year: number, month: number, date: number): IsoDate {
    if (month < 0 || month > 11) {
      throw Error(`Invalid month index "${month}". Month index has to be between 0 and 11.`);
    }
    if (date < 1 || date > daysInMonth(year, month + 1)) {
      throw Error(`Invalid date "${date}" for month with index "${month}".`);
    }
    return isoDate(year, month + 1, date);
  }

  today(): IsoDate {
    return todayIso();
  }

  /** A date typed in the field; `null` when empty, an invalid date when it is not a day. */
  parse(value: unknown): IsoDate | null {
    if (typeof value !== 'string') {
      return null;
    }
    const text = value.trim();
    if (text === '') {
      return null;
    }
    if (isIsoDate(text)) {
      return text;
    }
    const match = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{2}|\d{4})$/.exec(text);
    if (!match) {
      return this.invalid();
    }
    const [day, month, year] = match.slice(1).map(Number);
    const date = isoDate(match[3].length === 2 ? 2000 + year : year, month, day);
    return isIsoDate(date) ? date : this.invalid();
  }

  format(date: IsoDate, displayFormat: Intl.DateTimeFormatOptions): string {
    if (!this.isValid(date)) {
      throw Error('IsoDateAdapter: Cannot format invalid date.');
    }
    return this.formatUtc(date, displayFormat);
  }

  addCalendarYears(date: IsoDate, years: number): IsoDate {
    return addMonths(date, years * 12);
  }

  addCalendarMonths(date: IsoDate, months: number): IsoDate {
    return addMonths(date, months);
  }

  addCalendarDays(date: IsoDate, days: number): IsoDate {
    return addDays(date, days);
  }

  toIso8601(date: IsoDate): string {
    return date;
  }

  /** Values from the API or from bindings: ISO dates, or `''` / `null` for no date. */
  override deserialize(value: unknown): IsoDate | null {
    if (value === '' || value === null || value === undefined) {
      return null;
    }
    return isIsoDate(value) ? value : this.invalid();
  }

  isDateInstance(obj: unknown): boolean {
    return obj === INVALID_DATE || isIsoDate(obj);
  }

  isValid(date: IsoDate): boolean {
    return isIsoDate(date);
  }

  invalid(): IsoDate {
    return INVALID_DATE;
  }

  /** ISO dates sort as text. */
  override compareDate(first: IsoDate, second: IsoDate): number {
    return first < second ? -1 : first > second ? 1 : 0;
  }

  private formatUtc(date: IsoDate, options: Intl.DateTimeFormatOptions): string {
    return formatIsoDate(date, this.locale, options);
  }
}

/** Formats of the date picker: `17/11/2026` in the field, `nov 2026` in the calendar header. */
export const ISO_DATE_FORMATS: MatDateFormats = {
  parse: { dateInput: null },
  display: {
    dateInput: { day: '2-digit', month: '2-digit', year: 'numeric' },
    monthLabel: { month: 'short' },
    monthYearLabel: { month: 'short', year: 'numeric' },
    dateA11yLabel: { day: 'numeric', month: 'long', year: 'numeric' },
    monthYearA11yLabel: { month: 'long', year: 'numeric' },
  },
};

/** Every date picker of the app works on `YYYY-MM-DD` strings (provided once, in app.config). */
export function provideIsoDateAdapter(): Provider[] {
  return [
    { provide: DateAdapter, useClass: IsoDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: ISO_DATE_FORMATS },
  ];
}

function range<T>(length: number, valueFunction: (index: number) => T): T[] {
  return Array.from({ length }, (_, index) => valueFunction(index));
}
