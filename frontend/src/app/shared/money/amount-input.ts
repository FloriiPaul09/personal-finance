import {
  booleanAttribute,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  LOCALE_ID,
  model,
  output,
  untracked,
  viewChild,
} from '@angular/core';
import { FormValueControl, transformedValue, ValidationError } from '@angular/forms/signals';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInput, MatInputModule } from '@angular/material/input';
import { AmountParseError, currencySymbol, formatAmountInput, parseAmount } from './amount';

/** Kind of the errors this input reports when the text is not an amount. */
export const AMOUNT_PARSE_ERROR_KIND = 'amount';

const PARSE_MESSAGES: Record<AmountParseError, string> = {
  format: $localize`:Amount input error:Inserisci un importo, ad esempio 12,50.`,
  decimals: $localize`:Amount input error:Al massimo due decimali.`,
  negative: $localize`:Amount input error:L'importo non può essere negativo.`,
  too_large: $localize`:Amount input error:Importo troppo grande.`,
};

/**
 * A money field: `<app-amount-input label="Importo" currency="EUR" [formField]="form.amount" />`.
 *
 * The field holds a `number | null`, like the API; the person types the Italian way (`12,50`,
 * `1.234,56`, or `12.5` from a phone keyboard with a dot). Text that is not an amount is a
 * Signal Forms parse error on the field, so the form cannot be submitted. Rules that depend on the
 * use (required, greater than zero) are validators of the form, not of this input.
 * The whole Material form field is inside: label, currency suffix, hint and the first error,
 * shown once the field is touched like every other field (docs/frontend/ui-components.md#forms).
 */
@Component({
  selector: 'app-amount-input',
  imports: [MatFormFieldModule, MatInputModule],
  templateUrl: './amount-input.html',
  styleUrl: './amount-input.scss',
})
export class AmountInput implements FormValueControl<number | null> {
  private readonly locale = inject(LOCALE_ID);

  readonly value = model<number | null>(null);
  readonly label = input.required<string>();
  readonly currency = input('EUR');
  readonly hint = input<string>();
  /** Accept amounts below zero (e.g. the opening balance of a credit card). */
  readonly allowNegative = input(false, { transform: booleanAttribute });

  // Bound by the `FormField` directive of Signal Forms.
  readonly errors = input<readonly ValidationError.WithOptionalFieldTree[]>([]);
  readonly touched = input(false);
  readonly disabled = input(false);
  readonly readonly = input(false);
  readonly required = input(false);
  readonly name = input('');
  readonly touch = output<void>();

  protected readonly raw = transformedValue(this.value, {
    parse: (text: string) => {
      const result = parseAmount(text, { allowNegative: this.allowNegative() });
      return result.error === undefined
        ? { value: result.value }
        : { error: { kind: AMOUNT_PARSE_ERROR_KIND, message: PARSE_MESSAGES[result.error] } };
    },
    format: (value) => formatAmountInput(value, this.locale),
  });

  protected readonly symbol = computed(() => currencySymbol(this.currency(), this.locale));

  /** The message under the field: the first error, once the field has been touched. */
  protected readonly error = computed(() =>
    this.touched() ? (this.errors()[0] ?? this.raw.parseErrors()[0] ?? null) : null,
  );

  /** The inner input is red exactly when a message is shown. */
  protected readonly errorStateMatcher: ErrorStateMatcher = {
    isErrorState: () => this.error() !== null,
  };

  private readonly matInput = viewChild.required(MatInput);
  private readonly inputElement = viewChild.required<ElementRef<HTMLInputElement>>('input');

  constructor() {
    effect(() => {
      this.error();
      untracked(() => this.matInput().updateErrorState());
    });
  }

  focus(options?: FocusOptions): void {
    this.inputElement().nativeElement.focus(options);
  }

  protected onInput(event: Event): void {
    this.raw.set((event.target as HTMLInputElement).value);
  }

  /** Leaving the field writes the amount the canonical way (`1234,5` → `1.234,50`). */
  protected onBlur(): void {
    if (this.raw.parseErrors().length === 0) {
      this.raw.set(formatAmountInput(this.value(), this.locale));
    }
    this.touch.emit();
  }
}
