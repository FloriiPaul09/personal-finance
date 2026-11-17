import { TransactionKind, TransactionResponse } from '../../../core';
import {
  addDays,
  currentMonth,
  formatIsoDate,
  IsoDate,
  isIsoDate,
  isIsoMonth,
  IsoMonth,
  monthRange,
} from '../../../shared/dates';
import { MovementFilters } from './transactions-api';

/** The filters of the movement list as they are written in the URL (`/transactions?month=…`). */
export interface MovementQuery {
  readonly month?: string | null;
  readonly from?: string | null;
  readonly to?: string | null;
  readonly accountId?: string | null;
  readonly categoryId?: string | null;
  readonly kind?: string | null;
  readonly q?: string | null;
}

/** The query keys that narrow the list beyond its period. */
export const NARROWING_KEYS = ['accountId', 'categoryId', 'kind', 'q'] as const;

const KINDS: readonly TransactionKind[] = ['expense', 'income', 'transfer'];

export interface MovementView {
  /** What the API is asked. */
  readonly filters: MovementFilters;
  /** The month shown when the period is a month. */
  readonly month: IsoMonth;
  /** A period chosen day by day (`from` and `to` in the URL) instead of a month. */
  readonly customPeriod: boolean;
  /** Filters beyond the period, for the chips and the empty state. */
  readonly narrowed: number;
}

/**
 * Reads the URL of the list: a valid `from`–`to` pair is a custom period, otherwise `month`
 * (the current one when missing or not a month); values that are not valid are ignored, so an
 * old or edited link still opens a list.
 */
export function movementView(query: MovementQuery, today: IsoMonth = currentMonth()): MovementView {
  const month = isIsoMonth(query.month) ? query.month : today;
  const customPeriod = isIsoDate(query.from) && isIsoDate(query.to) && query.from <= query.to;
  const period = customPeriod
    ? { from: query.from as IsoDate, to: query.to as IsoDate }
    : monthRange(month);
  const kind = KINDS.find((k) => k === query.kind) ?? null;
  const q = query.q?.trim() || null;
  const filters: MovementFilters = {
    ...period,
    accountId: query.accountId || null,
    categoryId: kind === 'transfer' ? null : query.categoryId || null,
    kind,
    q,
  };
  const narrowed = [filters.accountId, filters.categoryId, filters.kind, filters.q].filter(
    (value) => value !== null,
  ).length;
  return { filters, month, customPeriod, narrowed };
}

/** Movements of one day, in the API order (newest first). */
export interface DayGroup {
  readonly date: IsoDate;
  readonly movements: readonly TransactionResponse[];
}

/** Groups consecutive movements of the same day (the API sorts by date). */
export function groupByDay(movements: readonly TransactionResponse[]): DayGroup[] {
  const groups: { date: IsoDate; movements: TransactionResponse[] }[] = [];
  for (const movement of movements) {
    const last = groups.at(-1);
    if (last?.date === movement.date) {
      last.movements.push(movement);
    } else {
      groups.push({ date: movement.date, movements: [movement] });
    }
  }
  return groups;
}

const TODAY = $localize`:Day heading of the movement list:Oggi`;
const YESTERDAY = $localize`:Day heading of the movement list:Ieri`;

/** "Oggi", "Ieri", "lunedì 16 novembre" (with the year when it is not this year). */
export function dayLabel(date: IsoDate, today: IsoDate, locale: string): string {
  if (date === today) {
    return TODAY;
  }
  if (date === addDays(today, -1)) {
    return YESTERDAY;
  }
  const sameYear = date.slice(0, 4) === today.slice(0, 4);
  const text = formatIsoDate(date, locale, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    ...(sameYear ? {} : { year: 'numeric' }),
  });
  return text.charAt(0).toLocaleUpperCase(locale) + text.slice(1);
}
