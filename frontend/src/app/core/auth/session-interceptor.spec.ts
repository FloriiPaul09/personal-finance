import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { firstValueFrom } from 'rxjs';
import { ApiError } from '../api/api-error';
import { apiErrorInterceptor } from '../http/api-error-interceptor';
import { sessionInterceptor } from './session-interceptor';
import { SessionStore } from './session-store';

@Component({ template: '' })
class Blank {}

describe('sessionInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let router: Router;
  let markSignedOut: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    markSignedOut = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: Blank },
          { path: '**', component: Blank },
        ]),
        provideHttpClient(withInterceptors([sessionInterceptor, apiErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: SessionStore, useValue: { markSignedOut } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/transactions?kind=expense');
  });

  async function failWith401(url: string): Promise<unknown> {
    const result = firstValueFrom(http.get(url)).catch((error: unknown) => error);
    backend
      .expectOne(url)
      .flush({ code: 'auth.required', status: 401 }, { status: 401, statusText: 'Unauthorized' });
    const error = await result;
    await new Promise((resolve) => setTimeout(resolve));
    return error;
  }

  it('ends the session and opens the login page, which comes back here afterwards', async () => {
    const error = await failWith401('/api/accounts');

    expect(markSignedOut).toHaveBeenCalled();
    expect(router.url).toBe('/login?returnUrl=%2Ftransactions%3Fkind%3Dexpense');
    expect(error).toBeInstanceOf(ApiError);
  });

  it.each(['/api/auth/me', '/api/auth/login'])('treats a 401 of %s as an answer', async (url) => {
    await failWith401(url);

    expect(markSignedOut).not.toHaveBeenCalled();
    expect(router.url).toBe('/transactions?kind=expense');
  });

  it('does not open the login page again from the login page', async () => {
    await router.navigateByUrl('/login?returnUrl=%2Faccounts');

    await failWith401('/api/accounts');

    expect(router.url).toBe('/login?returnUrl=%2Faccounts');
  });

  it('lets other errors through untouched', async () => {
    const result = firstValueFrom(http.get('/api/accounts')).catch((error: unknown) => error);
    backend
      .expectOne('/api/accounts')
      .flush({ code: 'resource.not_found', status: 404 }, { status: 404, statusText: 'Not Found' });

    expect(await result).toMatchObject({ code: 'resource.not_found' });
    expect(markSignedOut).not.toHaveBeenCalled();
  });
});
