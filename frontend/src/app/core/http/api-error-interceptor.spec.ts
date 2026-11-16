import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ApiError, CLIENT_ERROR_CODES } from '../api/api-error';
import { apiErrorInterceptor, isApiRequest } from './api-error-interceptor';

describe('apiErrorInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function failureOf(
    url: string,
    respond: (r: ReturnType<HttpTestingController['expectOne']>) => void,
  ) {
    const result = firstValueFrom(http.get(url)).then(
      () => null,
      (error: unknown) => error,
    );
    respond(backend.expectOne(url));
    return result;
  }

  it('turns an API error answer into an ApiError', async () => {
    const error = await failureOf('/api/accounts/1', (r) =>
      r.flush(
        { status: 404, code: 'resource.not_found', traceId: '00-1-01' },
        { status: 404, statusText: 'Not Found' },
      ),
    );

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 404, code: 'resource.not_found', traceId: '00-1-01' });
  });

  it('turns a request without answer into network.unavailable', async () => {
    const error = await failureOf('/api/dashboard', (r) => r.error(new ProgressEvent('error')));

    expect(error).toMatchObject({ status: 0, code: CLIENT_ERROR_CODES.networkUnavailable });
  });

  it('leaves requests outside the API alone', async () => {
    const error = await failureOf('icons/home.svg', (r) =>
      r.flush('missing', { status: 404, statusText: 'Not Found' }),
    );

    expect(error).toBeInstanceOf(HttpErrorResponse);
  });

  it('lets successful answers through unchanged', async () => {
    const result = firstValueFrom(http.get('/api/auth/me'));
    backend.expectOne('/api/auth/me').flush({ id: '1', email: 'me@example.com' });

    expect(await result).toEqual({ id: '1', email: 'me@example.com' });
  });
});

describe('isApiRequest', () => {
  it.each([
    ['/api', true],
    ['/api/accounts', true],
    ['/api?x=1', true],
    ['/apis/other', false],
    ['icons/home.svg', false],
    ['https://example.com/api/accounts', false],
  ])('%s → %s', (url, expected) => {
    expect(isApiRequest(url)).toBe(expected);
  });
});
