import { FieldTree } from '@angular/forms/signals';

/**
 * Moves the focus to the first field with an error: pass it as `onInvalid` of `submit()`, so
 * pressing "Salva" on a phone brings the person to what is missing instead of leaving them at
 * the bottom of the form (Signal Forms does not do it by itself).
 */
export function focusFirstError(form: FieldTree<unknown>): void {
  form().errorSummary()[0]?.fieldTree().focusBoundControl();
}
