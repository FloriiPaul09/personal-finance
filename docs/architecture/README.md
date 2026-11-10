# Architecture

## Scope

Cross-cutting decisions that affect more than one area of the project (database, backend, frontend, DevOps): technology stack, authentication, structure of the backend solution, testing strategy, repository layout, and the Architecture Decision Records (ADRs) that justify them.

## Micro categories

| Document | Covers | Status |
|---|---|---|
| [authentication.md](authentication.md) | How users sign in, how sessions work, how data is owned per user | Decided — ADR-0001 |
| [backend-structure.md](backend-structure.md) | Projects of the .NET solution, vertical slices, endpoints, shared code and duplication | Decided — ADR-0002, ADR-0003; solution created |
| [testing-strategy.md](testing-strategy.md) | Test levels, tools, minimum required tests, CI | Decided — ADR-0006; test projects and Vitest set up |
| [repository-structure.md](repository-structure.md) | Monorepo layout, top-level files, repository rules | Decided — ADR-0007, ADR-0009; skeleton created |

## Architecture Decision Records

ADRs live in [`adr/`](adr/) and are named `NNNN-short-title.md`. Each one records context, decision, consequences and rejected alternatives.

- **Status values**: `Proposed`, `Accepted`, `Superseded by ADR-NNNN`.
- An accepted ADR is not rewritten: if the decision changes, a new ADR supersedes it and the old one is marked accordingly.
- Numbers follow the order in which ADRs are written, not the order of decisions (ADR-0007 records the initial stack retroactively).

| ADR | Title | Status | Date |
|---|---|---|---|
| [0001](adr/0001-authentication.md) | Authentication with ASP.NET Core Identity | Accepted | 2026-11-09 |
| [0002](adr/0002-backend-structure.md) | Backend structure — vertical slices with Domain and Infrastructure projects | Accepted | 2026-11-09 |
| [0003](adr/0003-minimal-api.md) | Endpoints with native Minimal API | Accepted | 2026-11-09 |
| [0004](adr/0004-angular-ui-library.md) | Angular Material as the UI component library | Accepted | 2026-11-09 |
| [0005](adr/0005-license.md) | MIT license for the repository | Accepted | 2026-11-09 |
| [0006](adr/0006-testing-strategy.md) | Risk-focused testing strategy | Accepted | 2026-11-09 |
| [0007](adr/0007-tech-stack.md) | Technology stack (retroactive) | Accepted | 2026-11-09 |
| [0008](adr/0008-local-development-environment.md) | Local development environment | Accepted | 2026-11-09 |
| [0009](adr/0009-repository-workflow.md) | Repository workflow — branches, commits and dependency updates | Accepted | 2026-11-09 |
| [0010](adr/0010-data-model-and-user-isolation.md) | Data model, keys and per-user isolation | Accepted | 2026-11-10 |
| [0011](adr/0011-persistence-testing.md) | Persistence tests — one container, one database per test class | Accepted | 2026-11-10 |

## Current state

- **Initial architecture complete**: stack (ADR-0007), authentication (ADR-0001), backend structure (ADR-0002), endpoint style (ADR-0003), UI library (ADR-0004, documented in [frontend/ui-components.md](../frontend/ui-components.md)), license (ADR-0005), testing strategy (ADR-0006), repository layout.
- **Repository skeleton created** by `[DevOps]` (ADR-0008, ADR-0009): .NET solution `PersonalFinance.slnx` with the three projects and three test projects, Angular workspace with pnpm and Vitest, Compose with PostgreSQL. No feature is implemented yet; the GitHub repository is not published yet.
- **Data model decided** by `[DB]` (ADR-0010, ADR-0011): see [database/README.md](../database/README.md).
- Detail questions are handed off to the other areas (see below).

## Decisions

| Date | Decision | Reference |
|---|---|---|
| 2026-11-09 | Angular + ASP.NET Core (LTS) + EF Core/Npgsql + PostgreSQL + Docker Compose, monorepo on GitHub | [ADR-0007](adr/0007-tech-stack.md) |
| 2026-11-09 | ASP.NET Core Identity from day one, cookie-based session, registration disabled by default, `UserId` on every user-owned entity | [ADR-0001](adr/0001-authentication.md) |
| 2026-11-09 | Three backend projects: `Api` (vertical slices by module), `Domain` (no dependencies), `Infrastructure` (EF Core, Identity); no repository layer | [ADR-0002](adr/0002-backend-structure.md) |
| 2026-11-09 | Native Minimal API: one endpoint per slice, automatic registration, one route group per module | [ADR-0003](adr/0003-minimal-api.md) |
| 2026-11-09 | Angular Material + CDK as UI foundation, shared wrappers for missing components, separate chart library | [ADR-0004](adr/0004-angular-ui-library.md) |
| 2026-11-09 | MIT license; contributions accepted under the same license | [ADR-0005](adr/0005-license.md) |
| 2026-11-09 | Risk-focused testing: domain unit, API integration on real PostgreSQL with per-endpoint isolation tests, architecture tests, Vitest, a few Playwright E2E; coverage without threshold | [ADR-0006](adr/0006-testing-strategy.md) |
| 2026-11-09 | Monorepo layout with `.github/`, `backend/`, `frontend/`, `docs/` and project-wide files at the root | [repository-structure.md](repository-structure.md) |
| 2026-11-09 | Database in Docker and applications on the host; versioned development defaults; PostgreSQL 18 pinned once in Compose and read by Testcontainers; pnpm 11 | [ADR-0008](adr/0008-local-development-environment.md) |
| 2026-11-09 | Repository `personal-finance` (projects `PersonalFinance.*`), strict GitHub Flow with squash merge, commits `type(area[/module])`, Dependabot | [ADR-0009](adr/0009-repository-workflow.md) |
| 2026-11-10 | `Guid` keys everywhere, snake_case tables, named query filter + composite foreign keys `(user_id, id)`, signed amounts, transfers as two legs, computed balances, budgets with `effective_from` (decided in `[DB]`) | [ADR-0010](adr/0010-data-model-and-user-isolation.md) |
| 2026-11-10 | Persistence tests: one container per run, migrated template, one database per test class; `Infrastructure.Tests` and `Testing` projects (decided in `[DB]`) | [ADR-0011](adr/0011-persistence-testing.md) |

