// Public API of shared/money: amounts typed and shown the Italian way.
export {
  currencySymbol,
  formatAmount,
  formatAmountInput,
  MAX_INTEGER_DIGITS,
  parseAmount,
  type AmountFormatOptions,
  type AmountParseError,
  type AmountParseOptions,
  type AmountParseResult,
} from './amount';
export {
  currencyOptions,
  isCurrencyCode,
  normalizeCurrency,
  searchCurrencies,
  type CurrencyOption,
} from './currencies';
export { AMOUNT_PARSE_ERROR_KIND, AmountInput } from './amount-input';
export { AmountPipe } from './amount.pipe';
