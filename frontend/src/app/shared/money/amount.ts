/**
 * Amounts typed and shown the Italian way: `1.234,56`.
 *
 * The model keeps amounts as `number`, like the API (JSON numbers with at most 2 decimals,
 * docs/backend/api-endpoints.md#conventions). The app never adds amounts up: totals and balances
 * come from the API, so floating point never changes a cent. Parsing goes through the text, so
 * `12,30` becomes exactly the number the API reads as `12.30`.
 */

/** Integer digits accepted: below 10^12, where every cent is still an exact `number`. */
export const MAX_INTEGER_DIGITS = 12;

export type AmountParseError = 'format' | 'decimals' | 'negative' | 'too_large';

export type AmountParseResult =
  | { readonly value: number | null; readonly error?: undefined }
  | { readonly value?: undefined; readonly error: AmountParseError };

export interface AmountParseOptions {
  /** Accept a leading minus (e.g. an opening balance below zero). Default: false. */
  readonly allowNegative?: boolean;
}

/**
 * `useGrouping: 'always'` (ES2023 Intl, in every supported browser): Italian rules alone write
 * `1234` without the dot. The TypeScript lib of the ES2022 target still types it as a boolean.
 */
const ALWAYS_GROUP = 'always' as unknown as boolean;

const GROUPED_THOUSANDS = /^\d{1,3}(\.\d{3})+$/;
const DIGITS = /^\d+$/;

/**
 * Reads an amount typed by a person. Empty text is `null` (the form decides whether it is
 * required). Accepted: `12`, `12,5`, `12,50`, `1234,56`, `1.234,56`, and `12.5` (phone keyboards
 * that only offer a dot); `1.234` is one thousand two hundred thirty-four. Never rounds: a third
 * decimal is an error, like on the API.
 */
export function parseAmount(text: string, options: AmountParseOptions = {}): AmountParseResult {
  let rest = text.replace(/[\s\u00a0\u202f€]/g, '');
  if (rest === '') {
    return { value: null };
  }

  let negative = false;
  if (/^[-−]/.test(rest)) {
    negative = true;
    rest = rest.slice(1);
  }

  const parts = /\d/.test(rest) ? splitDecimals(rest) : null;
  if (!parts) {
    return { error: 'format' };
  }
  const { integer, decimals } = parts;
  if (decimals.length > 2) {
    return { error: 'decimals' };
  }
  const digits = integer.replace(/^0+(?=\d)/, '');
  if (digits.length > MAX_INTEGER_DIGITS) {
    return { error: 'too_large' };
  }
  if (negative && !options.allowNegative) {
    return { error: 'negative' };
  }

  const value = Number(`${digits}.${decimals || '0'}`);
  return { value: negative && value !== 0 ? -value : value };
}

/** Integer part without separators, and the decimals, or null when the text is not an amount. */
function splitDecimals(text: string): { integer: string; decimals: string } | null {
  const comma = text.indexOf(',');
  if (comma >= 0) {
    if (comma !== text.lastIndexOf(',')) {
      return null;
    }
    const integer = text.slice(0, comma) || '0';
    const decimals = text.slice(comma + 1);
    if (!(DIGITS.test(decimals) || decimals === '') || !isInteger(integer)) {
      return null;
    }
    return { integer: integer.replaceAll('.', ''), decimals };
  }

  const dots = text.split('.').length - 1;
  if (dots === 1 && (!GROUPED_THOUSANDS.test(text) || /^0?\./.test(text))) {
    // A single dot not followed by exactly three digits (or after a lone zero): the decimal
    // point of a phone keyboard.
    const [integer, decimals] = text.split('.');
    if (!DIGITS.test(decimals) && decimals !== '') {
      return null;
    }
    return DIGITS.test(integer || '0') ? { integer: integer || '0', decimals } : null;
  }
  return isInteger(text) ? { integer: text.replaceAll('.', ''), decimals: '' } : null;
}

/** Digits, optionally grouped by thousands with dots (`1.234.567`). */
function isInteger(text: string): boolean {
  return DIGITS.test(text) || GROUPED_THOUSANDS.test(text);
}

/**
 * The text of an amount in an input: Italian separators, no currency, two decimals only when
 * there are cents (`12`, `12,50`, `1.234,56`).
 */
export function formatAmountInput(value: number | null, locale: string): string {
  if (value === null || !Number.isFinite(value)) {
    return '';
  }
  const fraction = Number.isInteger(value) ? 0 : 2;
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: fraction,
    maximumFractionDigits: 2,
    useGrouping: ALWAYS_GROUP,
  }).format(value);
}

export interface AmountFormatOptions {
  /** Show `+` before positive amounts (signed amounts of a list). Default: false. */
  readonly signed?: boolean;
}

/** An amount with its currency, for reading: `1.234,56 €`, `-12,00 €`, `+50,00 €`. */
export function formatAmount(
  value: number,
  currency: string,
  locale: string,
  options: AmountFormatOptions = {},
): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
    useGrouping: ALWAYS_GROUP,
    signDisplay: options.signed ? 'exceptZero' : 'auto',
  }).format(value);
}

/** The symbol of a currency in a locale (`€`, `$`, `CHF`), for the suffix of an input. */
export function currencySymbol(currency: string, locale: string): string {
  const parts = new Intl.NumberFormat(locale, { style: 'currency', currency }).formatToParts(0);
  return parts.find((part) => part.type === 'currency')?.value ?? currency;
}
