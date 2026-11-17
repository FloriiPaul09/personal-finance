# Frontend

## Scope

The Angular single-page application: structure of the workspace, routing, state management, UI components, tests and the way the app talks to the API.

## Micro categories

| Document | Covers | Status |
|---|---|---|
| [app-structure.md](app-structure.md) | Folders, module boundaries (Sheriff), what stays out of the initial bundle, API types and their generation, locale and i18n, scripts | Implemented — ADR-0021, ADR-0022, ADR-0025, ADR-0026, ADR-0027 |
| [routing.md](routing.md) | Routes, lazy loading, shell and navigation, page titles, form pages and leaving them, movement, transfer and budget routes, list filters in the URL, login, guards, expired session, logout | Implemented — ADR-0021, ADR-0023, ADR-0025, ADR-0026, ADR-0027 |
| [state-management.md](state-management.md) | Stores with signals and `httpResource`, session, reference data (accounts, categories), refresh after writes (`DataChanges`), recent choices, movement list, budgets (entries and progress), errors (`ApiError`), versions, cursor paging, client ids | Implemented — ADR-0021, ADR-0022, ADR-0023, ADR-0025, ADR-0026, ADR-0027 |
| [ui-components.md](ui-components.md) | Component library, theme and dark mode, typography, icons, forms, shared components (amounts, dates, month selector, page states, selectors, kind switch, tags, budget progress, confirmation, messages), quick entry, movement list and filters, budgets, lists, archive and delete, mobile | Implemented up to block 7 — ADR-0004, ADR-0021, ADR-0023, ADR-0024, ADR-0025, ADR-0026, ADR-0027 |
| [testing.md](testing.md) | Vitest conventions, test data, time zones, coverage, Playwright (devices, stack, login limits, data created by tests), layout regressions, component catalog | Vitest, coverage, Playwright and catalog set up — ADR-0021, ADR-0023, ADR-0024, ADR-0025, ADR-0026, ADR-0027 |

## Current state

**Block 1 (foundation) done** ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)): Material 3 theme with dark mode from the system, Roboto and Material Symbols self-hosted, shell with navigation bar (phone) or rail (desktop) and the "Nuova spesa" button, five lazy features with placeholder pages, Italian locale, ESLint with module boundaries, coverage.

**Block 2 (backend integration) done** ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)): API types generated from a committed snapshot of the OpenAPI document, antiforgery at startup without blocking offline, `ApiError` with Italian messages for every code, global error snackbar above the phone navigation, `server.unavailable` when the API is down, `ifMatch`, `cursorPager`, `newId()` (UUIDv7).

**Block 3 (authentication) done** ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)): login page, session asked on the first protected page with `canMatch` guards, "server unavailable" page, expired session back to the login and then to the same page, logout with a full reload, settings page (email, logout, password change) behind an account button, forms with Signal Forms and server errors on their fields, Playwright on the real stack (phone and desktop) with login, session and layout tests.

**Block 4 (shared components) done** ([ADR-0024](../architecture/adr/0024-frontend-shared-components.md)): amount input and display the Italian way with `number` values, `YYYY-MM-DD` date adapter and date field tested in three time zones, month selector, empty / error / loading states, a softer income green, and a development-only catalog at `/dev/components`.

**Block 5 (accounts and categories) done** ([ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md)): accounts page with balances and categories page with a tab per kind, forms as pages, archive with "Annulla" and delete only when the API allows it (with the reason otherwise), one list of accounts and categories in `core` read again after writes (`DataChanges`), account and category selectors for blocks 6–8, currency suggestions, `412` showing the current data, focus on the first error; snackbar and date adapter out of the initial bundle (479 kB).

**Block 6 (transactions and transfers) done** ([ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)): quick entry as one short page with the amount focused, the 6 recent categories as chips and the last account (kept on the device per user), tabs "Uscita / Entrata / Trasferimento" with transfers as their own feature, movement list grouped by day with infinite scroll, month and filters (kind, account, category, text, custom period) in the URL with chips, delete after a confirmation, "Annulla" after saving and "Salva e aggiungi un'altra", tags with suggestions, no FAB on phone form pages, `PageExit` going back only to a page of the app; initial bundle 491.5 kB.

