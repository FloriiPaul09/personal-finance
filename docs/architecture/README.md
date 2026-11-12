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
| [0012](adr/0012-api-foundation.md) | API foundation — endpoint discovery, secure-by-default authorization, validation and tooling | Accepted | 2026-11-12 |
| [0013](adr/0013-error-responses-and-concurrency.md) | Error responses, optimistic concurrency over HTTP and access to other users' data | Accepted | 2026-11-12 |
| [0014](adr/0014-authentication-endpoints-and-sessions.md) | Authentication endpoints, CSRF, sessions and authentication in tests | Accepted | 2026-11-12 |
| [0015](adr/0015-resource-conventions-and-accounts.md) | Resource endpoint conventions and the accounts module | Accepted | 2026-11-12 |
| [0016](adr/0016-categories-module.md) | The categories module | Accepted | 2026-11-12 |
| [0017](adr/0017-transactions-and-tags.md) | Transactions, tags, paging and offline-safe creation | Accepted | 2026-11-12 |
| [0018](adr/0018-transfers-module.md) | The transfers module | Accepted | 2026-11-12 |
| [0019](adr/0019-budgets-module.md) | The budgets module | Accepted | 2026-11-12 |

## Current state

- **Initial architecture complete**: stack (ADR-0007), authentication (ADR-0001), backend structure (ADR-0002), endpoint style (ADR-0003), UI library (ADR-0004, documented in [frontend/ui-components.md](../frontend/ui-components.md)), license (ADR-0005), testing strategy (ADR-0006), repository layout.
- **Repository published** on GitHub as the public repository [`FloriiPaul09/personal-finance`](https://github.com/FloriiPaul09/personal-finance), skeleton created by `[DevOps]` (ADR-0008, ADR-0009): .NET solution `PersonalFinance.slnx`, Angular workspace with pnpm and Vitest, Compose with PostgreSQL.
- **Data model implemented** by `[DB]` (ADR-0010, ADR-0011): domain entities and rules, `AppDbContext` with Identity, isolation filter and interceptor, `InitialCreate` migration, default categories; see [database/README.md](../database/README.md).
- **Backend API in progress** in `[Backend]` (ADR-0012): foundation done (endpoint discovery, fallback policy, current user, validation, OpenAPI, architecture tests, ADR-0012) error handling done (ProblemDetails with codes, versions with `ETag` / `If-Match`, ADR-0013), authentication done (login, CSRF, rate limiting, initial user, keys in the database, ADR-0014), accounts done together with the conventions of every resource (enums as strings, amounts as numbers, list envelope, ADR-0015), categories done (ADR-0016), transactions and tags done (ADR-0017), transfers done (ADR-0018), budgets done (ADR-0019); the dashboard is the last block; see [backend/README.md](../backend/README.md).
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
| 2026-11-12 | API foundation: `IEndpoint<TModule>` discovered at startup, fallback policy requiring authentication, `HttpCurrentUser`, built-in validation, manual mapping, no mediator, OpenAPI + Scalar in Development, ArchUnitNET and banned `IgnoreQueryFilters` (decided in `[Backend]`) | [ADR-0012](adr/0012-api-foundation.md) |
| 2026-11-12 | Errors as ProblemDetails with stable `code` and `traceId`; domain codes `400` / `409` / `500` by family; duplicates from unique indexes `409`; `ETag` + mandatory `If-Match` (`428` / `412`); other users' data `404` (decided in `[Backend]`) | [ADR-0013](adr/0013-error-responses-and-concurrency.md) |
| 2026-11-12 | Custom auth endpoints, registration only when enabled, `SameSite=Strict` + antiforgery tokens, password ≥ 12, lockout 5 / 15 min, sliding 14-day cookie, login rate limiter, Data Protection keys in the database, test authentication handler (decided in `[Backend]`) | [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md) |
| 2026-11-12 | Resource conventions: enums as `camelCase` strings, amounts as JSON numbers, every list in `{ items }`, one response type per resource; accounts with balance computed in SQL, archive actions, delete only without movements (decided in `[Backend]`) | [ADR-0015](adr/0015-resource-conventions-and-accounts.md) |
| 2026-11-12 | Categories: flat list with filters, `PUT` of name + kind + parent, restore one category at a time, delete only when unused (decided in `[Backend]`) | [ADR-0016](adr/0016-categories-module.md) |
| 2026-11-12 | Transactions: kind + positive amount in, `signedAmount` out; client UUIDv7 ids for offline-safe creates; cursor paging; tags by name (decided in `[Backend]`) | [ADR-0017](adr/0017-transactions-and-tags.md) |
| 2026-11-12 | Transfers: one response with both legs, listed through transactions, one version for the whole transfer, client ids (decided in `[Backend]`) | [ADR-0018](adr/0018-transfers-module.md) |
| 2026-11-12 | Budgets: entries as a resource, months `yyyy-MM`, progress of a month equal to the domain, stop vs delete (decided in `[Backend]`) | [ADR-0019](adr/0019-budgets-module.md) |

## Tests

- `Architecture.Tests` checks the dependency rules of ADR-0002 and the rules of the API with ArchUnitNET ([backend/testing.md](../backend/testing.md#architecture-tests)).
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
- ✅ Auth endpoints (custom, registration only when enabled), CSRF (antiforgery tokens), password / lockout / cookie values, login rate limiting, Data Protection keys (database) — decided in `[Backend]`, [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md)
- ✅ Authorization fallback policy, `IEndpoint` discovery mechanics, OpenAPI UI (Scalar, Development only) — decided in `[Backend]`, [ADR-0012](adr/0012-api-foundation.md)
- ✅ Validation approach (built-in .NET 10), mapping (manual), mediator (none) — decided in `[Backend]`, [ADR-0012](adr/0012-api-foundation.md)
- ✅ Error model, domain side: domain errors are exceptions (`DomainException` with a stable code) — decided in `[DB]`, [ADR-0010](adr/0010-data-model-and-user-isolation.md); the HTTP mapping stays in `[Backend]`
- ✅ Architecture-test library (ArchUnitNET) — decided in `[Backend]`, [ADR-0012](adr/0012-api-foundation.md); ✅ expected status for cross-user access (`404`) — [ADR-0013](adr/0013-error-responses-and-concurrency.md); ✅ test authentication (test handler + real login) — [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md) — [testing-strategy.md](testing-strategy.md)
- ✅ Testcontainers fixture on `PostgresImage.Name` and database reset between tests — decided in `[DB]`: one container per run, migrated template, one database per test class, [ADR-0011](adr/0011-persistence-testing.md), [database/testing.md](../database/testing.md)
- ✅ From `[DB]`: HTTP current user (`ICurrentUser`) — `HttpCurrentUser`, [backend/security.md](../backend/security.md#current-user)
- ✅ From `[DB]`: HTTP status for concurrency conflicts — `412` with `ETag` / `If-Match`, [ADR-0013](adr/0013-error-responses-and-concurrency.md)
- ✅ Error model, HTTP side: ProblemDetails with stable `code`, domain codes by family — [ADR-0013](adr/0013-error-responses-and-concurrency.md)
- ✅ From `[DB]`: `DefaultCategoriesInitializer.EnsureForUserAsync` called after creating the initial user and at registration — [backend/security.md](../backend/security.md#initial-user)
- ✅ From `[DB]`: exposure of signed amounts in the API — kind + positive `amount` in requests, `signedAmount` in responses, [ADR-0017](adr/0017-transactions-and-tags.md)

### `[Frontend]`
- From `[Backend]`: call `GET /api/auth/antiforgery` at startup and keep Angular's default XSRF names (`XSRF-TOKEN` / `X-XSRF-TOKEN`); `GET /api/auth/me` to know whether to show the login; send `If-Match` with the `ETag` on updates and deletes; switch on the error `code` — [backend/security.md](../backend/security.md#csrf), [backend/validation-and-errors.md](../backend/validation-and-errors.md#codes)
- From `[Backend]`: lists come as `{ "items": [...] }`, enums as `camelCase` strings, amounts as JSON numbers never added on the client; accounts expose `balance` and `hasMovements` (lock the currency, offer archive instead of delete), categories come as a flat list to group by `parentId` with `isInUse` / `hasSubcategories` — [backend/api-endpoints.md](../backend/api-endpoints.md#accounts), [ADR-0015](adr/0015-resource-conventions-and-accounts.md)
- From `[Backend]`: the end-to-end login test (Playwright) is written with the login page — [ADR-0006](adr/0006-testing-strategy.md)
- From `[Backend]`: the offline queue of the PWA generates a UUIDv7 `id` for each expense and transfer and sends it again unchanged; a transfer leg in the list opens `/api/transfers/{transferId}`; budget screens send `month` explicitly and explain "stop from this month" vs "delete"; lists of transactions follow `nextCursor`; running balances use `signedAmount` — [backend/api-endpoints.md](../backend/api-endpoints.md#transactions), [ADR-0017](adr/0017-transactions-and-tags.md)
- From `[Backend]` to `[DB]`: a `pg_trgm` index on `transactions.description` when text search slows down — [ADR-0017](adr/0017-transactions-and-tags.md)
- Chart library, date adapter (no time-zone shift), theme and dark mode, i18n approach — [frontend/ui-components.md](../frontend/ui-components.md)
- `app-structure.md`, `routing.md` (login, guard, `401` interceptor), `state-management.md` — [frontend/README.md](../frontend/README.md)
- ✅ Vitest setup — done in `[DevOps]` with the Angular unit-test builder ([devops/local-development.md](../devops/local-development.md#tests))
- Playwright location, Vitest coverage provider, API client generated from OpenAPI — [testing-strategy.md](testing-strategy.md), [repository-structure.md](repository-structure.md)

### `[DevOps]`
- From `[Backend]`: HTTPS is required for login outside `localhost` (`Secure` cookies); forwarded headers behind the reverse proxy for rate limiting — [backend/security.md](../backend/security.md#to-do--open-questions)
- From `[Backend]`: enable Dependabot alerts and security updates; vulnerable transitive packages are pinned by hand until then — [devops/repository-workflow.md](../devops/repository-workflow.md#security-fixes-in-transitive-packages)
- HTTPS and reverse proxy, how the app is reached from the phone (LAN/VPN vs public) — [authentication.md](authentication.md)
- ✅ `Auth__*` variables in `.env.example` — [devops/configuration.md](../devops/configuration.md)
- CI workflow: jobs on pull requests vs main branch, coverage reporting, Docker for Testcontainers, when E2E tests run — part 2, [testing-strategy.md](testing-strategy.md)
- ✅ Repository name, `.editorconfig` / `.gitattributes`, branch strategy, dependency updates — [ADR-0009](adr/0009-repository-workflow.md)
- ✅ Compose for development — [ADR-0008](adr/0008-local-development-environment.md); production setup still open — [devops/docker.md](../devops/docker.md)
- ✅ License section in the README, `CONTRIBUTING.md`; `LICENSE` with copyright holder Paul Florii, 2026 — [ADR-0005](adr/0005-license.md)

## To do / open questions

None for the initial phase. This macro reopens when a new cross-cutting decision is needed (e.g. multi-user rollout, Worker host, native clients): each one gets its own ADR.

---
_Last updated: 2026-11-12_
