import { computed } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DataChanges } from './data-changes';

describe('DataChanges', () => {
  let changes: DataChanges;

  beforeEach(() => (changes = TestBed.inject(DataChanges)));

  it('counts the writes to each resource', () => {
    expect(changes.version('accounts')).toBe(0);

    changes.notify('accounts');
    changes.notify('accounts', 'transactions');

    expect(changes.version('accounts')).toBe(2);
    expect(changes.version('transactions')).toBe(1);
    expect(changes.version('categories')).toBe(0);
  });

  it('changes a reader that depends on one of the resources written, and no other', () => {
    const balances = computed(() => changes.version('accounts', 'transactions', 'transfers'));
    const categories = computed(() => changes.version('categories'));
    const before = { balances: balances(), categories: categories() };

    changes.notify('transfers');

    expect(balances()).not.toBe(before.balances);
    expect(categories()).toBe(before.categories);
  });
});
