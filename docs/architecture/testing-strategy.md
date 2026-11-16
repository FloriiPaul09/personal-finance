# Testing strategy

## Scope

What is tested, at which level, with which tools, and how tests run locally and in CI, for both backend and frontend. Test code conventions specific to one area are documented in that area's docs.

## Current state

Decided ([ADR-0006](adr/0006-testing-strategy.md)). **Test projects and runners are set up** (`[DevOps]`, [local-development.md](../devops/local-development.md#tests)); feature tests arrive with the features.

### Principle

Test where an error is expensive. For this app that means:

1. **Money correctness**: balances, transfers, budgets.
2. **Data isolation**: a user must never see or change another user's data.
3. **Authentication**: nothing is reachable without a session, except login.

Coverage follows these risks, not a percentage.

### Levels and tools

| Level | Tools | What it covers | Location |
|---|---|---|---|
| Domain unit | xUnit v3 | Every business rule in `Domain` | `backend/tests/Domain.Tests` |
| Persistence integration | xUnit v3 + Testcontainers (real PostgreSQL) | Migrations, constraints, composite keys, query filter, interceptor, default data ([ADR-0011](adr/0011-persistence-testing.md)) | `backend/tests/Infrastructure.Tests` |
| API integration | xUnit v3 + `WebApplicationFactory` + Testcontainers (real PostgreSQL) | Endpoints through HTTP: validation, persistence, query filters, authentication | `backend/tests/Api.Tests` |
| Architecture | ArchUnitNET ([ADR-0012](adr/0012-api-foundation.md)) | Dependency rules of [ADR-0002](adr/0002-backend-structure.md) (e.g. `Domain` references nothing), slice rules, no `IgnoreQueryFilters` in `Api` | `backend/tests/Architecture.Tests` |
| Frontend unit | Vitest through the Angular `@angular/build:unit-test` builder, jsdom | Shared components and logic: amount input, date adapter, services with logic | `frontend/src/**/*.spec.ts` |
| End-to-end | Playwright | A few critical flows on the full stack | Location to define in `[Frontend]` block 3 ([frontend/testing.md](../frontend/testing.md)) |

Backend tests run on **Microsoft Testing Platform** (enabled in `backend/global.json`), the default runner of xUnit v3 4.x and of the .NET 10 SDK. A test project that runs zero tests fails, so every project keeps at least one test.

### Minimum required

- **Domain**: every business rule has unit tests for normal and edge cases (e.g. a transfer produces two linked movements with opposite amounts; budget progress at month boundaries).
- **Each endpoint**, at least:
  - the successful case;
  - `401` without a session;
  - a validation error, if it accepts input;
  - **isolation**: user B cannot read or change user A's data (`404`, [ADR-0013](adr/0013-error-responses-and-concurrency.md)).
- **Frontend shared components**: Italian amount parsing and formatting; dates never shifted by the time zone.
- **End-to-end**: login, adding an expense on a phone-sized viewport, a transfer between accounts, dashboard loading.
- **Bug fixes**: a test that reproduces the bug comes with the fix.

### Integration tests

- One PostgreSQL container per test run (not per test), started by an xUnit v3 assembly fixture; migrations are applied once to a **template database**: the same migrations used in production are exercised ([ADR-0011](adr/0011-persistence-testing.md)).
- **Same PostgreSQL image as Docker Compose**: the version is written only in the `db` service of `compose.yaml`; tests read it through `PostgresImage.Name` (in the shared `Testing` support project) ([ADR-0008](adr/0008-local-development-environment.md)).
- **One database per test class**, cloned from the template; classes run in parallel and nothing is reset. Every test creates its own users and never asserts on global counts. Details: [database/testing.md](../database/testing.md).
- Docker is required to run them, locally and in CI.

### Coverage

Measured for backend and frontend and shown in CI, **without a mandatory threshold**. It is used to spot untested areas, not as a target. Frontend: Vitest's `v8` provider, `pnpm test:coverage` ([ADR-0021](adr/0021-frontend-foundation.md)).

### Where tests run

- **Locally**: `dotnet test` in `backend/` (Docker running for integration tests), `pnpm test` in `frontend/` (`--watch=false` for a single run), Playwright for end-to-end.
- **CI (GitHub Actions)**: build and all backend and frontend tests on every pull request and push to the main branch; when end-to-end tests run is decided in `[DevOps]` part 2.
- A pull request is merged only with all tests green (required checks on `main`, [ADR-0009](adr/0009-repository-workflow.md)).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Risk-focused strategy: domain unit, API integration on real PostgreSQL, architecture tests, frontend unit, a few E2E | Covers money, isolation and auth with sustainable effort for one developer | [ADR-0006](adr/0006-testing-strategy.md) |
| Testcontainers instead of an in-memory database | Query filters, migrations and PostgreSQL behavior are tested for real | [ADR-0006](adr/0006-testing-strategy.md) |
| Coverage measured, no threshold | Thresholds push toward tests written for the number | [ADR-0006](adr/0006-testing-strategy.md) |
| PostgreSQL image read from `compose.yaml` | One version for development, tests and CI | [ADR-0008](adr/0008-local-development-environment.md) |
| One container per run, migrated template, one database per test class; `Infrastructure.Tests` and `Testing` projects | Parallel and isolated tests without reset code; one fixture for every integration project | [ADR-0011](adr/0011-persistence-testing.md) |
| xUnit v3 on Microsoft Testing Platform; Vitest through the Angular builder | Default runners of the chosen tool versions | [ADR-0008](adr/0008-local-development-environment.md) |

## Tests

This strategy is checked by the CI pipeline (part 2) and by the pull request checklist (`.github/pull_request_template.md`), which repeats the minimum required list.

## Scalability

- **More contributors**: add a coverage rule (minimum threshold or "no decrease" on pull requests) with a new ADR; the minimum required list above becomes the review checklist.
- **More modules**: test folders mirror `Features/<Module>` in the API and the modules in `Domain`.
- **Worker** (roadmap): its own integration tests, reusing the fixture of the `Testing` support project.
- **Slower test suite**: run test projects in parallel, split CI jobs, move end-to-end tests to the main branch or a nightly run.
- **Multi-user**: already covered by the isolation tests required on every endpoint.

## To do / open questions

- ✅ `[Backend]` Architecture-test library: ArchUnitNET ([ADR-0012](adr/0012-api-foundation.md), [backend/testing.md](../backend/testing.md#architecture-tests)).
- ✅ `[Backend]` How tests authenticate (test handler for resources, real login for authentication) and test data (`TestData` factories) — [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md); ✅ expected status for cross-user access: `404` ([ADR-0013](adr/0013-error-responses-and-concurrency.md)) ([backend/README.md](../backend/README.md)).
- ✅ Testcontainers fixture and database reset between tests — decided in `[DB]` ([ADR-0011](adr/0011-persistence-testing.md)).
- ✅ `[Frontend]` Vitest coverage provider: `v8` ([ADR-0021](adr/0021-frontend-foundation.md)).
- `[Frontend]` Playwright location and phone viewport (block 3).
- `[DevOps]` CI workflow: jobs on pull requests vs main branch, coverage reporting, Docker for Testcontainers on runners (part 2).

---
_Last updated: 2026-11-13_
