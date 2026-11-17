import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentProviders, Provider } from '@angular/core';
import { apiErrorInterceptor } from '..';

/** HttpClient answered by HttpTestingController, with API errors turned into `ApiError`. */
export function provideApiHttpTesting(): (Provider | EnvironmentProviders)[] {
  return [provideHttpClient(withInterceptors([apiErrorInterceptor])), provideHttpClientTesting()];
}

/** A ProblemDetails answer of the API, for `TestRequest.flush(...problem(409, 'x.y'))`. */
export function problem(
  status: number,
  code: string,
  errors?: Record<string, string[]>,
): [object, { status: number; statusText: string }] {
  return [
    { status, code, ...(errors ? { errors } : {}) },
    { status, statusText: 'Error' },
  ];
}
