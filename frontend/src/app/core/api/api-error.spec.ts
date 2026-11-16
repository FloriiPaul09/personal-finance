import { HttpErrorResponse } from '@angular/common/http';
import { ApiError, CLIENT_ERROR_CODES, toApiError } from './api-error';

function errorResponse(status: number, error: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status, error, url: '/api/accounts' });
}

describe('toApiError', () => {
  it('reads code, detail, trace id and field errors from a ProblemDetails', () => {
    const error = toApiError(
      errorResponse(409, {
        status: 409,
        code: 'account.name_taken',
        detail: 'An account with this name exists.',
        traceId: '00-abc-01',
        errors: { name: ['Taken.'] },
      }),
    );

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(409);
    expect(error.code).toBe('account.name_taken');
    expect(error.detail).toBe('An account with this name exists.');
    expect(error.traceId).toBe('00-abc-01');
    expect(error.fieldErrors).toEqual({ name: ['Taken.'] });
    expect(error.fields).toEqual(['name']);
  });

  it('names fields like the request properties (camelCase) and merges duplicates', () => {
    const error = toApiError(
      errorResponse(400, {
        code: 'validation.failed',
        errors: { AccountId: ['Required.'], accountId: ['Not found.'], Amount: ['Too big.'] },
      }),
    );

    expect(error.fieldErrors).toEqual({
      accountId: ['Required.', 'Not found.'],
      amount: ['Too big.'],
    });
  });

  it('reads the validation answer exactly as the API sends it (field names in PascalCase)', () => {
    // Recorded from POST /api/auth/login with an invalid email and no password.
    const error = toApiError(
      errorResponse(400, {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          Email: ['The Email field is not a valid e-mail address.'],
          Password: ['The Password field is required.'],
        },
        code: 'validation.failed',
        traceId: '00-2c94db1953e1d026635a7cbcfaa55c61-490fa6f2c4f1c1be-00',
      }),
    );

    expect(error.code).toBe('validation.failed');
    expect(error.fields).toEqual(['email', 'password']);
    expect(error.traceId).toBe('00-2c94db1953e1d026635a7cbcfaa55c61-490fa6f2c4f1c1be-00');
  });

  it('reads a ProblemDetails delivered as text', () => {
    const error = toApiError(errorResponse(404, '{"code":"resource.not_found","status":404}'));

    expect(error.code).toBe('resource.not_found');
  });

  it('reports a request without answer as network.unavailable', () => {
    const error = toApiError(errorResponse(0, new ProgressEvent('error')));

    expect(error.status).toBe(0);
    expect(error.code).toBe(CLIENT_ERROR_CODES.networkUnavailable);
  });

  it.each([502, 503, 504])(
    'reports a proxy answer %s without ProblemDetails as server.unavailable (the API is down)',
    (status) => {
      // Recorded from the dev server with the API stopped: 502, text/plain, empty body.
      const error = toApiError(errorResponse(status, ''));

      expect(error.status).toBe(status);
      expect(error.code).toBe(CLIENT_ERROR_CODES.serverUnavailable);
    },
  );

  it('keeps the code of a ProblemDetails even on a proxy status', () => {
    const error = toApiError(errorResponse(503, { code: 'server.error', status: 503 }));

    expect(error.code).toBe('server.error');
  });

  it.each([
    ['an HTML page', '<html>Internal error</html>'],
    ['a body without code', { title: 'Bad Request' }],
    ['no body', null],
  ])('reports %s as response.unexpected', (_, body) => {
    const error = toApiError(errorResponse(500, body));

    expect(error.status).toBe(500);
    expect(error.code).toBe(CLIENT_ERROR_CODES.unexpectedResponse);
    expect(error.fieldErrors).toEqual({});
    expect(error.traceId).toBeNull();
  });

  it('matches one of several codes', () => {
    const error = new ApiError(409, 'category.in_use');

    expect(error.hasCode('category.in_use', 'category.has_subcategories')).toBe(true);
    expect(error.hasCode('account.archived')).toBe(false);
  });
});
