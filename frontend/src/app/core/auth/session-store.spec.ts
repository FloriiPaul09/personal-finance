import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiErrorInterceptor } from '../http/api-error-interceptor';
import { LOAD_PAGE, SessionStore } from './session-store';

const USER = { id: '0193a5c4-0000-7000-8000-000000000001', email: 'dev@example.com' };

describe('SessionStore', () => {
  let store: SessionStore;
  let backend: HttpTestingController;
  let loadPage: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    loadPage = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: LOAD_PAGE, useValue: loadPage },
      ],
    });
    store = TestBed.inject(SessionStore);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  function answerMe(status: number, body: object | null = null): void {
    backend
      .expectOne('/api/auth/me')
      .flush(body, { status, statusText: status === 200 ? 'OK' : 'Error' });
  }

  it('starts without knowing the session', () => {
    expect(store.status()).toBe('unknown');
    expect(store.user()).toBeNull();
  });

  it('knows the user after GET /api/auth/me and asks only once', async () => {
    const first = store.ensureLoaded();
    const second = store.ensureLoaded();
    answerMe(200, USER);

    expect(await first).toBe('authenticated');
    expect(await second).toBe('authenticated');
    expect(await store.ensureLoaded()).toBe('authenticated');
    expect(store.user()).toEqual(USER);
  });

  it('is anonymous on 401', async () => {
    const status = store.ensureLoaded();
    answerMe(401, { code: 'auth.required', status: 401 });

    expect(await status).toBe('anonymous');
    expect(store.user()).toBeNull();
  });

  it('is unavailable when the API cannot be reached, and asks again next time', async () => {
    const first = store.ensureLoaded();
    backend.expectOne('/api/auth/me').flush('', { status: 502, statusText: 'Bad Gateway' });
    expect(await first).toBe('unavailable');

    const second = store.ensureLoaded();
    answerMe(200, USER);
    expect(await second).toBe('authenticated');
  });

  it('signs in with the form values and keeps the user', async () => {
    const login = store.login({ email: USER.email, password: 'a-long-password', rememberMe: true });
    const request = backend.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: USER.email,
      password: 'a-long-password',
      rememberMe: true,
    });
    request.flush(USER);

    expect(await login).toEqual(USER);
    expect(store.status()).toBe('authenticated');
  });

  it('forgets the user when the session ends', async () => {
    const status = store.ensureLoaded();
    answerMe(200, USER);
    await status;

    store.markSignedOut();

    expect(store.status()).toBe('anonymous');
    expect(store.user()).toBeNull();
  });

  it('logs out on the API, then reloads the app on the login page', async () => {
    const logout = store.logout();
    backend.expectOne({ method: 'POST', url: '/api/auth/logout' }).flush(null, {
      status: 204,
      statusText: 'No Content',
    });
    await logout;

    expect(store.status()).toBe('anonymous');
    expect(loadPage).toHaveBeenCalledWith('/login');
  });

  it('still opens the login page when the session had already expired', async () => {
    const logout = store.logout();
    backend
      .expectOne('/api/auth/logout')
      .flush({ code: 'auth.required', status: 401 }, { status: 401, statusText: 'Unauthorized' });
    await logout;

    expect(loadPage).toHaveBeenCalledWith('/login');
  });

  it('stays on the page when the logout fails for another reason', async () => {
    const logout = store.logout();
    backend.expectOne('/api/auth/logout').flush('', { status: 502, statusText: 'Bad Gateway' });

    await expect(logout).rejects.toMatchObject({ code: 'server.unavailable' });
    expect(loadPage).not.toHaveBeenCalled();
  });

  it('changes the password with POST /api/auth/change-password', async () => {
    const change = store.changePassword({ currentPassword: 'old', newPassword: 'new-and-long-1' });
    const request = backend.expectOne('/api/auth/change-password');
    expect(request.request.body).toEqual({ currentPassword: 'old', newPassword: 'new-and-long-1' });
    request.flush(null, { status: 204, statusText: 'No Content' });

    await expect(change).resolves.toBeNull();
  });
});
