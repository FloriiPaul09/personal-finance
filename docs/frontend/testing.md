# Frontend testing

## Scope

How the Angular application is tested: unit tests with Vitest, coverage, end-to-end tests with Playwright and the phone viewport. What must be tested is fixed by [testing-strategy.md](../architecture/testing-strategy.md#minimum-required) and the Definition of Done; this document holds the frontend conventions.

## Current state

Vitest and coverage set up (blocks 0–1), HTTP layer tested (block 2), Playwright set up with the login, session and layout tests (block 3, [ADR-0023](../architecture/adr/0023-frontend-authentication.md)), shared components tested in three time zones and tried in a development-only catalog (block 4, [ADR-0024](../architecture/adr/0024-frontend-shared-components.md)).

### Unit tests (Vitest)

- Run by the Angular builder `@angular/build:unit-test` on jsdom: `pnpm test` (watch), `pnpm test --watch=false` (one run).
- Files `*.spec.ts` **next to the code** they test.
- Names describe the behavior: `it('puts the page title before the product name')`, `it.each` for the same behavior over several inputs.
- Globals (`describe`, `it`, `expect`) from `vitest/globals`.

| What | How |
|---|---|
| Pure functions (formatting, parsing, URL building) | Call them directly; `TestBed.inject` only for the Angular services they need |
| Routes and layout | `RouterTestingHarness` with the real `routes`, then check the DOM |
| Services that call the API | `provideHttpClient()` + `provideHttpClientTesting()`, `HttpTestingController` to check URL, headers (`If-Match`, `X-XSRF-TOKEN`) and body, then flush a response |
| Components with Material | Material component harnesses (`@angular/material/<component>/testing`) when the interaction matters |
| Configuration the app relies on | A test that fails if the setting is lost (e.g. `app-locale.spec.ts` for the locale) |

- **HTTP**: `provideApiHttp()` + `provideHttpClientTesting()` runs the real XSRF support and interceptors against `HttpTestingController`; flush the startup `GET /api/auth/antiforgery` first. Cookies are set with `document.cookie` and cleared after each test.
- **Error shapes**: tests use bodies recorded from the API (e.g. the `validation.failed` answer with PascalCase field names in `api-error.spec.ts`), not invented ones.
- **Snackbars and other overlays**: Material harnesses from `TestbedHarnessEnvironment.documentRootLoader(...)` (`MatSnackBarHarness`).
- **Time**: `vi.useFakeTimers()` + `vi.advanceTimersByTimeAsync` (e.g. the antiforgery timeout); always restore real timers.
- **Session**: tests of pages inside the shell provide a stub `SessionStore` (`app.spec.ts` → `startApp(status)`), otherwise the guard waits for `GET /api/auth/me`. A test that flushes a `401 auth.required` also starts the redirect to the login: give it routes, or use another error when the redirect is not what is tested.
- **Forms** (Signal Forms): fill the inputs, dispatch `input` / `blur`, submit the `<form>`; check that a message is **not** shown before the field is touched and that `mat-error` ends up in `.mat-mdc-form-field-subscript-wrapper` (block 3 regression: errors showed on load, inside the field).
- `<mat-icon svgIcon>` requests its SVG through `HttpClient`: tests that render icons provide `provideHttpClientTesting()` and leave those requests unanswered.
- **Dates**: `useTimeZone(zone)` / `setTimeZone(zone)` from `shared/testing` switch `process.env.TZ`, which Node reads again at each change; `describe.each(TIME_ZONES)` runs a test in `Pacific/Kiritimati` (UTC+14), `America/Los_Angeles` (UTC−8/−7) and `Europe/Rome`. "Today" is tested with `vi.useFakeTimers({ now })` at instants where the date differs between zones (23:30 UTC).
- **Custom form controls**: a host component with a real Signal Form and `[formField]`; type with `input.value = …` + `input` event, leave with `blur`, then check the model, the text and the message (`amount-input.spec.ts`). Material controls through their harnesses (`MatDatepickerInputHarness`, `getCalendar().selectCell(...)`).
- **Icons in component tests**: `MatIconTestingModule` (no SVG requests); read the icon name from the `MatIcon` instance (`By.directive(MatIcon)`), not from an attribute.
- **Test helpers** live in `shared/testing` (or a `testing/` folder of a module): excluded from the app build (`tsconfig.app.json`) and from coverage, included in `tsconfig.spec.json`.

### Coverage

- `pnpm test:coverage`: Vitest's `v8` provider through the builder (`coverage` option), reports `text-summary` in the terminal, `html` and `lcov` in `coverage/` (ignored by git).
- `coverageExclude`: the generated `core/api/schema.ts`, test helpers (`**/testing/**`) and the development-only catalog (`features/dev`).
- **No threshold** ([ADR-0006](../architecture/adr/0006-testing-strategy.md)): coverage points at untested areas, it is not a goal.

### End-to-end (Playwright)

In `frontend/e2e/` ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)), against the real stack: API, PostgreSQL and the dev server.