**Block 7 (budgets) in review** ([ADR-0027](../architecture/adr/0027-frontend-budgets.md)): budgets of a month with a progress bar in three states (normal, amber warning from 90%, red over the limit), rows opening the expenses of their category, "Ferma da <mese>" with "Annulla", a budget form per category with the month it applies from and the history of its entries (delete a wrong one after a confirmation), categories that cannot get a budget disabled with the reason, amounts in euros until budgets have a currency.

- Angular 22 (standalone components, signals, zoneless, `OnPush` by default, TypeScript strict), SCSS, no SSR.
- **pnpm 11** (`packageManager` in `package.json`), Node.js 26 LTS (`.nvmrc`).
- **Vitest** through the Angular unit-test builder (jsdom): `pnpm test`; coverage with `pnpm test:coverage`.
- **ESLint** (`angular-eslint`, template accessibility, Sheriff): `pnpm lint`.
- Dev server proxies `/api` to the API on `http://localhost:5080` (`proxy.conf.json`), so the app always calls its own origin.
- Session through an `HttpOnly` cookie on the same origin as the API: the frontend never handles tokens ([ADR-0001](../architecture/adr/0001-authentication.md)). Served by nginx in containers, which also proxies `/api`.

### Blocks

The MVP frontend is built one block at a time, one branch and one pull request each (commits `type(frontend[/module])`).

