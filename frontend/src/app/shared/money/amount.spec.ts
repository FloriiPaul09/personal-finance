import {
  currencySymbol,
  formatAmount,
  formatAmountInput,
  MAX_INTEGER_DIGITS,
  parseAmount,
} from './amount';

describe('parseAmount', () => {
  it.each([
    ['12', 12],
    ['12,5', 12.5],
    ['12,50', 12.5],
    ['0,01', 0.01],
    [',5', 0.5],
    ['12,', 12],
    ['1234,56', 1234.56],
    ['1.234,56', 1234.56],
    ['1.234.567,89', 1234567.89],
    ['1.234', 1234],
    ['12.5', 12.5],
    ['12.50', 12.5],
    ['0.123', undefined],
    ['12.', 12],
    ['  12,50 € ', 12.5],
    ['12\u00a0345,00', 12345],
    ['007', 7],
    ['0', 0],
  ])('reads %j as %s', (text, expected) => {
    const result = parseAmount(text);

    if (expected === undefined) {
      expect(result.error).toBeDefined();
    } else {
      expect(result).toEqual({ value: expected });
    }
  });

  it('reads an empty field as no amount (the form decides whether it is required)', () => {
    expect(parseAmount('')).toEqual({ value: null });
    expect(parseAmount('   ')).toEqual({ value: null });
  });

  it.each([['12,345'], ['1.234,567'], ['12.345,678'], ['0,001']])(
    'never rounds %j: a third decimal is an error, like on the API',
    (text) => {
      expect(parseAmount(text)).toEqual({ error: 'decimals' });
    },
  );

  it.each([
    ['abc'],
    ['12a'],
    ['1,2,3'],
    ['12.34.5'],
    ['1.23,45'],
    ['12,3.4'],
    ['1e5'],
    ['+12'],
    ['-'],
    [','],
  ])('refuses %j', (text) => {
    expect(parseAmount(text)).toEqual({ error: 'format' });
  });

  it('refuses negative amounts unless asked', () => {
    expect(parseAmount('-12,50')).toEqual({ error: 'negative' });
    expect(parseAmount('-12,50', { allowNegative: true })).toEqual({ value: -12.5 });
    expect(parseAmount('−1.234', { allowNegative: true })).toEqual({ value: -1234 });
  });

  it(`refuses more than ${MAX_INTEGER_DIGITS} integer digits, where cents would stop being exact`, () => {
    expect(parseAmount('999.999.999.999,99')).toEqual({ value: 999999999999.99 });
    expect(parseAmount('1.000.000.000.000')).toEqual({ error: 'too_large' });
  });

  it('gives the number the API reads, without floating point noise', () => {
    expect(JSON.stringify({ amount: parseAmount('0,10').value })).toBe('{"amount":0.1}');
    expect(JSON.stringify({ amount: parseAmount('1.234,56').value })).toBe('{"amount":1234.56}');
    expect(JSON.stringify({ amount: parseAmount('999.999.999.999,99').value })).toBe(
      '{"amount":999999999999.99}',
    );
  });
});

describe('formatAmountInput', () => {
  it.each([
    [12, '12'],
    [12.5, '12,50'],
    [0.1, '0,10'],
    [1234.56, '1.234,56'],
    [1234, '1.234'],
    [-12.5, '-12,50'],
    [null, ''],
  ])('writes %s as %j', (value, text) => {
    expect(formatAmountInput(value, 'it')).toBe(text);
  });

  it.each([[12.5], [1234.56], [1234], [0.01], [999999999999.99]])(
    'is read back as the same amount (%s)',
    (value) => {
      expect(parseAmount(formatAmountInput(value, 'it'))).toEqual({ value });
    },
  );
});

describe('formatAmount', () => {
  // Intl puts a non-breaking space before the symbol: compare with plain spaces.
  const plain = (text: string) => text.replace(/[\u00a0\u202f]/g, ' ');

  it('shows the amount with two decimals and its currency', () => {
    expect(plain(formatAmount(1234.5, 'EUR', 'it'))).toBe('1.234,50 €');
    expect(plain(formatAmount(-12, 'EUR', 'it'))).toBe('-12,00 €');
    expect(plain(formatAmount(10, 'USD', 'it'))).toBe('10,00 USD');
  });

  it('shows the sign of a signed amount', () => {
    expect(plain(formatAmount(50, 'EUR', 'it', { signed: true }))).toBe('+50,00 €');
    expect(plain(formatAmount(-50, 'EUR', 'it', { signed: true }))).toBe('-50,00 €');
    expect(plain(formatAmount(0, 'EUR', 'it', { signed: true }))).toBe('0,00 €');
  });
});

describe('currencySymbol', () => {
  it('gives the symbol of the currency in the locale', () => {
    expect(currencySymbol('EUR', 'it')).toBe('€');
    expect(currencySymbol('CHF', 'it')).toBe('CHF');
  });
});
