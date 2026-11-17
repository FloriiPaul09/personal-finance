/** A currency to offer: ISO 4217 code and its name in the app's language. */
export interface CurrencyOption {
  readonly code: string;
  readonly name: string;
}

/** Offered first: the currencies a person in Italy uses most. */
const COMMON = ['EUR', 'USD', 'GBP', 'CHF'];
const CODE = /^[A-Za-z]{3}$/;

/** Three letters, the only rule of the API (`currency.invalid` otherwise). */
export function isCurrencyCode(text: string): boolean {
  return CODE.test(text.trim());
}

/** The code the API stores (`eur` → `EUR`); anything else unchanged. */
export function normalizeCurrency(text: string): string {
  const trimmed = text.trim();
  return isCurrencyCode(trimmed) ? trimmed.toUpperCase() : trimmed;
}

const optionsByLocale = new Map<string, readonly CurrencyOption[]>();

/** Every currency the browser knows (about 300), the common ones first, named in `locale`. */
export function currencyOptions(locale: string): readonly CurrencyOption[] {
  let options = optionsByLocale.get(locale);
  if (!options) {
    const names = new Intl.DisplayNames([locale], { type: 'currency' });
    const codes = [
      ...COMMON,
      ...Intl.supportedValuesOf('currency').filter((c) => !COMMON.includes(c)),
    ];
    options = codes.map((code) => ({ code, name: names.of(code) ?? code }));
    optionsByLocale.set(locale, options);
  }
  return options;
}

/**
 * Currencies matching what the person typed: by code (`ch` → CHF) or by name (`franco`).
 * A complete code shows every currency, so a field already holding `EUR` still offers the others.
 */
export function searchCurrencies(query: string, locale: string, limit = 50): CurrencyOption[] {
  const options = currencyOptions(locale);
  const text = query.trim().toLowerCase();
  const exact = options.some((option) => option.code.toLowerCase() === text);
  const matches =
    text === '' || exact
      ? options
      : options.filter(
          (option) =>
            option.code.toLowerCase().startsWith(text) || option.name.toLowerCase().includes(text),
        );
  return matches.slice(0, limit);
}
