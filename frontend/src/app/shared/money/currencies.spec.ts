import { currencyOptions, isCurrencyCode, normalizeCurrency, searchCurrencies } from './currencies';

describe('currencies', () => {
  it('knows the currencies of the browser, named in Italian, the common ones first', () => {
    const options = currencyOptions('it');

    expect(options.length).toBeGreaterThan(100);
    expect(options.slice(0, 4).map((o) => o.code)).toEqual(['EUR', 'USD', 'GBP', 'CHF']);
    expect(options[0].name.toLowerCase()).toBe('euro');
    expect(new Set(options.map((o) => o.code)).size).toBe(options.length);
  });

  it('finds currencies by code or by name', () => {
    expect(searchCurrencies('ch', 'it')[0].code).toBe('CHF');
    expect(searchCurrencies('franco svizzero', 'it').map((o) => o.code)).toContain('CHF');
    expect(searchCurrencies('yen', 'it').map((o) => o.code)).toContain('JPY');
    expect(searchCurrencies('xyz-nothing', 'it')).toEqual([]);
  });

  it('offers every currency when the field is empty or holds a complete code', () => {
    expect(searchCurrencies('', 'it', 10)).toHaveLength(10);
    expect(searchCurrencies('EUR', 'it', 10).map((o) => o.code)).toEqual(
      currencyOptions('it')
        .slice(0, 10)
        .map((o) => o.code),
    );
  });

  it('accepts three letters in any case, like the API', () => {
    expect(isCurrencyCode('EUR')).toBe(true);
    expect(isCurrencyCode(' eur ')).toBe(true);
    expect(isCurrencyCode('EU')).toBe(false);
    expect(isCurrencyCode('EUR1')).toBe(false);
    expect(isCurrencyCode('€')).toBe(false);
    expect(normalizeCurrency(' eur ')).toBe('EUR');
    expect(normalizeCurrency('eu')).toBe('eu');
  });
});