```
frontend/
├── playwright.config.ts   # projects, web servers, locale it-IT, time zone Europe/Rome
└── e2e/
    ├── auth.setup.ts      # signs in once, saves the session in e2e/.auth/user.json (git-ignored)
    ├── login.e2e.ts       # critical flow: login on a phone
    ├── session.e2e.ts     # guest redirect, logout, expired session
    ├── layout.e2e.ts      # layout regressions on every page
    └── support/           # env.ts (base URL, user), pages.ts (signIn and layout checks)
```

| Project | Device | Session |
|---|---|---|
| `setup` | Desktop Chrome | Signs in with the test user and saves the cookies |
| `phone` | Pixel 7 (412×839, touch, mobile user agent) | Reuses the saved session |
| `desktop` | Desktop Chrome at 1280×720 | Reuses the saved session |

**Running them** (from `frontend/`):

```powershell
docker compose up -d db                 # from the repository root
pnpm exec playwright install chromium   # first time only
pnpm e2e                                # pnpm e2e:report opens the last HTML report
```

- `playwright.config.ts` starts the API (`dotnet run --project ../backend/src/Api --launch-profile http`, ready on `/api/health`) and the dev server (`pnpm start`); locally an API or dev server already running is reused.
- **User**: the development initial user of `appsettings.Development.json`; `E2E_EMAIL` / `E2E_PASSWORD` change it, `E2E_BASE_URL` changes the address.
- **Rate limit and lockout**: the API allows 10 logins per minute per address and locks a user after 5 wrong passwords. So the suite signs in once (`setup`), the login tests run on the phone project only, and wrong logins use `nobody@example.invalid`, an address no user has.
- Tests that need no session use `test.use({ storageState: { cookies: [], origins: [] } })`. An expired session is simulated with `context.clearCookies({ name: 'pf_session' })`; an API that does not answer with `page.route(..., route.fulfill({ status: 502 }))`.
- Locators by role and label, in Italian, as a user reads the page (`getByRole('button', { name: 'Accedi' })`); `data-testid` only where there is no accessible name (`account-email`).
- **Layout regressions** (`support/pages.ts`), on every page in both projects: `expectNoPageScroll` (block 1: the rail made the page 16px taller than the window), `expectClickable` on the navigation, the new expense and the account buttons (block 1: the button's touch target covered the rail), `expectTitleClearOfAccountButton` (block 3: on phones a long title ran under the account button); on phones, the error snackbar ends above the navigation bar and the new expense button (block 2).
- Failures keep a trace and a screenshot (`test-results/`, git-ignored); `pnpm exec playwright show-trace <file>` replays them.

### Component catalog

`/dev/components` (feature `dev`) shows every shared component with live values: amount inputs in a Signal Form with the value the API would receive, amounts to read in each currency, today and the month selector, empty, error and loading states. It opens with `pnpm start` after the login, outside the navigation. The production build replaces `src/app/dev-routes.ts` with `dev-routes.prod.ts` (an empty list, `fileReplacements` in `angular.json`), so its code never reaches the production bundles; a unit test checks the replacement is empty.

Use it to check a component in the built-in browser on a phone (390×844) and a desktop, in light and dark, before a page uses it; add each new shared component to it.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Vitest through the Angular builder, jsdom | Default runner of Angular 22 | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| `v8` coverage provider, no threshold | Fast, and as accurate as `istanbul` since Vitest 3.2 | [ADR-0021](../architecture/adr/0021-frontend-foundation.md), [ADR-0006](../architecture/adr/0006-testing-strategy.md) |
| Spec files next to the code | Moving or deleting code moves or deletes its tests | — |
| Playwright in `frontend/e2e`, real API, projects `setup` / `phone` / `desktop`, one login per run | Tests the real contract (cookies, antiforgery, rate limits) on the devices that matter | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |

## Tests

- Block 1 (21 tests): `app.spec.ts` (routes and shell), `app-locale.spec.ts`, `core/icons/app-icons.spec.ts`, `core/layout/page-title-strategy.spec.ts`.
- Block 2 (50 tests): `core/api/api-error.spec.ts`, `error-messages.spec.ts` (every documented code), `core/http/api-error-interceptor.spec.ts`, `provide-api-http.spec.ts` (antiforgery at startup, `X-XSRF-TOKEN` on writes only and re-read at each request, startup never blocked offline or after 5 s), `cursor-pager.spec.ts`, `versions.spec.ts`, `core/ids/new-id.spec.ts` (version 7, time prefix, 2,000 ids unique and ordered), `core/errors/*.spec.ts`.
- Coverage after block 2: 77 tests, 100% of statements, functions and lines, 94% of branches.
- Contract checked against the API in Development (block 2): `XSRF-TOKEN` readable after `GET /api/auth/antiforgery`, `400 auth.antiforgery_invalid` on a write without header, ProblemDetails with `code` and `traceId` on `401` and `400`, field names in PascalCase.
- Block 2 checked in the built-in browser on the running app (dev server + API), through the app's own `HttpClient` and `ErrorHandler` (reached with `ng.getInjector` in development mode): antiforgery at startup (`204`, cookie set); the header added to writes (a login with fake credentials gets `auth.invalid_credentials`, not `auth.antiforgery_invalid`); `ApiError` with camelCase fields; no snackbar on `auth.required`; snackbar text and "Copia riferimento" copying the exact `traceId`; with the API stopped the app still opens (antiforgery `502` in 9 ms) and the error is `server.unavailable`.
- Found and fixed while testing: on a phone the snackbar covered the navigation bar (now above the bar and the new expense button, `snack-bars.spec.ts` checks the class); with the API stopped the message said "server error" (now `server.unavailable`, recorded `502` with an empty body in `api-error.spec.ts`).
- Block 3 (60 Vitest tests): `core/auth/*.spec.ts` (safe return URL, one shared `me` request, `401` vs unreachable, login, logout with reload and an already expired session, guards, interceptor that skips `me` and `login`), `core/forms/api-form-errors.spec.ts`, `features/auth` and `features/settings` pages (validation, server errors on the right field or on the form, nothing shown before a field is touched), `core/layout/unavailable-page.spec.ts`, `app.spec.ts` with the session stub.
- Coverage after block 3: 137 tests, 99% of statements and lines, 98% of functions, 94% of branches.
- Block 3 E2E: 29 tests (setup, then 14 per project): login, session and layout ([End-to-end](#end-to-end-playwright)).
- Block 4 (159 Vitest tests): `shared/money` (parser with every accepted and refused text, JSON value without floating point noise, round trip with the formatter, the input in a Signal Form, the pipe), `shared/dates` and `shared/date-pickers` (helpers, adapter, a real date picker and the date field in three time zones, month selector), `shared/ui` and `core/feedback` states, the catalog route. Coverage after block 4: 296 tests, 99% of statements and lines, 97% of functions, 94% of branches (the catalog page is excluded).
- Block 4 E2E: on phones the account button has its own surface (`layout.e2e.ts`).
- Block 3 checked in the built-in browser on the running app: form errors, wrong credentials (unknown address), login back to the requested page, settings, password change errors without changing the password, expired session simulated with a `401` back to `/login?returnUrl=%2Fsettings` and back again after the login, logout, layout on phone and desktop. Found and fixed: form errors showed on load and inside the fields (now after the field is touched, below it, with a regression test); a long page title ran under the account button on phones (now wraps, checked by Playwright); an expired session was logged as an error in the console (now silent, unit test); a Vitest test left an unhandled rejection that failed `pnpm test` intermittently (`provide-api-http.spec.ts`).

## Scalability

- **More tests**: Vitest runs files in parallel; the builder rebuilds only what changed in watch mode.
- **Browser mode**: if jsdom misses a behavior (layout, real `IntersectionObserver` for infinite scroll), Vitest browser mode with Playwright can run the same specs in Chromium.
- **CI** (`[DevOps]`): `pnpm lint`, `pnpm test:coverage` with the `lcov` report on every pull request; E2E timing decided there. On CI the config already turns on retries, `forbidOnly` and a non-interactive report; it needs PostgreSQL, .NET and Node, and `E2E_*` if the user differs.
- **More flows** (blocks 6 and 8): expense on a phone, transfer, dashboard. Tests that write data create their own (accounts and categories named after the test, unique with `newId()`), never rely on what another test left.

## To do / open questions

- ✅ Block 3: Playwright location, devices, how the stack runs; login, session and layout tests, including the regression of the block 1 scrollbar ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)).
- Blocks 6 and 8: E2E of an expense on a phone, a transfer, the dashboard; a way to give each run its own data (a user per run needs registration, off by default).
- ✅ Block 4: time-zone tests of the date adapter; amount input tests ([ADR-0024](../architecture/adr/0024-frontend-shared-components.md)).
- `[DevOps]`: frontend jobs in CI, E2E included (when they run, how the stack starts).

---
_Last updated: 2026-11-17_
