import {
  addDays,
  addMonths,
  addMonthsToMonth,
  currentMonth,
  dayOfWeek,
  formatIsoDate,
  formatMonth,
  isIsoDate,
  isIsoMonth,
  monthOf,
  monthRange,
  todayIso,
} from './iso-date';
import { setTimeZone, TIME_ZONES, useTimeZone } from '../testing';

describe('ISO dates', () => {
  it.each([
    ['2026-11-17', true],
    ['2028-02-29', true],
    ['2026-02-29', false],
    ['2026-04-31', false],
    ['2026-13-01', false],
    ['2026-00-10', false],
    ['2026-1-7', false],
    ['17/11/2026', false],
    ['2026-11-17T00:00:00Z', false],
    [20261117, false],
  ])('%j is an ISO date: %s', (value, expected) => {
    expect(isIsoDate(value)).toBe(expected);
  });

  it('recognizes ISO months', () => {
    expect(isIsoMonth('2026-11')).toBe(true);
    expect(isIsoMonth('2026-13')).toBe(false);
    expect(isIsoMonth('2026-11-01')).toBe(false);
  });

  it('gives the month of a date and the days of a month', () => {
    expect(monthOf('2026-11-17')).toBe('2026-11');
    expect(monthRange('2026-02')).toEqual({ from: '2026-02-01', to: '2026-02-28' });
    expect(monthRange('2028-02')).toEqual({ from: '2028-02-01', to: '2028-02-29' });
  });

  it('moves months, across years', () => {
    expect(addMonthsToMonth('2026-11', 2)).toBe('2027-01');
    expect(addMonthsToMonth('2026-01', -1)).toBe('2025-12');
    expect(addMonthsToMonth('2026-01', -13)).toBe('2024-12');
  });

  describe.each(TIME_ZONES)('in %s', (timeZone) => {
    useTimeZone(timeZone);

    it('moves days across months, years and daylight saving changes', () => {
      expect(addDays('2026-01-31', 1)).toBe('2026-02-01');
      expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
      expect(addDays('2026-03-28', 1)).toBe('2026-03-29');
      expect(addDays('2026-03-29', 1)).toBe('2026-03-30');
      expect(addDays('2026-10-25', 1)).toBe('2026-10-26');
      expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
    });

    it('moves months to the last day when the day does not exist', () => {
      expect(addMonths('2026-01-31', 1)).toBe('2026-02-28');
      expect(addMonths('2028-01-31', 1)).toBe('2028-02-29');
      expect(addMonths('2026-03-31', -1)).toBe('2026-02-28');
      expect(addMonths('2026-11-17', 2)).toBe('2027-01-17');
    });

    it('knows the day of the week', () => {
      expect(dayOfWeek('2026-11-17')).toBe(2);
      expect(dayOfWeek('2026-03-29')).toBe(0);
    });

    it('writes the date itself, never the day before or after', () => {
      expect(formatIsoDate('2026-11-17', 'it')).toBe('17 nov 2026');
      expect(formatIsoDate('2026-01-01', 'it')).toBe('1 gen 2026');
      expect(
        formatIsoDate('2026-12-31', 'it', { day: '2-digit', month: '2-digit', year: 'numeric' }),
      ).toBe('31/12/2026');
      expect(formatMonth('2026-11', 'it')).toBe('novembre 2026');
    });
  });

  describe('today', () => {
    afterEach(() => vi.useRealTimers());

    // 17 November 2026 at 23:30 UTC: already the 18th in Rome and Kiritimati, still the 17th in
    // Los Angeles; at 00:30 UTC the 18th is still the 17th in Los Angeles.
    it.each([
      ['Europe/Rome', '2026-11-17T23:30:00Z', '2026-11-18'],
      ['Pacific/Kiritimati', '2026-11-17T23:30:00Z', '2026-11-18'],
      ['America/Los_Angeles', '2026-11-17T23:30:00Z', '2026-11-17'],
      ['America/Los_Angeles', '2026-11-18T00:30:00Z', '2026-11-17'],
      ['Pacific/Kiritimati', '2026-11-17T09:59:00Z', '2026-11-17'],
      ['Pacific/Kiritimati', '2026-11-17T10:00:00Z', '2026-11-18'],
    ])('in %s at %s is %s, the day on the clock of the person', (timeZone, instant, expected) => {
      const restore = setTimeZone(timeZone);
      try {
        vi.useFakeTimers({ now: new Date(instant) });

        expect(todayIso()).toBe(expected);
        expect(currentMonth()).toBe(expected.slice(0, 7));
      } finally {
        restore();
      }
    });
  });
});
