import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SessionStore } from '../auth/session-store';
import { RECENT_CATEGORIES, RECENT_STORAGE, RecentChoices } from './recent-choices';

class MemoryStorage implements Pick<Storage, 'getItem' | 'setItem'> {
  readonly data = new Map<string, string>();
  getItem(key: string): string | null {
    return this.data.get(key) ?? null;
  }
  setItem(key: string, value: string): void {
    this.data.set(key, value);
  }
}

describe('RecentChoices', () => {
  let storage: MemoryStorage | null;
  const user = signal<{ id: string; email: string } | null>({ id: 'u1', email: 'a@b.c' });

  function create(): RecentChoices {
    TestBed.configureTestingModule({
      providers: [
        { provide: RECENT_STORAGE, useFactory: () => storage },
        { provide: SessionStore, useValue: { user } },
      ],
    });
    return TestBed.inject(RecentChoices);
  }

  beforeEach(() => {
    storage = new MemoryStorage();
    user.set({ id: 'u1', email: 'a@b.c' });
  });

  it('starts from nothing', () => {
    const recent = create();

    expect(recent.categories('expense')).toEqual([]);
    expect(recent.account()).toBeNull();
  });

  it('keeps the last categories of each kind, most recent first, without repeats', () => {
    const recent = create();

    recent.remember({ kind: 'expense', categoryId: 'a', accountId: 'x' });
    recent.remember({ kind: 'expense', categoryId: 'b', accountId: 'x' });
    recent.remember({ kind: 'expense', categoryId: 'a', accountId: 'y' });
    recent.remember({ kind: 'income', categoryId: 'c', accountId: 'y' });

    expect(recent.categories('expense')).toEqual(['a', 'b']);
    expect(recent.categories('income')).toEqual(['c']);
    expect(recent.account()).toBe('y');
  });

  it(`keeps at most ${RECENT_CATEGORIES} categories`, () => {
    const recent = create();
    for (const id of ['1', '2', '3', '4', '5', '6', '7']) {
      recent.remember({ kind: 'expense', categoryId: id, accountId: 'x' });
    }

    expect(recent.categories('expense')).toEqual(['7', '6', '5', '4', '3', '2']);
  });

  it('remembers only the account for a transfer', () => {
    const recent = create();

    recent.remember({ accountId: 'x' });

    expect(recent.account()).toBe('x');
    expect(recent.categories('expense')).toEqual([]);
  });

  it('keeps the choices of each user apart, across visits', () => {
    create().remember({ kind: 'expense', categoryId: 'a', accountId: 'x' });
    expect(storage!.data.has('pf:u1:recent')).toBe(true);

    TestBed.resetTestingModule();
    user.set({ id: 'u2', email: 'b@b.c' });
    const other = create();
    expect(other.categories('expense')).toEqual([]);

    user.set({ id: 'u1', email: 'a@b.c' });
    expect(other.categories('expense')).toEqual(['a']);
  });

  it('ignores what it cannot read and works without storage', () => {
    storage!.setItem('pf:u1:recent', '{not json');
    expect(create().account()).toBeNull();

    TestBed.resetTestingModule();
    storage = null;
    const recent = create();
    recent.remember({ kind: 'expense', categoryId: 'a', accountId: 'x' });
    expect(recent.categories('expense')).toEqual(['a']);
  });

  it('forgets nothing and saves nothing without a user', () => {
    user.set(null);
    const recent = create();

    recent.remember({ kind: 'expense', categoryId: 'a', accountId: 'x' });

    expect(storage!.data.size).toBe(0);
  });
});
