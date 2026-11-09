# Frontend

## Scope

The Angular single-page application: structure of the workspace, routing, state management, UI components, tests and the way the app talks to the API.

## Micro categories

| Document | Covers | Status |
|---|---|---|
| `app-structure.md` | Folders, feature areas, shared code, API client | Not started |
| `routing.md` | Routes, lazy loading, guards | Not started |
| `state-management.md` | Signals, services, server state, caching | Not started |
| [ui-components.md](ui-components.md) | Component library, theme, shared components, dates and amounts, mobile | Decided — ADR-0004 |
| `testing.md` | Vitest conventions, Playwright, phone viewport | Not started |

## Current state

**Workspace created** in `frontend/` by `[DevOps]` ([ADR-0008](../architecture/adr/0008-local-development-environment.md)); no feature yet, the page is still the Angular welcome page.

- Angular 22 (`ng new` defaults: standalone components, zoneless, TypeScript strict), SCSS, no SSR.
- **pnpm 11** (`packageManager` in `package.json`), Node.js 26 LTS (`.nvmrc`).
- **Vitest** through the Angular unit-test builder (`@angular/build:unit-test`, jsdom): `pnpm test`.
- Dev server proxies `/api` to the API on `http://localhost:5080` (`proxy.conf.json`), so the app always calls its own origin.

Fixed by the project context and ADRs:

- Latest stable Angular, standalone components, signals, TypeScript strict.
- UI in Italian, ready for i18n.
- Angular Material + CDK as UI foundation ([ADR-0004](../architecture/adr/0004-angular-ui-library.md)).
- Session through an `HttpOnly` cookie on the same origin as the API: the frontend never handles tokens ([ADR-0001](../architecture/adr/0001-authentication.md)).
- Served by nginx, which also proxies `/api` to the backend.

## Decisions

| Date | Decision | Reference |
|---|---|---|
| 2026-11-09 | Angular Material + CDK, shared wrappers for missing components, separate chart library | [ADR-0004](../architecture/adr/0004-angular-ui-library.md) |
| 2026-11-09 | pnpm 11, Node.js 26 LTS, Vitest through the Angular builder, `/api` proxy in development | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |

## Tests

- Unit tests: `*.spec.ts` next to the code, run by Vitest with `pnpm test` (single run: `pnpm test --watch=false`). Today only the generated `app.spec.ts` exists.
- What must be tested is listed in [testing-strategy.md](../architecture/testing-strategy.md#minimum-required); conventions go in `testing.md`.

## Scalability

To evaluate in `app-structure.md`: organizing the app in feature areas that mirror the backend modules (accounts, categories, transactions, budgets, see [ADR-0002](../architecture/adr/0002-backend-structure.md)), so that a new end-to-end feature adds a backend module and a frontend area with the same name.

## To do / open questions

- Chart library, date adapter, theme and dark mode, i18n approach → [ui-components.md](ui-components.md)
- Login page, route guard, `401` interceptor → `routing.md`
- Write `app-structure.md`, `routing.md`, `state-management.md`, `testing.md`
- Add Angular Material (`ng add @angular/material`) and replace the welcome page
- Playwright location and phone viewport; Vitest coverage provider

---
_Last updated: 2026-11-09_
