# Frontend

## Scope

The Angular single-page application: structure of the workspace, routing, state management, UI components, tests and the way the app talks to the API.

## Micro categories

| Document | Covers | Status |
|---|---|---|
| [app-structure.md](app-structure.md) | Folders, module boundaries (Sheriff), API types and their generation, locale and i18n, scripts | Implemented — ADR-0021, ADR-0022 |
| [routing.md](routing.md) | Routes, lazy loading, shell and navigation, page titles, login, guards, expired session, logout | Implemented — ADR-0021, ADR-0023 |
| [state-management.md](state-management.md) | Stores with signals and `httpResource`, session, errors (`ApiError`), versions, cursor paging, client ids, refresh after writes | HTTP layer and session implemented — ADR-0021, ADR-0022, ADR-0023; feature stores from block 5 |
| [ui-components.md](ui-components.md) | Component library, theme and dark mode, typography, icons, forms, shared components (amounts, dates, month selector, page states), mobile | Theme, layout, forms and first shared components implemented — ADR-0004, ADR-0021, ADR-0023, ADR-0024 |
| [testing.md](testing.md) | Vitest conventions, time zones, coverage, Playwright (devices, stack, login limits), layout regressions, component catalog | Vitest, coverage, Playwright and catalog set up — ADR-0021, ADR-0023, ADR-0024 |

## Current state

**Block 1 (foundation) done** ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)): Material 3 theme with dark mode from the system, Roboto and Material Symbols self-hosted, shell with navigation bar (phone) or rail (desktop) and the "Nuova spesa" button, five lazy features with placeholder pages, Italian locale, ESLint with module boundaries, coverage.

**Block 2 (backend integration) done** ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)): API types generated from a committed snapshot of the OpenAPI document, antiforgery at startup without blocking offline, `ApiError` with Italian messages for every code, global error snackbar above the phone navigation, `server.unavailable` when the API is down, `ifMatch`, `cursorPager`, `newId()` (UUIDv7).

**Block 3 (authentication) done** ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)): login page, session asked on the first protected page with `canMatch` guards, "server unavailable" page, expired session back to the login and then to the same page, logout with a full reload, settings page (email, logout, password change) behind an account button, forms with Signal Forms and server errors on their fields, Playwright on the real stack (phone and desktop) with login, session and layout tests.

**Block 4 (shared components) done** ([ADR-0024](../architecture/adr/0024-frontend-shared-components.md)): amount input and display the Italian way with `number` values, `YYYY-MM-DD` date adapter tested in three time zones, month selector, empty / error / loading states, a softer income green, and a development-only catalog at `/dev/components`.

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
| 4 | Shared components | Amount input, date adapter, month selector, empty / error / loading states, income green, component catalog | — | Done |
| 5 | Accounts and categories | Pages and forms; archive vs delete from `hasMovements`, `isInUse`, `hasSubcategories`; reference data in `core` with account and category selectors | — | Next |
| 6 | Transactions and transfers | List with filter bar and cursor, quick entry from the phone, transfers | Expense on phone, transfer | |
| 7 | Budgets | Progress of the month; "stop from this month" vs "delete" | — | |
| 8 | Dashboard | One row per currency; chart library | Dashboard | |

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

## Tests

- Unit tests: `*.spec.ts` next to the code, run by Vitest (`pnpm test --watch=false`); 274 tests after block 4, 99% of lines covered. Conventions in [testing.md](testing.md).
- End-to-end: Playwright in `e2e/` (`pnpm e2e`) on the real stack, phone and desktop; 29 tests after block 3 ([testing.md](testing.md#end-to-end-playwright)).
- Lint (`pnpm lint`) checks the module boundaries ([app-structure.md](app-structure.md#boundaries)).
- What must be tested is listed in [testing-strategy.md](../architecture/testing-strategy.md#minimum-required).

## Scalability

The app is organized in feature areas that mirror the backend modules (accounts, categories, transactions, budgets, dashboard, see [ADR-0002](../architecture/adr/0002-backend-structure.md)): a new end-to-end feature adds a backend module and a frontend feature with the same name, and lint keeps features independent. Details in each micro category.

## To do / open questions

- ✅ Block 3: Playwright location, devices, how the full stack runs for E2E ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)).
- ✅ Block 4: date adapter and its time-zone tests; a softer income green in dark mode ([ADR-0024](../architecture/adr/0024-frontend-shared-components.md)).
- Block 5: account and category selectors with the reference data in `core`; block 6: the list filter bar.
- Block 8: chart library.
- `[DevOps]`: lint and coverage in CI; OpenAPI document exported at build time to check `openapi.json` / `schema.ts`; E2E in CI (PostgreSQL, .NET and Node, when they run).
- `[Backend]`: numbers and `YearMonth` described precisely in the OpenAPI document; field names of validation errors in camelCase (today `Email`, mapped by the app).

---
_Last updated: 2026-11-17_
