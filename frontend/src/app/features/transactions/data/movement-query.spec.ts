import { aTransaction } from '../../../core/testing';
import { dayLabel, groupByDay, movementView } from './movement-query';

describe('movementView', () => {
  it('shows the current month without a query', () => {
    expect(movementView({}, '2026-11')).toEqual({
      filters: {
        from: '2026-11-01',
        to: '2026-11-30',
        accountId: null,
        categoryId: null,
        kind: null,
        q: null,
      },
      month: '2026-11',
      customPeriod: false,
      narrowed: 0,
    });
  });

  it('reads a month, the filters and a trimmed text', () => {
    const view = movementView(
      { month: '2026-02', accountId: 'a', categoryId: 'c', kind: 'income', q: '  pizza ' },
      '2026-11',
    );

    expect(view.filters).toEqual({
      from: '2026-02-01',
      to: '2026-02-28',
      accountId: 'a',
      categoryId: 'c',
      kind: 'income',
      q: 'pizza',
    });
    expect(view.narrowed).toBe(4);
  });

  it('prefers a valid custom period to the month', () => {
    const view = movementView({ month: '2026-02', from: '2026-01-10', to: '2026-03-05' });

    expect(view.customPeriod).toBe(true);
    expect(view.filters.from).toBe('2026-01-10');
    expect(view.filters.to).toBe('2026-03-05');
  });

  it('ignores values that are not valid, so an old link still opens a list', () => {
    const view = movementView(
      { month: '2026-13', from: '2026-05-10', to: '2026-05-01', kind: 'gift', q: '  ' },
      '2026-11',
    );

    expect(view.month).toBe('2026-11');
    expect(view.customPeriod).toBe(false);
    expect(view.filters.kind).toBeNull();
    expect(view.filters.q).toBeNull();
    expect(view.narrowed).toBe(0);
  });

  it('drops the category for transfers, which have none', () => {
    expect(movementView({ kind: 'transfer', categoryId: 'c' }).filters.categoryId).toBeNull();
  });
});

describe('groupByDay', () => {
  it('groups consecutive movements of the same day, keeping the order', () => {
    const a = aTransaction({ date: '2026-11-17' });
    const b = aTransaction({ date: '2026-11-17' });
    const c = aTransaction({ date: '2026-11-15' });

    expect(groupByDay([a, b, c])).toEqual([
      { date: '2026-11-17', movements: [a, b] },
      { date: '2026-11-15', movements: [c] },
    ]);
    expect(groupByDay([])).toEqual([]);
  });
});

describe('dayLabel', () => {
  it('says today, yesterday, or the day with its weekday', () => {
    expect(dayLabel('2026-11-17', '2026-11-17', 'it')).toBe('Oggi');
    expect(dayLabel('2026-11-16', '2026-11-17', 'it')).toBe('Ieri');
    expect(dayLabel('2026-11-15', '2026-11-17', 'it')).toBe('Domenica 15 novembre');
    expect(dayLabel('2025-12-31', '2026-01-02', 'it')).toBe('Mercoledì 31 dicembre 2025');
  });

  it('counts yesterday across the first of the month', () => {
    expect(dayLabel('2026-02-28', '2026-03-01', 'it')).toBe('Ieri');
  });
});
