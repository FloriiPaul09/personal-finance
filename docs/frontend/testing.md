# Frontend testing

## Scope

How the Angular application is tested: unit tests with Vitest, coverage, end-to-end tests with Playwright and the phone viewport. What must be tested is fixed by [testing-strategy.md](../architecture/testing-strategy.md#minimum-required) and the Definition of Done; this document holds the frontend conventions.

## Current state

Vitest and coverage set up (blocks 0–1), HTTP layer tested (block 2), Playwright set up with the login, session and layout tests (block 3, [ADR-0023](../architecture/adr/0023-frontend-authentication.md)), shared components tested in three time zones and tried in a development-only catalog (block 4, [ADR-0024](../architecture/adr/0024-frontend-shared-components.md)), test data in `core/testing` and the first E2E flows that write data (block 5, [ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md)), the critical flows of movements and transfers with data created through the API (block 6, [ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)), budgets with a check of a color in a real browser (block 7, [ADR-0027](../architecture/adr/0027-frontend-budgets.md)), the dashboard on an account in its own currency (block 8, [ADR-0028](../architecture/adr/0028-frontend-dashboard.md)).

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
- **Error shapes**: tests use bodies recorded from the API (e.g. the `validation.failed` answer in `api-error.spec.ts`, with camelCase field names since [ADR-0029](../architecture/adr/0029-precise-openapi-contract.md)), not invented ones.
- **Snackbars and other overlays**: Material harnesses from `TestbedHarnessEnvironment.documentRootLoader(...)` (`MatSnackBarHarness`).
- **Time**: `vi.useFakeTimers()` + `vi.advanceTimersByTimeAsync` (e.g. the antiforgery timeout); always restore real timers.
- **Session**: tests of pages inside the shell provide a stub `SessionStore` (`app.spec.ts` → `startApp(status)`), otherwise the guard waits for `GET /api/auth/me`. A test that flushes a `401 auth.required` also starts the redirect to the login: give it routes, or use another error when the redirect is not what is tested.
- **Forms** (Signal Forms): fill the inputs, dispatch `input` / `blur`, submit the `<form>`; check that a message is **not** shown before the field is touched and that `mat-error` ends up in `.mat-mdc-form-field-subscript-wrapper` (block 3 regression: errors showed on load, inside the field).
- `<mat-icon svgIcon>` requests its SVG through `HttpClient`: tests that render icons provide `provideHttpClientTesting()` and leave those requests unanswered.
- **Dates**: `useTimeZone(zone)` / `setTimeZone(zone)` from `shared/testing` switch `process.env.TZ`, which Node reads again at each change; `describe.each(TIME_ZONES)` runs a test in `Pacific/Kiritimati` (UTC+14), `America/Los_Angeles` (UTC−8/−7) and `Europe/Rome`. "Today" is tested with `vi.useFakeTimers({ now })` at instants where the date differs between zones (23:30 UTC).
- **Custom form controls**: a host component with a real Signal Form and `[formField]`; type with `input.value = …` + `input` event, leave with `blur`, then check the model, the text and the message (`amount-input.spec.ts`). Material controls through their harnesses (`MatDatepickerInputHarness`, `getCalendar().selectCell(...)`).
- **Icons in component tests**: `MatIconTestingModule` (no SVG requests); read the icon name from the `MatIcon` instance (`By.directive(MatIcon)`), not from an attribute.
- **Test data** (blocks 5–6, `core/testing`): `anAccount()`, `aCategory()`, `aTransaction()`, `aTransfer()`, `aBudget()`, `aBudgetProgress()`, `someCategories()` (a parent with two subcategories, an archived one, income) shaped like the API's answers; `provideApiHttpTesting()` (HttpClient with the `ApiError` interceptor, answered by `HttpTestingController`); `problem(status, code)` for a ProblemDetails answer; `flushAccounts()` / `flushCategories()` for the lists of `core/data`.
- **Pages with lists of `core`**: render with `fixture.detectChanges()`, flush the list, then `await fixture.whenStable()`. After a write, wait a macrotask and `detectChanges()` instead of `whenStable()`: the write has the list read again, and `whenStable()` would wait for that unanswered request. A request sent after a dialog closes is awaited by polling `backend.match(url)`.
- **Router history** (`page-exit.spec.ts`): TestBed does not run the first navigation, so back and forward do nothing until `router.setUpLocationChangeListener()`; then `location.back()` really navigates (wait for `router.url` with `vi.waitFor`).
- **Root stores that read at startup** (`BudgetsStore` reads its entries when created): answer that request first, or `whenStable()` waits for it; a write that has several lists read again needs each of them answered (`backend.match(url)`).
- **Texts made of several elements**: the compiler drops the spaces between elements (the gaps come from the CSS) and `Intl` puts a no-break space before the currency; read the parts one by one and normalize spaces (`budget-progress.spec.ts`).
- **Recent choices**: `RECENT_STORAGE` replaced by an in-memory `Storage`, so tests never touch the real `localStorage`.
- **Waiting in the shell**: `whenStable()` waits for every pending request, icons included; a check on the shell after a navigation uses `vi.waitFor` instead (`app.spec.ts`, the FAB on form pages).
- **Messages**: pages and forms show snackbars through `Notifier`; a form test can replace it (`{ provide: Notifier, useValue: { show } }`), a page test reads the real snackbar with `MatSnackBarHarness` (archive, "Annulla").
- **Menus, selects, dialogs**: `MatMenuHarness.with({ selector: '[aria-label="Azioni per …"]' })`, `MatSelectHarness`, `MatAutocompleteHarness`, `MatButtonToggleGroupHarness`, `MatDialogHarness`, `MatTabNavBarHarness` from the document root loader.
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
    ├── accounts.e2e.ts    # create, edit, archive with undo, delete; form errors before the API
    ├── categories.e2e.ts  # category and subcategory on the income tab, then deleted
    ├── transactions.e2e.ts # expense from the FAB with the balance, "Annulla", text filter in the URL
    ├── transfers.e2e.ts   # transfer with both balances, leg opening the transfer, delete
    ├── budgets.e2e.ts     # budget set, expense counted (warning state and color), stop with "Annulla"
    ├── dashboard.e2e.ts   # every section of the home page on its own currency, drill-down, another month
    └── support/           # env.ts (base URL, user), pages.ts (signIn and layout checks), api.ts (TestApi)
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
- Locators by role and label, in Italian, as a user reads the page (`getByRole('button', { name: 'Accedi' })`); `data-testid` only where there is no accessible name (`account-email`) or where a role would match several buttons of a form (`save-movement`, `delete-transfer`). **Names are matched exactly** (`exact: true`) when one label can contain another ("Al conto" inside "Dal conto", "Azioni per X" inside "Azioni per X figlia").
- **Layout regressions** (`support/pages.ts`), on every page in both projects: `expectNoPageScroll` (block 1: the rail made the page 16px taller than the window; since block 5 a long page may scroll by the height of its content, never more, and never sideways), `expectClickable` on the navigation, the new expense and the account buttons (block 1: the button's touch target covered the rail), `expectTitleClearOfAccountButton` (block 3: on phones a long title ran under the account button); on phones, the error snackbar ends above the navigation bar and the new expense button (block 2).
- **Data written by tests** (block 5): each run names what it creates after the project and the time (`E2E phone 1763…`), so the phone and desktop projects never collide on a name, and deletes it at the end of the test. A run that fails halfway can leave an `E2E …` account or category behind; delete it from the app.
- **Data through the API** (block 6, `support/api.ts`): `TestApi.of(page)` calls the API with the page's session and XSRF header; tests create the accounts they need (`createAccount(name, balance)`), read balances (`account(id)`, polled with `expect.poll`) and in `afterEach` delete them with `deleteAccount(id)`, which first deletes their movements and transfers, so a failed test leaves nothing behind. Block 7 adds `createCategory(name)`, `createExpense(account, category, amount, date)` and `deleteCategory(id)`, which deletes the category's budget entries first; delete the account (its expenses) before the category. Block 8 adds a currency to `createAccount`, a kind to `createCategory`, `createIncome` and `createBudget`.
- **Totals that other data cannot change** (block 8): the dashboard test puts its movements on an account in a currency of its own (CHF on the phone project, SEK on desktop), so the totals of that currency are exactly its movements, whatever the user has and whatever other tests add at the same time.
- **Charts** are drawn only in a real browser (jsdom has no canvas: unit tests stub `getContext` and check the configuration); Playwright checks the canvas is visible and the figures beside it.
- **Colors** are checked in Playwright, not in Vitest (jsdom computes none): resolve a theme token on a probe element and compare it with the computed color (`budgets.e2e.ts`: the warning bar is not the primary blue).
- Failures keep a trace and a screenshot (`test-results/`, git-ignored); `pnpm exec playwright show-trace <file>` replays them.

### Component catalog

`/dev/components` (feature `dev`) shows every shared component with live values: amount inputs in a Signal Form with the value the API would receive, account and category selectors with the real data of the user (block 5), amounts to read in each currency, today and the month selector, empty, error and loading states, the budget progress (block 7), the bar chart with a long name and the last bar clicked (block 8). It opens with `pnpm start` after the login, outside the navigation. The production build replaces `src/app/dev-routes.ts` with `dev-routes.prod.ts` (an empty list, `fileReplacements` in `angular.json`), so its code never reaches the production bundles; a unit test checks the replacement is empty.

Use it to check a component in the built-in browser on a phone (390×844) and a desktop, in light and dark, before a page uses it; add each new shared component to it.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Vitest through the Angular builder, jsdom | Default runner of Angular 22 | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| `v8` coverage provider, no threshold | Fast, and as accurate as `istanbul` since Vitest 3.2 | [ADR-0021](../architecture/adr/0021-frontend-foundation.md), [ADR-0006](../architecture/adr/0006-testing-strategy.md) |
| Spec files next to the code | Moving or deleting code moves or deletes its tests | — |
| Playwright in `frontend/e2e`, real API, projects `setup` / `phone` / `desktop`, one login per run | Tests the real contract (cookies, antiforgery, rate limits) on the devices that matter | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |
| Dashboard E2E on an account in a currency of its own | Exact totals without a user per run | [ADR-0028](../architecture/adr/0028-frontend-dashboard.md) |

## Tests

- Block 1 (21 tests): `app.spec.ts` (routes and shell), `app-locale.spec.ts`, `core/icons/app-icons.spec.ts`, `core/layout/page-title-strategy.spec.ts`.
- Block 2 (50 tests): `core/api/api-error.spec.ts`, `error-messages.spec.ts` (every documented code), `core/http/api-error-interceptor.spec.ts`, `provide-api-http.spec.ts` (antiforgery at startup, `X-XSRF-TOKEN` on writes only and re-read at each request, startup never blocked offline or after 5 s), `cursor-pager.spec.ts`, `versions.spec.ts`, `core/ids/new-id.spec.ts` (version 7, time prefix, 2,000 ids unique and ordered), `core/errors/*.spec.ts`.
- Coverage after block 2: 77 tests, 100% of statements, functions and lines, 94% of branches.
- Contract checked against the API in Development (block 2): `XSRF-TOKEN` readable after `GET /api/auth/antiforgery`, `400 auth.antiforgery_invalid` on a write without header, ProblemDetails with `code` and `traceId` on `401` and `400`, field names in PascalCase (camelCase since [ADR-0029](../architecture/adr/0029-precise-openapi-contract.md)).
- Block 2 checked in the built-in browser on the running app (dev server + API), through the app's own `HttpClient` and `ErrorHandler` (reached with `ng.getInjector` in development mode): antiforgery at startup (`204`, cookie set); the header added to writes (a login with fake credentials gets `auth.invalid_credentials`, not `auth.antiforgery_invalid`); `ApiError` with camelCase fields; no snackbar on `auth.required`; snackbar text and "Copia riferimento" copying the exact `traceId`; with the API stopped the app still opens (antiforgery `502` in 9 ms) and the error is `server.unavailable`.
- Found and fixed while testing: on a phone the snackbar covered the navigation bar (now above the bar and the new expense button, `snack-bars.spec.ts` checks the class); with the API stopped the message said "server error" (now `server.unavailable`, recorded `502` with an empty body in `api-error.spec.ts`).
- Block 3 (60 Vitest tests): `core/auth/*.spec.ts` (safe return URL, one shared `me` request, `401` vs unreachable, login, logout with reload and an already expired session, guards, interceptor that skips `me` and `login`), `core/forms/api-form-errors.spec.ts`, `features/auth` and `features/settings` pages (validation, server errors on the right field or on the form, nothing shown before a field is touched), `core/layout/unavailable-page.spec.ts`, `app.spec.ts` with the session stub.
- Coverage after block 3: 137 tests, 99% of statements and lines, 98% of functions, 94% of branches.
- Block 3 E2E: 29 tests (setup, then 14 per project): login, session and layout ([End-to-end](#end-to-end-playwright)).
- Block 4 (159 Vitest tests): `shared/money` (parser with every accepted and refused text, JSON value without floating point noise, round trip with the formatter, the input in a Signal Form, the pipe), `shared/dates` and `shared/date-pickers` (helpers, adapter, a real date picker and the date field in three time zones, month selector), `shared/ui` and `core/feedback` states, the catalog route. Coverage after block 4: 296 tests, 99% of statements and lines, 97% of functions, 94% of branches (the catalog page is excluded).
- Block 4 E2E: on phones the account button has its own surface (`layout.e2e.ts`).
- Block 5 (84 Vitest tests more): `core/data` (writes counted by resource, lists read once and again after writes to their sources only, kept while reading, running read cancelled), selectors, confirmation, `Notifier`, currencies, stores and pages of accounts and categories. Coverage after block 5: 380 tests, 98% of lines, 98% of statements, 95% of functions and branches.
- Block 5 E2E: the first run failed twice on the tests themselves: "Azioni per <parent>" also matched the subcategory's button, whose name starts the same way (now `exact: true`), and the categories list is taller than a phone, so the page scrolls by design (the check now allows the height of the content). The data the failed runs left was deleted.
- Block 5 E2E: `accounts.e2e.ts` (create with a negative opening balance, edit from the row, archive and "Annulla", delete after the confirmation; form errors and focus before any request), `categories.e2e.ts` (income tab in the URL, subcategory from the parent's menu, parent not deletable while it has one, both deleted), and the layout checks on `/accounts/new` and `/categories/new`: 41 tests in all.
- Block 5 checked in the built-in browser on the running app, phone (390×844, light and dark) and desktop (1280×720), with real data: account created with a currency found by name ("fran" → CHF) and a negative opening balance, a name already used shown under the field, errors and focus before any request, a `412` caused by a change made meanwhile from another request (the form showed the other name and the message), archive with "Annulla", the archived toggle, an account with a movement (created for the test) showing the reason instead of "Elimina" and its currency locked; categories with the tab in the URL, a subcategory added from the parent's menu, a new kind clearing the parent, kind and parent locked with their reasons, archive of "Trasporti" with "Annulla" restoring its three subcategories one by one, delete with the confirmation; selectors on `/dev/components` with only the active account and its balance, subcategories indented, "Trasporti › Carburante" once chosen. Found and fixed: on a desktop the lists ran 1200px wide, the balance far from its name (now 720px at most). The test data was deleted afterwards.
- Block 6 (66 Vitest tests more): `core/data/recent-choices` (per user, 6 per kind, broken or missing storage), `core/layout/page-exit` (back only to a page of the app, replaced entries, back and forward), `core/selectors` (kind switch, category select with both kinds, account select with "Tutti i conti"), `movement-query` (URL to filters, invalid values ignored, groups and day labels), stores of movements and transfers, `tags-input` (Enter, comma typed or split from the text, limits, suggestions), pages of the list (filters, chips, infinite scroll, legs opening their transfer) and of the two forms (focus, recent chips, validation, `412`, "Annulla", "Salva e aggiungi un'altra", delete). Coverage after block 6: 446 tests, 97% of lines.
- Block 6 E2E: `transactions.e2e.ts` (an expense from the FAB on both projects: amount focused, category and account, the balance from the API, the list filtered by the account, delete; "Annulla" removing the new expense; a text filter written in the URL), `transfers.e2e.ts` (300 between two accounts, balances 700 and 300, the leg in the list opening the transfer, delete bringing them back to 1000 and 0), layout checks on the new forms with the FAB hidden on phone form pages and "Salva" clickable: 48 tests and 5 skipped on purpose. The first runs failed twice: "Al conto" also matched "Dal conto" (now exact names), then a real bug: the transfer form, opened as the first page and reached through the kind tab, went back to the page before the app and lost the snackbar (`PageExit` now counts the entries of the app, [ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)).
- Block 6 checked in the built-in browser on the running app, phone (light) and desktop (dark), with real data: the FAB opening the form with the amount focused, category, description and tags, "Spesa salvata." with "Annulla" back on the previous page, the recent category as a chip the next time, "Salva e aggiungi un'altra" keeping account and date, a transfer from the tab shown as two legs under "Oggi", a leg opening the transfer, the kind filter as a chip, a custom period in the URL removed with its chip, the edit page with details open. Found and fixed: on phones the FAB covered "Salva" (hidden on form pages, checked by Playwright); a comma did not split tags on a phone keyboard (split from the text, unit test); the two dates of the period ran out of the screen (stacked on phones); "Elimina" was blue (now the error color). The test data was deleted afterwards.
- Block 7 (36 Vitest tests more): `budget-progress`, the budgets store, both budget pages, disabled categories in the selector, the budget routes in `app.spec.ts` (two shell tests moved from `/budgets` to `/dashboard`, a page that loads nothing). Coverage after block 7: 482 tests, 97% of lines.
- Block 7 E2E: `budgets.e2e.ts` (on both projects: "Nuovo budget" from the month, the limit focused, the category by name, 95 € spent of 100 € through the API shows "Restano 5,00 €" in the warning state and color, the row opens the one expense of the category, "Ferma da <mese>" removes the row and "Annulla" brings it back, the history shows the one entry), layout checks on `/budgets/new`: 52 tests and 5 skipped on purpose.
- Block 7 checked in the built-in browser on the running app, phone (light) and desktop (dark), with real data: the empty page, a budget set from it, a budget and two expenses through the API (Carburante over its limit, Spesa at 92.5%), the reasons in the selector (Spesa and Carburante budgeted, Trasporti for its subcategory, Auto and Mezzi pubblici still free), "Ferma" and "Annulla" on a month's own entry, 250 € from December as a second entry, December and November each with their limit, the December entry deleted after the confirmation, the row opening the expenses of Spesa in November. Found and fixed: the warning state was computed but not visible, because the theme's tertiary role is the primary blue; an orange token was then too close to the error red on dark surfaces, so the warning is amber (`--pf-warning`, checked by Playwright). The test data was deleted afterwards.
- Block 8 (40 Vitest tests more): `core/icons/icons-in-use` (every icon named in the sources exists among the self-hosted SVGs), `bar-chart` (configuration, size, tooltip, click, scheme and motion), the dashboard store (month sent, read again after its five sources, no other month's numbers) and page (first use, every section and its empty state, top 3 budgets, spending bars and list together, a bar opening its expenses, archived part of the total, currencies apart, month in the URL, error), the canvas stubbed in the catalog route test. Coverage after block 8: 522 tests, 97% of lines.
- Block 8 E2E: `dashboard.e2e.ts` (on both projects, on an account in CHF or SEK with an income of 2.000, an expense of 95 and a budget of 10 created through the API: income, expenses and net of the currency, the budget over its limit, the bars and the category's row, the account and the total of the currency, the row opening the movements of the category in the month, the previous month and back), the dashboard in the layout checks: 54 tests and 5 skipped on purpose.
- Block 8 checked in the built-in browser on the running app, phone (light) and desktop (dark), with real data: the first-use invitation with no account; then an account in EUR and one in USD, an income, eight expenses (one in October, one in USD) and four budgets through the API: totals per currency, the three budgets closest to their limit in their three states with "Vedi tutti (4)", seven categories as bars and rows with "Mostra tutte (7)", the tooltip with the whole name and the amount, a bar opening the expenses of its category in the month, back to the home page, October with its own figures and today's balances, a future month in the URL shown as the current one, two columns on a desktop. Found and fixed: "Mostra tutte" asked for `expand_more` / `expand_less`, names of the icon font that do not exist among the SVGs of Material Symbols, so the page showed "Si è verificato un errore" (now `keyboard_arrow_down` / `keyboard_arrow_up`, and a test checks every icon named in the sources). The test data was deleted afterwards.
- Block 4 checked in the built-in browser on `/dev/components`, phone (390×844, light and dark) and desktop (1280×720), with real keystrokes: amounts with comma and with the dot of a phone keyboard, a third decimal refused, a negative only where allowed, submit blocked by a parse error and the value sent once corrected; Monday-first Italian calendar, days picked and typed reaching the form as `YYYY-MM-DD` (`2028-02-29`), reset; month selector, amounts to read, states, the softer green in dark mode. Found and fixed: a date that does not exist showed a red field without a message and the calendar spoke English (now `<app-date-input>`); the date field exported from the startup barrel raised the initial bundle to 779 kB (now `shared/date-pickers`, and the build fails above 600 kB); on phones the page scrolled under a transparent account button; long error references broke after `00-`.
- Block 3 checked in the built-in browser on the running app: form errors, wrong credentials (unknown address), login back to the requested page, settings, password change errors without changing the password, expired session simulated with a `401` back to `/login?returnUrl=%2Fsettings` and back again after the login, logout, layout on phone and desktop. Found and fixed: form errors showed on load and inside the fields (now after the field is touched, below it, with a regression test); a long page title ran under the account button on phones (now wraps, checked by Playwright); an expired session was logged as an error in the console (now silent, unit test); a Vitest test left an unhandled rejection that failed `pnpm test` intermittently (`provide-api-http.spec.ts`).

## Scalability

- **More tests**: Vitest runs files in parallel; the builder rebuilds only what changed in watch mode.
- **Browser mode**: if jsdom misses a behavior (layout, real `IntersectionObserver` for infinite scroll), Vitest browser mode with Playwright can run the same specs in Chromium.
- **CI** (`[DevOps]`): `pnpm lint`, `pnpm test:coverage` with the `lcov` report on every pull request; E2E timing decided there. On CI the config already turns on retries, `forbidOnly` and a non-interactive report; it needs PostgreSQL, .NET and Node, and `E2E_*` if the user differs.
- **More flows**: tests that write data create their own (accounts and categories named after the test, unique with `newId()`), never rely on what another test left.

## To do / open questions

- ✅ Block 3: Playwright location, devices, how the stack runs; login, session and layout tests, including the regression of the block 1 scrollbar ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)).
- ✅ Block 6: E2E of an expense on a phone, a transfer and a list filter, with their data created and deleted through the API ([ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)).
- ✅ Block 8: E2E of the dashboard on its own currency ([ADR-0028](../architecture/adr/0028-frontend-dashboard.md)).
- A way to give each run its own data (a user per run needs registration, off by default).
- ✅ Block 4: time-zone tests of the date adapter; amount input tests ([ADR-0024](../architecture/adr/0024-frontend-shared-components.md)).
- ✅ Block 5: test data in `core/testing`; E2E that create and delete their own data ([ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md)).
- ✅ `[DevOps]`: frontend jobs in CI, E2E included: lint, unit tests with coverage and the production build in the `frontend` job, Playwright in the `e2e` job on every pull request and push to `main`, with the database from `compose.yaml` and the API built first ([devops/ci-cd.md](../devops/ci-cd.md#jobs), [ADR-0030](../architecture/adr/0030-continuous-integration.md)).

---
_Last updated: 2026-11-24_