| # | Block | Content | End-to-end | Status |
|---|---|---|---|---|
| 1 | Foundation | Material and theme, structure and boundaries, shell and routing, state approach, API types, i18n, coverage | — | Done (PR #21) |
| 2 | Backend integration | Antiforgery at startup, errors by `code` with Italian messages, `ETag` / `If-Match`, `{ items }` lists and cursor paging, UUIDv7 ids, first `schema.ts` | — | Done (PR #28) |
| 3 | Authentication | Login page, guards, expired session, logout, settings, Signal Forms; Playwright setup | Login (+ session, layout) | Done (PR #29) |
| 4 | Shared components | Amount input, date adapter, month selector, empty / error / loading states, income green, component catalog | — | Done (PR #30) |
| 5 | Accounts and categories | Pages and forms; archive vs delete from `hasMovements`, `isInUse`, `hasSubcategories`; reference data in `core` with account and category selectors | Accounts, categories | Done (PR #31) |
| 6 | Transactions and transfers | List with filter bar and cursor, quick entry from the phone, transfers | Expense on phone, transfer, list filter | Done (PR #32) |
| 7 | Budgets | Progress of the month; "stop from this month" vs "delete" | Budget set, counted and stopped | In review |
| 8 | Dashboard | One row per currency; chart library | Dashboard | Next |

## Decisions

| Date | Decision | Reference |
|---|---|---|
| 2026-11-09 | Angular Material + CDK, shared wrappers for missing components, separate chart library | [ADR-0004](../architecture/adr/0004-angular-ui-library.md) |
| 2026-11-09 | pnpm 11, Node.js 26 LTS, Vitest through the Angular builder, `/api` proxy in development | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| 2026-11-13 | M3 theme `azure`, dark mode from the system, money tokens; Roboto and Material Symbols (SVG) self-hosted | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| 2026-11-13 | Folders by feature like the backend modules; `core` / `shared` / `features` boundaries checked by Sheriff through ESLint | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| 2026-11-13 | Navigation bar on phones, rail from 600px, floating "Nuova spesa" button; English lazy routes | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| 2026-11-13 | Store services with signals and `httpResource`, no state library | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| 2026-11-13 | Types generated from OpenAPI (`openapi-typescript`), services by hand | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| 2026-11-13 | `@angular/localize` with source locale `it`; `v8` coverage without threshold | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| 2026-11-16 | Antiforgery at startup (5 s at most, never blocking); `ApiError` for every failed request, Italian messages by code, global snackbar; explicit `ifMatch(version)`; `cursorPager`; `newId()` with `uuid`; types from a committed, adjusted OpenAPI snapshot | [ADR-0022](../architecture/adr/0022-frontend-api-integration.md) |
| 2026-11-17 | `SessionStore` asked on the first protected page, `canMatch` guards, `/unavailable` when the API is down; one interceptor for expired sessions; logout with a full reload; settings behind an account button; Signal Forms with `apiFormErrors()`; Playwright in `frontend/e2e` on the real stack, phone and desktop, one login per run | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |
| 2026-11-17 | Amounts as `number` with an Italian parser and `<app-amount-input>`; own `DateAdapter<string>` on `YYYY-MM-DD`; month selector; small state components; income green from a generated palette; selectors moved to block 5, filter bar to block 6; catalog of components in development only | [ADR-0024](../architecture/adr/0024-frontend-shared-components.md) |
| 2026-11-17 | Forms as pages; accounts and categories read once in `core` and read again through `DataChanges`; archive with "Annulla", delete with confirmation only when allowed; categories in a tab per kind; selectors as Material selects; currency with suggestions from `Intl`; `412` shows the current data; `Notifier` and date adapter out of the initial bundle | [ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md) |
| 2026-11-17 | Quick entry as one page with recent categories and last account on the device; kind tabs with transfers as their own feature; list by day with infinite scroll, filters in the URL as chips; delete with confirmation; "Annulla" after saving; tags as chips with suggestions; no FAB on phone form pages; `PageExit` counts the entries of the app | [ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md) |
| 2026-11-17 | Budgets of the month with the row opening its expenses; progress bar in `shared/ui` with three states and an amber `--pf-warning`; budget form per category with the month and the history; "Ferma" with "Annulla", "Elimina" of an entry with confirmation; categories that cannot get a budget disabled with the reason; amounts in euros (`BUDGET_CURRENCY`) | [ADR-0027](../architecture/adr/0027-frontend-budgets.md) |

## Tests

- Unit tests: `*.spec.ts` next to the code, run by Vitest (`pnpm test --watch=false`); 482 tests after block 7, 97% of lines covered. Conventions in [testing.md](testing.md).
- End-to-end: Playwright in `e2e/` (`pnpm e2e`) on the real stack, phone and desktop; 52 tests after block 7, 5 more skipped on purpose ([testing.md](testing.md#end-to-end-playwright)).
- Lint (`pnpm lint`) checks the module boundaries ([app-structure.md](app-structure.md#boundaries)).
- What must be tested is listed in [testing-strategy.md](../architecture/testing-strategy.md#minimum-required).

## Scalability

The app is organized in feature areas that mirror the backend modules (accounts, categories, transactions, budgets, dashboard, see [ADR-0002](../architecture/adr/0002-backend-structure.md)): a new end-to-end feature adds a backend module and a frontend feature with the same name, and lint keeps features independent. Details in each micro category.

## To do / open questions

- ✅ Block 3: Playwright location, devices, how the full stack runs for E2E ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)).
- ✅ Block 4: date adapter and its time-zone tests; a softer income green in dark mode ([ADR-0024](../architecture/adr/0024-frontend-shared-components.md)).
- ✅ Block 5: account and category selectors with the reference data in `core` ([ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md)).
- ✅ Block 6: the list filters (a panel with chips, in the URL); refresh of accounts and categories after movements (`DataChanges.notify('transactions')`) ([ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)).
- ✅ Block 7: budget page and form, "stop from this month" vs "delete" ([ADR-0027](../architecture/adr/0027-frontend-budgets.md)).
- Budgets in another currency: `[Backend]` gives budgets a currency, then `BUDGET_CURRENCY` goes away.
- Recent choices are per device; kept by the API if people use more devices.
- Block 8: chart library.
- `[DevOps]`: lint and coverage in CI; OpenAPI document exported at build time to check `openapi.json` / `schema.ts`; E2E in CI (PostgreSQL, .NET and Node, when they run).
- `[Backend]`: numbers and `YearMonth` described precisely in the OpenAPI document; field names of validation errors in camelCase (today `Email`, mapped by the app).

---
_Last updated: 2026-11-17_
