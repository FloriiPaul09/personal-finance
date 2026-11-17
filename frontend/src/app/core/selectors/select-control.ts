import {
  computed,
  Directive,
  effect,
  input,
  model,
  output,
  untracked,
  viewChild,
} from '@angular/core';
import { FormValueControl, ValidationError } from '@angular/forms/signals';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatSelect } from '@angular/material/select';

/**
 * What the account and category selectors share: a Signal Forms control holding the id of the
 * chosen item (`string | null`, like the API), around a Material select. Messages and the red
 * state follow the other fields: the first error, once the field has been touched
 * (docs/frontend/ui-components.md#forms).
 */
@Directive()
export abstract class SelectControl implements FormValueControl<string | null> {
  readonly value = model<string | null>(null);
  readonly label = input.required<string>();
  readonly hint = input<string>();

  // Bound by the `FormField` directive of Signal Forms.
  readonly errors = input<readonly ValidationError.WithOptionalFieldTree[]>([]);
  readonly touched = input(false);
  readonly disabled = input(false);
  readonly required = input(false);
  readonly name = input('');
  readonly touch = output<void>();

  /** The message under the field: the first error, once the field has been touched. */
  protected readonly error = computed(() => (this.touched() ? (this.errors()[0] ?? null) : null));

  /** The select is red exactly when a message is shown. */
  protected readonly errorStateMatcher: ErrorStateMatcher = {
    isErrorState: () => this.error() !== null,
  };

  private readonly select = viewChild.required(MatSelect);

  constructor() {
    effect(() => {
      this.error();
      untracked(() => this.select().updateErrorState());
    });
  }

  focus(options?: FocusOptions): void {
    this.select().focus(options);
  }

  protected choose(id: string | null): void {
    this.value.set(id);
  }

  /** Closing the list counts as leaving the field, like the blur of an input. */
  protected onOpenedChange(open: boolean): void {
    if (!open) {
      this.touch.emit();
    }
  }
}
