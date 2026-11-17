import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form } from '@angular/forms/signals';
import { ApiError } from '..';
import { apiFormErrors, SERVER_ERROR_KIND } from './api-form-errors';

describe('apiFormErrors', () => {
  function accountForm() {
    return TestBed.runInInjectionContext(() =>
      form(signal({ name: '', currency: 'EUR', initialBalance: 0 })),
    );
  }

  it('puts the Italian message of the code on the field the API named', () => {
    const f = accountForm();
    const errors = apiFormErrors(f, new ApiError(409, 'account.name_taken', { name: ['Taken.'] }));

    expect(errors).toEqual([
      {
        kind: SERVER_ERROR_KIND,
        fieldTree: f.name,
        message: 'Esiste già un conto con questo nome.',
      },
    ]);
  });

  it('uses a generic Italian message for validation errors (the API messages are English)', () => {
    const f = accountForm();
    const errors = apiFormErrors(
      f,
      new ApiError(400, 'validation.failed', {
        name: ['The Name field is required.'],
        currency: ['Too long.'],
      }),
    );

    expect(errors?.map((e) => [e.fieldTree, e.message])).toEqual([
      [f.name, 'Valore non valido.'],
      [f.currency, 'Valore non valido.'],
    ]);
  });

  it('places codes without fields where the caller says', () => {
    const f = accountForm();
    const errors = apiFormErrors(f, new ApiError(409, 'account.currency_locked'), {
      fieldsByCode: { 'account.currency_locked': 'currency' },
    });

    expect(errors?.[0].fieldTree).toBe(f.currency);
  });

  it('puts form codes on the whole form', () => {
    const f = accountForm();
    const errors = apiFormErrors(f, new ApiError(401, 'auth.invalid_credentials'), {
      formCodes: ['auth.invalid_credentials'],
    });

    expect(errors).toEqual([
      { kind: SERVER_ERROR_KIND, fieldTree: f, message: 'Email o password non corretti.' },
    ]);
  });

  it.each([
    ['an error the form cannot show', new ApiError(502, 'server.unavailable')],
    ['a field this form does not have', new ApiError(400, 'validation.failed', { other: ['x'] })],
    ['an error that is not from the API', new TypeError('boom')],
  ])('returns null for %s, so the caller rethrows it', (_, error) => {
    expect(apiFormErrors(accountForm(), error)).toBeNull();
  });
});
