import { ApiError, CLIENT_ERROR_CODES } from './api-error';
import { errorMessage, KNOWN_ERROR_CODES, messageOf } from './error-messages';

// Every code of docs/backend/validation-and-errors.md#codes that can reach the app.
// The 500 domain codes (programming errors) reach it as `server.error`.
const DOCUMENTED_CODES = [
  'validation.failed',
  'request.invalid',
  'id.invalid',
  'account.not_found',
  'category.not_found',
  'concurrency.version_invalid',
  'auth.antiforgery_invalid',
  'auth.password_incorrect',
  'auth.password_rejected',
  'auth.required',
  'auth.invalid_credentials',
  'auth.forbidden',
  'auth.email_taken',
  'resource.conflict',
  'resource.not_found',
  'request.method_not_allowed',
  'account.name_taken',
  'category.name_taken',
  'tag.name_taken',
  'budget.month_taken',
  'concurrency.version_mismatch',
  'request.unsupported_media_type',
  'concurrency.version_required',
  'request.too_many',
  'server.error',
  // Domain, 400
  'amount.not_positive',
  'amount.too_many_decimals',
  'amount.out_of_range',
  'name.required',
  'name.too_long',
  'description.too_long',
  'currency.invalid',
  'month.invalid',
  'account.type_invalid',
  'category.kind_invalid',
  'category.too_deep',
  'category.parent_itself',
  'category.kind_mismatch',
  'category.kind_follows_parent',
  'category.not_a_subcategory',
  'transfer.same_account',
  'transfer.currency_mismatch',
  'budget.category_not_expense',
  // Domain, 409
  'account.archived',
  'account.currency_locked',
  'account.has_movements',
  'category.archived',
  'category.has_subcategories',
  'category.kind_locked',
  'category.in_use',
  'transaction.is_transfer_leg',
  'budget.branch_conflict',
];

const GENERIC = errorMessage('unknown.code');

describe('error messages', () => {
  it('has an Italian message for every documented API code and every app code', () => {
    const expected = [...DOCUMENTED_CODES, ...Object.values(CLIENT_ERROR_CODES)];

    expect([...KNOWN_ERROR_CODES].sort()).toEqual([...expected].sort());
    for (const code of expected) {
      expect(errorMessage(code), code).not.toBe(GENERIC);
    }
  });

  it('falls back to a generic message for a code the app does not know yet', () => {
    expect(GENERIC).toBe('Si è verificato un errore. Riprova.');
  });

  it('gives the message of an ApiError by its code', () => {
    expect(messageOf(new ApiError(409, 'account.name_taken'))).toBe(
      'Esiste già un conto con questo nome.',
    );
    expect(messageOf(new ApiError(412, 'concurrency.version_mismatch'))).toContain(
      'modificato nel frattempo',
    );
  });

  it('gives the generic message for errors that are not API errors', () => {
    expect(messageOf(new TypeError('x is undefined'))).toBe(GENERIC);
  });
});
