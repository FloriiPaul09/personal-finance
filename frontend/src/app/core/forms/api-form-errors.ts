import { FieldTree, isFieldTree, ValidationError } from '@angular/forms/signals';
import { ApiError, errorMessage } from '..';

/** Kind of the errors that come from the API, next to the client validators' kinds. */
export const SERVER_ERROR_KIND = 'server';

const INVALID_VALUE = $localize`:Form field error from the API:Valore non valido.`;

export interface ApiFormErrorOptions {
  /** Codes shown on the whole form instead of a field (e.g. `auth.invalid_credentials`). */
  readonly formCodes?: readonly string[];
  /** Field for codes the API does not attach to a field, e.g. `{ 'account.name_taken': 'name' }`. */
  readonly fieldsByCode?: Readonly<Record<string, string>>;
}

/**
 * Turns an API error into Signal Forms errors for `submit()`: on the fields the API named in
 * `errors`, or on the whole form for `formCodes`. Messages are the Italian ones of the code
 * (`validation.failed` gets a generic message per field: the API's own messages are English).
 * Returns `null` when the error belongs to no field of this form: the caller rethrows it and
 * the global handler shows it.
 */
export function apiFormErrors<TModel>(
  form: FieldTree<TModel>,
  error: unknown,
  options: ApiFormErrorOptions = {},
): ValidationError.WithFieldTree[] | null {
  if (!(error instanceof ApiError)) {
    return null;
  }
  const message = error.code === 'validation.failed' ? INVALID_VALUE : errorMessage(error.code);

  if (options.formCodes?.includes(error.code)) {
    return [{ kind: SERVER_ERROR_KIND, fieldTree: form, message: errorMessage(error.code) }];
  }

  const named = error.fields.length > 0 ? error.fields : [options.fieldsByCode?.[error.code]];
  const errors = named
    .map((name) => (name ? fieldOf(form, name) : undefined))
    .filter((field): field is FieldTree<unknown> => field !== undefined)
    .map((fieldTree) => ({ kind: SERVER_ERROR_KIND, fieldTree, message }));

  return errors.length > 0 ? errors : null;
}

function fieldOf(form: FieldTree<unknown>, name: string): FieldTree<unknown> | undefined {
  const field = (form as unknown as Record<string, unknown>)[name];
  return isFieldTree(field) ? (field as FieldTree<unknown>) : undefined;
}
