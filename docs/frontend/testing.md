# Frontend testing

## Scope

How the Angular application is tested: unit tests with Vitest, coverage, end-to-end tests with Playwright and the phone viewport. What must be tested is fixed by [testing-strategy.md](../architecture/testing-strategy.md#minimum-required) and the Definition of Done; this document holds the frontend conventions.

## Current state

Vitest and coverage set up (blocks 0–1); Playwright arrives with the login page (block 3).

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

- `<mat-icon svgIcon>` requests its SVG through `HttpClient`: tests that render icons provide `provideHttpClientTesting()` and leave those requests unanswered.
- **Dates** (block 4): the date adapter is tested under time zones far from UTC (e.g. `Pacific/Kiritimati`, `America/Los_Angeles`), to prove a date never moves by a day.

### Coverage

- `pnpm test:coverage`: Vitest's `v8` provider through the builder (`coverage` option), reports `text-summary` in the terminal, `html` and `lcov` in `coverage/` (ignored by git).
- `coverageExclude`: the generated `core/api/schema.ts`.
- **No threshold** ([ADR-0006](../architecture/adr/0006-testing-strategy.md)): coverage points at untested areas, it is not a goal.

### End-to-end (Playwright)

To be decided with block 3: location of the tests (`frontend/e2e/` or a top-level `e2e/`), phone viewport and browsers, how the full stack is started. Critical flows: login, adding an expense on a phone, a transfer, the dashboard.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Vitest through the Angular builder, jsdom | Default runner of Angular 22 | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| `v8` coverage provider, no threshold | Fast, and as accurate as `istanbul` since Vitest 3.2 | [ADR-0021](../architecture/adr/0021-frontend-foundation.md), [ADR-0006](../architecture/adr/0006-testing-strategy.md) |
| Spec files next to the code | Moving or deleting code moves or deletes its tests | — |

## Tests

Tests of block 1 (21 tests): `app.spec.ts` (routes and shell), `app-locale.spec.ts`, `core/icons/app-icons.spec.ts`, `core/layout/page-title-strategy.spec.ts`. Coverage after block 1: 100% of statements and lines.

## Scalability

- **More tests**: Vitest runs files in parallel; the builder rebuilds only what changed in watch mode.
- **Browser mode**: if jsdom misses a behavior (layout, real `IntersectionObserver` for infinite scroll), Vitest browser mode with Playwright can run the same specs in Chromium.
- **CI** (`[DevOps]`): `pnpm lint`, `pnpm test:coverage` with the `lcov` report on every pull request; E2E timing decided there.

## To do / open questions

- Block 3: Playwright location, phone viewport, how the stack runs for E2E; first test (login).
- Block 4: time-zone tests of the date adapter; amount input tests.
- `[DevOps]`: frontend jobs in CI.

---
_Last updated: 2026-11-13_