## Tests

- `Architecture.Tests` checks the dependency rules of ADR-0002 (today with plain reflection, see [backend-structure.md](backend-structure.md#tests)).
- `PostgresImageTests` checks that the PostgreSQL version is pinned once ([devops/docker.md](../devops/docker.md)).
- The remaining decisions are verified by the tests described in each micro category.

## Scalability

Architectural choices are made assuming the app may become multi-user and accept external contributors: when two options cost about the same today, prefer the one that reaches that future without a rewrite. The backend can grow from a single API host to API + worker, and from module folders to a modular monolith, without changing the structure decided in ADR-0002 and ADR-0003. Multi-user is a configuration change plus UI thanks to ADR-0001.

## Handoffs to other areas

Questions raised here that belong to another conversation. Each one is detailed in the linked document. Resolved items are marked ✅ with the place where they were decided.

### `[DB]`
- ✅ `UserId` on every owned entity, global query filters, `SaveChanges` interceptor for `UserId` / `CreatedAt` / `UpdatedAt` — decided in `[DB]`, [ADR-0010](adr/0010-data-model-and-user-isolation.md), [database/user-isolation.md](../database/user-isolation.md)
- ✅ Identity key type (`Guid`) and table naming (snake_case) — decided in `[DB]`, [ADR-0010](adr/0010-data-model-and-user-isolation.md), [database/conventions.md](../database/conventions.md)
- ✅ PostgreSQL version pinned once, shared by Compose and Testcontainers — decided in `[DevOps]`, [ADR-0008](adr/0008-local-development-environment.md)

### `[Backend]`
- Auth endpoints (`MapIdentityApi` or custom, registration disabled), CSRF, password / lockout / cookie values, login rate limiting, Data Protection keys — [authentication.md](authentication.md)
- Authorization fallback policy, `IEndpoint` discovery mechanics, OpenAPI UI — [backend-structure.md](backend-structure.md)
- Validation approach, mapping, mediator or not — [backend-structure.md](backend-structure.md)
- ✅ Error model, domain side: domain errors are exceptions (`DomainException` with a stable code) — decided in `[DB]`, [ADR-0010](adr/0010-data-model-and-user-isolation.md); the HTTP mapping stays in `[Backend]`
- Architecture-test library, test authentication, expected status for cross-user access — [testing-strategy.md](testing-strategy.md)
- ✅ Testcontainers fixture on `PostgresImage.Name` and database reset between tests — decided in `[DB]`: one container per run, migrated template, one database per test class, [ADR-0011](adr/0011-persistence-testing.md), [database/testing.md](../database/testing.md)
- From `[DB]`: exposure of signed amounts in the API, HTTP status for concurrency conflicts, creation of default categories at startup — [database/README.md](../database/README.md#to-do--open-questions)

### `[Frontend]`
- Chart library, date adapter (no time-zone shift), theme and dark mode, i18n approach — [frontend/ui-components.md](../frontend/ui-components.md)
- `app-structure.md`, `routing.md` (login, guard, `401` interceptor), `state-management.md` — [frontend/README.md](../frontend/README.md)
- ✅ Vitest setup — done in `[DevOps]` with the Angular unit-test builder ([devops/local-development.md](../devops/local-development.md#tests))
- Playwright location, Vitest coverage provider, API client generated from OpenAPI — [testing-strategy.md](testing-strategy.md), [repository-structure.md](repository-structure.md)

### `[DevOps]`
- HTTPS and reverse proxy, how the app is reached from the phone (LAN/VPN vs public) — [authentication.md](authentication.md)
- ✅ `Auth__*` variables in `.env.example` — [devops/configuration.md](../devops/configuration.md)
- CI workflow: jobs on pull requests vs main branch, coverage reporting, Docker for Testcontainers, when E2E tests run — part 2, [testing-strategy.md](testing-strategy.md)
- ✅ Repository name, `.editorconfig` / `.gitattributes`, branch strategy, dependency updates — [ADR-0009](adr/0009-repository-workflow.md)
- ✅ Compose for development — [ADR-0008](adr/0008-local-development-environment.md); production setup still open — [devops/docker.md](../devops/docker.md)
- ✅ License section in the README, `CONTRIBUTING.md`; `LICENSE` with copyright holder Paul Florii, 2026 — [ADR-0005](adr/0005-license.md)

## To do / open questions

None for the initial phase. This macro reopens when a new cross-cutting decision is needed (e.g. multi-user rollout, Worker host, native clients): each one gets its own ADR.

---
_Last updated: 2026-11-10_
