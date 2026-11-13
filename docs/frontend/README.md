# Frontend

## Scope

The Angular single-page application: structure of the workspace, routing, state management, UI components, tests and the way the app talks to the API.

## Micro categories

| Document | Covers | Status |
|---|---|---|
| [app-structure.md](app-structure.md) | Folders, module boundaries (Sheriff), API types, locale and i18n, scripts | Implemented — ADR-0021 |
| [routing.md](routing.md) | Routes, lazy loading, shell and navigation, page titles; login, guard and `401` from block 3 | Implemented (block 1) — ADR-0021 |
| [state-management.md](state-management.md) | Stores with signals and `httpResource`, refresh after writes, paging | Decided — ADR-0021; first stores from block 2 |
| [ui-components.md](ui-components.md) | Component library, theme and dark mode, typography, icons, shared components, dates and amounts, mobile | Theme and layout implemented — ADR-0004, ADR-0021 |
| [testing.md](testing.md) | Vitest conventions, coverage, Playwright, phone viewport | Vitest and coverage set up; Playwright from block 3 |

## Current state

**Block 1 (foundation) done** ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)): Material 3 theme with dark mode from the system, Roboto and Material Symbols self-hosted, shell with navigation bar (phone) or rail (desktop) and the "Nuova spesa" button, five lazy features with placeholder pages, Italian locale, ESLint with module boundaries, coverage.

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
| 1 | Foundation | Material and theme, structure and boundaries, shell and routing, state approach, API types, i18n, coverage | — | Done |
| 2 | Backend integration | Antiforgery at startup, errors by `code` with Italian messages, `ETag` / `If-Match`, `{ items }` lists and cursor paging, UUIDv7 ids, first `schema.ts` | — | Next |
| 3 | Authentication | Login page, guard, `401` interceptor, logout; Playwright setup | Login | |
| 4 | Shared components | Amount input, date adapter, filter bar, empty and error states, selectors | — | |
| 5 | Accounts and categories | Pages and forms; archive vs delete from `hasMovements`, `isInUse`, `hasSubcategories` | — | |
| 6 | Transactions and transfers | List with filters and cursor, quick entry from the phone, transfers | Expense on phone, transfer | |
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

## Tests

- Unit tests: `*.spec.ts` next to the code, run by Vitest (`pnpm test --watch=false`); 21 tests after block 1. Conventions in [testing.md](testing.md).
- Lint (`pnpm lint`) checks the module boundaries ([app-structure.md](app-structure.md#boundaries)).
- What must be tested is listed in [testing-strategy.md](../architecture/testing-strategy.md#minimum-required).

## Scalability

The app is organized in feature areas that mirror the backend modules (accounts, categories, transactions, budgets, dashboard, see [ADR-0002](../architecture/adr/0002-backend-structure.md)): a new end-to-end feature adds a backend module and a frontend feature with the same name, and lint keeps features independent. Details in each micro category.

## To do / open questions

- Block 2: first `schema.ts` from the running API; HTTP layer.
- Block 3: Playwright location, phone viewport, how the full stack runs for E2E.
- Block 4: date adapter and its time-zone tests.
- Block 8: chart library.
- `[DevOps]`: lint and coverage in CI; OpenAPI document exported at build time to check `schema.ts`.

---
_Last updated: 2026-11-13_
