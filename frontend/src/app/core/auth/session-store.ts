import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { computed, inject, InjectionToken, Service, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError } from '../api/api-error';
import { ChangePasswordRequest, CurrentUser, LoginRequest } from '../api/models';
import { LOGIN_PATH } from './return-url';

/** Opens a URL with a full page load (tests replace it). */
export const LOAD_PAGE = new InjectionToken<(url: string) => void>('LOAD_PAGE', {
  factory: () => {
    const document = inject(DOCUMENT);
    return (url) => document.location.assign(url);
  },
});

/**
 * - `unknown`: not asked yet.
 * - `authenticated` / `anonymous`: the API answered `GET /api/auth/me`.
 * - `unavailable`: the API could not be reached (offline, API down); asked again next time.
 */
export type SessionStatus = 'unknown' | 'authenticated' | 'anonymous' | 'unavailable';

interface SessionState {
  status: SessionStatus;
  user: CurrentUser | null;
}

/**
 * The session of the user, from the API (docs/backend/api-endpoints.md#authentication).
 * The session itself is an HttpOnly cookie the app never reads: this store only knows
 * whether there is one and who the user is.
 */
@Service()
export class SessionStore {
  private readonly http = inject(HttpClient);
  private readonly loadPage = inject(LOAD_PAGE);
  private readonly state = signal<SessionState>({ status: 'unknown', user: null });
  private loading: Promise<SessionStatus> | null = null;

  readonly status = computed(() => this.state().status);
  readonly user = computed(() => this.state().user);

  /**
   * Asks `GET /api/auth/me` once and remembers the answer. Calls made while the request runs
   * share it. After `unavailable` the next call asks again.
   */
  ensureLoaded(): Promise<SessionStatus> {
    const status = this.status();
    if (status === 'authenticated' || status === 'anonymous') {
      return Promise.resolve(status);
    }
    this.loading ??= this.load().finally(() => (this.loading = null));
    return this.loading;
  }

  async login(request: LoginRequest): Promise<CurrentUser> {
    const user = await firstValueFrom(this.http.post<CurrentUser>('/api/auth/login', request));
    this.state.set({ status: 'authenticated', user });
    return user;
  }

  /** The API said the session is over (`401`): forget the user. */
  markSignedOut(): void {
    this.state.set({ status: 'anonymous', user: null });
  }

  changePassword(request: ChangePasswordRequest): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/auth/change-password', request));
  }

  /**
   * Ends the session on the API, then reloads the app on the login page: a full reload
   * drops every store, so nothing of this user stays in memory for the next one.
   */
  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post<void>('/api/auth/logout', null));
    } catch (error) {
      // Already signed out (expired session): nothing to end on the API.
      if (!(error instanceof ApiError && error.status === 401)) {
        throw error;
      }
    }
    this.markSignedOut();
    this.loadPage(LOGIN_PATH);
  }

  private async load(): Promise<SessionStatus> {
    try {
      const user = await firstValueFrom(this.http.get<CurrentUser>('/api/auth/me'));
      this.state.set({ status: 'authenticated', user });
    } catch (error) {
      const signedOut = error instanceof ApiError && error.status === 401;
      this.state.set({ status: signedOut ? 'anonymous' : 'unavailable', user: null });
    }
    return this.status();
  }
}
