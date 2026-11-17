/**
 * Calendar dates as the API writes them: `YYYY-MM-DD` strings (`DateOnly`), and months as
 * `YYYY-MM` (`YearMonth`). They never go through a `Date` at local midnight, so no time zone can
 * move them by a day: every computation below works on the numbers, with `Date.UTC` only where the
 * calendar is needed (days in a month, day of the week) and every format in UTC.
 */

/** A date of the calendar, `YYYY-MM-DD`. */
export type IsoDate = string;
/** A month of the calendar, `YYYY-MM`. */
export type IsoMonth = string;

const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;
const ISO_MONTH = /^(\d{4})-(\d{2})$/;

export interface DateParts {
  readonly year: number;
  /** 1–12. */
  readonly month: number;
  readonly day: number;
}

/** `YYYY-MM-DD` of a real day (no 2026-02-30). */
export function isIsoDate(value: unknown): value is IsoDate {
  if (typeof value !== 'string') {
    return false;
  }
  const match = ISO_DATE.exec(value);
  if (!match) {
    return false;
  }
  const [year, month, day] = match.slice(1).map(Number);
  return month >= 1 && month <= 12 && day >= 1 && day <= daysInMonth(year, month);
}

/** `YYYY-MM` of a real month. */
export function isIsoMonth(value: unknown): value is IsoMonth {
  if (typeof value !== 'string') {
    return false;
  }
  const match = ISO_MONTH.exec(value);
  return !!match && Number(match[2]) >= 1 && Number(match[2]) <= 12;
}

/** Year, month (1–12) and day of an ISO date. */
export function dateParts(date: IsoDate): DateParts {
  const [year, month, day] = date.split('-').map(Number);
  return { year, month, day };
}

/** The ISO date of a day; `month` is 1–12. */
export function isoDate(year: number, month: number, day: number): IsoDate {
  return `${pad(year, 4)}-${pad(month, 2)}-${pad(day, 2)}`;
}

/** Days in a month (1–12) of a year, leap years included. */
export function daysInMonth(year: number, month: number): number {
  return new Date(Date.UTC(year, month, 0)).getUTCDate();
}

/**
 * Today where the person is: the date on their clock, not in UTC. Late in the evening in Italy,
 * UTC may already be tomorrow (or, in other time zones, still yesterday).
 */
export function todayIso(now: Date = new Date()): IsoDate {
  return isoDate(now.getFullYear(), now.getMonth() + 1, now.getDate());
}

/** Moves a date by whole days (negative to go back). */
export function addDays(date: IsoDate, days: number): IsoDate {
  const { year, month, day } = dateParts(date);
  const moved = new Date(Date.UTC(year, month - 1, day + days));
  return isoDate(moved.getUTCFullYear(), moved.getUTCMonth() + 1, moved.getUTCDate());
}

/**
 * Moves a date by whole months; a day that does not exist in the target month becomes its last
 * day (31 January + 1 month = 28 or 29 February), like the date picker.
 */
export function addMonths(date: IsoDate, months: number): IsoDate {
  const { year, month, day } = dateParts(date);
  const target = shiftMonth(year, month, months);
  return isoDate(target.year, target.month, Math.min(day, daysInMonth(target.year, target.month)));
}

/** Day of the week: 0 Sunday … 6 Saturday. */
export function dayOfWeek(date: IsoDate): number {
  const { year, month, day } = dateParts(date);
  return new Date(Date.UTC(year, month - 1, day)).getUTCDay();
}

/** The month a date belongs to. */
export function monthOf(date: IsoDate): IsoMonth {
  return date.slice(0, 7);
}

/** The current month where the person is. */
export function currentMonth(now: Date = new Date()): IsoMonth {
  return monthOf(todayIso(now));
}

/** Moves a month by whole months. */
export function addMonthsToMonth(value: IsoMonth, months: number): IsoMonth {
  const [year, month] = value.split('-').map(Number);
  const target = shiftMonth(year, month, months);
  return `${pad(target.year, 4)}-${pad(target.month, 2)}`;
}

/** First and last day of a month, e.g. for a period filter. */
export function monthRange(value: IsoMonth): { from: IsoDate; to: IsoDate } {
  const [year, month] = value.split('-').map(Number);
  return { from: isoDate(year, month, 1), to: isoDate(year, month, daysInMonth(year, month)) };
}

/**
 * A date written for people: `formatIsoDate('2026-11-17', 'it')` → `17 nov 2026`.
 * `options` are those of `Intl.DateTimeFormat`; the date is formatted in UTC, where it was built.
 */
export function formatIsoDate(
  date: IsoDate,
  locale: string,
  options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' },
): string {
  return new Intl.DateTimeFormat(locale, { ...options, timeZone: 'UTC' }).format(toUtc(date));
}

/** A month written for people: `novembre 2026`. */
export function formatMonth(value: IsoMonth, locale: string): string {
  return formatIsoDate(`${value}-01`, locale, { month: 'long', year: 'numeric' });
}

/** The UTC midnight of a date: only for `Intl` formatting in UTC. */
export function toUtc(date: IsoDate): Date {
  const { year, month, day } = dateParts(date);
  const utc = new Date(0);
  // setUTCFullYear keeps years below 100 as they are (Date.UTC would read 26 as 1926).
  utc.setUTCFullYear(year, month - 1, day);
  return utc;
}

function shiftMonth(year: number, month: number, months: number): { year: number; month: number } {
  const index = year * 12 + (month - 1) + months;
  return { year: Math.floor(index / 12), month: (index % 12) + 1 };
}

function pad(value: number, length: number): string {
  return String(value).padStart(length, '0');
}
