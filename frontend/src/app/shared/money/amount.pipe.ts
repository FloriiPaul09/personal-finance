import { inject, LOCALE_ID, Pipe, PipeTransform } from '@angular/core';
import { AmountFormatOptions, formatAmount } from './amount';

/**
 * `{{ account.balance | amount: account.currency }}` → `1.234,56 €`;
 * `{{ t.signedAmount | amount: t.currency : { signed: true } }}` → `+50,00 €` / `-12,00 €`.
 * Shows the amount as the API sent it: no rounding beyond the two decimals it already has.
 */
@Pipe({ name: 'amount' })
export class AmountPipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(
    value: number | null | undefined,
    currency: string,
    options?: AmountFormatOptions,
  ): string {
    return value === null || value === undefined
      ? ''
      : formatAmount(value, currency, this.locale, options);
  }
}
