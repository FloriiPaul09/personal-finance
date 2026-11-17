import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, NEVER, of, throwError } from 'rxjs';
import { ApiError } from '../api/api-error';
import {
  ANTIFORGERY_TIMEOUT_MS,
  ANTIFORGERY_URL,
  provideApiHttp,
  requestAntiforgeryToken,
} from './provide-api-http';

describe('provideApiHttp', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideApiHttp(), provideHttpClientTesting()] });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
  });

  it('asks the API for the antiforgery cookie at startup', async () => {
    const status = TestBed.inject(ApplicationInitStatus);

    backend
      .expectOne({ method: 'GET', url: ANTIFORGERY_URL })
      .flush(null, { status: 204, statusText: 'No Content' });
    await status.donePromise;

    expect(status.done).toBe(true);
  });

  it('copies the XSRF-TOKEN cookie into X-XSRF-TOKEN on writes, not on reads', () => {
    backend.expectOne(ANTIFORGERY_URL).flush(null);
    document.cookie = 'XSRF-TOKEN=token-1; path=/';
    const http = TestBed.inject(HttpClient);

    http.post('/api/accounts', {}).subscribe();
    http.delete('/api/accounts/1').subscribe();
    http.get('/api/accounts').subscribe();

    expect(backend.expectOne({ method: 'POST' }).request.headers.get('X-XSRF-TOKEN')).toBe(
      'token-1',
    );
    expect(backend.expectOne({ method: 'DELETE' }).request.headers.get('X-XSRF-TOKEN')).toBe(
      'token-1',
    );
    expect(
      backend
        .expectOne({ method: 'GET', url: '/api/accounts' })
        .request.headers.has('X-XSRF-TOKEN'),
    ).toBe(false);
  });

  it('reads the cookie again at each request (the token changes at login and logout)', () => {
    backend.expectOne(ANTIFORGERY_URL).flush(null);
    const http = TestBed.inject(HttpClient);

    document.cookie = 'XSRF-TOKEN=before-login; path=/';
    http.post('/api/auth/login', {}).subscribe();
    document.cookie = 'XSRF-TOKEN=after-login; path=/';
    http.post('/api/transactions', {}).subscribe();

    expect(backend.expectOne('/api/auth/login').request.headers.get('X-XSRF-TOKEN')).toBe(
      'before-login',
    );
    expect(backend.expectOne('/api/transactions').request.headers.get('X-XSRF-TOKEN')).toBe(
      'after-login',
    );
  });

  it('turns API errors into ApiError', async () => {
    backend.expectOne(ANTIFORGERY_URL).flush(null);
    const result = firstValueFrom(TestBed.inject(HttpClient).get('/api/dashboard')).catch(
      (e: unknown) => e,
    );

    // Not a 401 auth.required: that one also sends the user to the login page
    // (session-interceptor.spec.ts), and this test has no routes.
    backend
      .expectOne('/api/dashboard')
      .flush({ code: 'account.archived' }, { status: 409, statusText: 'Conflict' });

    const error = await result;
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('account.archived');
  });
});

describe('requestAntiforgeryToken', () => {
  function httpReturning(answer: unknown): HttpClient {
    return { get: () => answer } as unknown as HttpClient;
  }

  beforeEach(() => vi.spyOn(console, 'warn').mockImplementation(() => undefined));
  afterEach(() => vi.useRealTimers());

  it('resolves true when the cookie was set', async () => {
    expect(await requestAntiforgeryToken(httpReturning(of(null)))).toBe(true);
  });

  it('never blocks the startup when the API cannot be reached (offline, API down)', async () => {
    const failing = httpReturning(throwError(() => new ApiError(0, 'network.unavailable')));

    expect(await requestAntiforgeryToken(failing)).toBe(false);
  });

  it(`gives up after ${ANTIFORGERY_TIMEOUT_MS} ms when the API does not answer`, async () => {
    vi.useFakeTimers();
    const result = requestAntiforgeryToken(httpReturning(NEVER));

    await vi.advanceTimersByTimeAsync(ANTIFORGERY_TIMEOUT_MS);

    expect(await result).toBe(false);
  });
});
