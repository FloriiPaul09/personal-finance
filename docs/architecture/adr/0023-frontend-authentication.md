# ADR-0023: Frontend authentication — session, guards, expired sessions, forms and end-to-end tests

- **Status**: Accepted
- **Date**: 2026-11-17
- **Deciders**: Paul

## Context

- The API keeps the session in an `HttpOnly` cookie (`pf_session`) the app cannot read; `GET /api/auth/me` answers the current user or `401` ([ADR-0014](0014-authentication-endpoints-and-sessions.md)). Logins are rate limited (10 per minute per address) and 5 wrong passwords lock the user for 15 minutes.
- Every error is an `ApiError` with a stable `code`; `auth.required` is left silent by the global handler for this block ([ADR-0022](0022-frontend-api-integration.md)).
- The roadmap adds a PWA that opens offline, multi-user and other languages: the session must never leave data of one user in memory for the next, and startup must not depend on the network.
- Angular 22 ships Signal Forms (`@angular/forms/signals`) as stable, integrated with Material form fields.
- The Definition of Done asks for a Playwright test of the login on a phone; block 1 left a layout regression (page taller than the window on the rail) that only a real browser can catch.

## Decision

1. **Session store in `core`**: `SessionStore` (`status`: `unknown` / `authenticated` / `anonymous` / `unavailable`, `user`) asks `GET /api/auth/me` once, when the first protected page is opened, not in an app initializer; calls made meanwhile share the request. `401` means anonymous; no answer (offline, API down) means unavailable and is asked again at the next navigation.
2. **`canMatch` guards**: `authGuard` on the shell route sends a signed-out user to `/login?returnUrl=<page>` and, when the API cannot be reached, to `/unavailable` (message and "Riprova"); `guestGuard` sends a signed-in user away from `/login`. `safeReturnUrl()` accepts only paths of the app (no absolute URLs, `//`, backslashes or the login page itself). `canMatch` does not load the code of a page the user may not open.
3. **Expired session in one interceptor**: a `401 auth.required` on any request except `/api/auth/me` and `/api/auth/login` marks the session signed out and opens `/login?returnUrl=<current page>`; the error still reaches the caller, and the global handler neither shows nor logs it.
4. **Logout reloads the app**: `POST /api/auth/logout` (an already expired session is fine), then a full page load of `/login`. Every store is a root singleton, so the reload is what guarantees that no data of this user stays in memory.
5. **Forms with Signal Forms and Material**: client validators carry the Italian message; messages show once the field is touched, which also happens on submit. `apiFormErrors(form, error, options)` turns an `ApiError` into errors on the fields the API named (or that a code points to, e.g. `auth.password_incorrect` → `currentPassword`) or on the whole form (`auth.invalid_credentials`, `request.too_many`); anything else goes to the global handler. It lives in `core/forms`, a barrel of its own, so pages without forms do not load `@angular/forms` (the initial bundle stays under its 500 kB budget).
6. **Settings page from an account button**: not a sixth navigation destination; a button at the top right on phones and at the bottom of the rail opens `/settings` (email, "Esci", password change with the API's rules: 12 characters at least, confirmation checked on the client).
7. **Playwright in `frontend/e2e`**, against the real stack: the config starts the API (`dotnet run`) and the dev server unless they are already running; PostgreSQL comes from `docker compose up -d db`. Projects `setup` (signs in once and saves the session), `phone` (Pixel 7) and `desktop` (1280×720). The user is the development initial user (`E2E_EMAIL` / `E2E_PASSWORD` override it); wrong logins use an address no user has, so the real user is never locked, and the login flow runs on the phone only. Layout regressions run on every page in both projects: no page scroll, every navigation button reachable, page title clear of the account button, error snackbar above the phone navigation.

## Consequences

**Positive**
- Startup never waits for `me`: the login page and the "unavailable" page open without it, and the PWA can later answer `unavailable` from its cache instead of a network call.
- One place decides what an ended session means; pages only handle their own codes.
- Logout is correct for multi-user by construction: no store has to remember to clear itself.
- Signal Forms keep forms on signals like the rest of the app; server errors and client errors are displayed the same way.
- The E2E tests run the real contract (cookies, antiforgery, rate limits), with one login per run for the whole suite.

**Negative**
- The first protected page waits for `me` once per load.
- A full reload on logout costs a second or two.
- Signal Forms are new: fewer examples, and the Material integration is recent.
- E2E tests need .NET, PostgreSQL and the dev server; in CI they need the whole stack (`[DevOps]`).

## Alternatives considered

**`me` in an app initializer** — Rejected: every page, the login included, would wait for the network, and offline startup would have to special-case it.

**`canActivate` guards, or a check inside the shell** — Rejected: the lazy code would be downloaded for a user who cannot see it, and the shell would show for an instant before the redirect.

**Logout without reload, each store clearing itself** — Rejected: one forgotten store leaks data to the next user.

**Reactive Forms** — Rejected: an observable-based API next to a signal-based app, and more code to keep error display and server errors in step.

**Settings as a sixth destination** — Rejected: the phone navigation holds five, and account settings are rare.

**E2E in a top-level folder, or against a mocked API** — Rejected: a top-level package duplicates dependencies and configuration; a mocked API would not test cookies, antiforgery and rate limits, the parts most likely to break.

**Signing in in every test** — Rejected: the API's rate limit would fail the suite.
