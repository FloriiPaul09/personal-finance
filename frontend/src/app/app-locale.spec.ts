import { formatDate, formatNumber } from '@angular/common';
import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';

// The locale comes from `i18n.sourceLocale` in angular.json, not from a provider:
// this test fails if that setting is lost.
describe('App locale', () => {
  it('is Italian (it) without any provider', () => {
    expect(TestBed.inject(LOCALE_ID)).toBe('it');
    expect($localize.locale).toBe('it');
  });

  it('formats numbers and dates the Italian way', () => {
    const locale = TestBed.inject(LOCALE_ID);

    expect(formatNumber(1234.5, locale, '1.2-2')).toBe('1.234,50');
    expect(formatDate('2026-11-13', 'd MMMM y', locale)).toBe('13 novembre 2026');
  });
});
