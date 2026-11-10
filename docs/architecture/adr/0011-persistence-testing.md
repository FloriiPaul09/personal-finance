# ADR-0011: Persistence tests — one container, one database per test class

- **Status**: Accepted
- **Date**: 2026-11-10
- **Deciders**: Paul

## Context

- Integration tests run on a real PostgreSQL through Testcontainers, with the image read from `compose.yaml` (`PostgresImage.Name`) ([ADR-0006](0006-testing-strategy.md), [ADR-0008](0008-local-development-environment.md)).
- The data model relies on database behavior that only a real PostgreSQL can verify: composite foreign keys between users, `CHECK` constraints, the per-user query filter, the `SaveChanges` interceptor, migrations ([ADR-0010](0010-data-model-and-user-isolation.md)).
- Tests must stay independent from each other and fast enough to run on every pull request; xUnit v3 runs test classes in parallel by default.
- Every row of the domain belongs to a user: tests that create their own users never see each other's data.
- A second test project needs the same PostgreSQL setup: shared helpers move to a support project ([local-development.md](../../devops/local-development.md#scalability)).

## Decision

1. **One PostgreSQL container per test run**, started by an xUnit v3 **assembly fixture** on `PostgresImage.Name`.
2. At start the fixture creates a **template database** and applies the migrations once.
3. **Each test class gets its own database**, cloned from the template (`CREATE DATABASE … TEMPLATE …`). Classes run in parallel; nothing is reset.
4. **Inside a class, every test creates its own users** and asserts only on their data. Tests never count rows across the whole database.
5. Test projects:
   - **`PersonalFinance.Testing`** (`backend/tests/Testing`): support library, not a test project — `PostgresImage`, the container and database fixtures, test data builders, a fixed current user and clock. Used by every integration test project.
   - **`PersonalFinance.Infrastructure.Tests`** (`backend/tests/Infrastructure.Tests`): persistence tests — migrations, constraints, composite keys, query filter, interceptor, default data.
   - `PersonalFinance.Api.Tests` keeps endpoint tests and reuses the same fixtures with `WebApplicationFactory`, pointing the API at the class database.

Details: [database/testing.md](../../database/testing.md).

## Consequences

**Positive**
- Full isolation between test classes and parallel execution without reset code.
- Migrations run once per test run; cloning a template is much faster than migrating again.
- The same setup serves persistence tests and endpoint tests.
- Tests exercise commits, constraints and interceptors exactly as production does.

**Negative**
- About fifty lines of project code (template, cloning, cleanup) to write and maintain.
- A convention to respect: no assertions on global counts, every test creates its own users.
- Many short-lived databases during a run (they disappear with the container).
- Two more projects in the solution.

## Alternatives considered

**Respawn between tests, tests run sequentially** — Rejected. Well-known and little code, but no parallelism and an extra dependency; the suite slows down as it grows.

**One transaction per test, rolled back at the end** — Rejected. Fastest, but it does not work with HTTP calls through `WebApplicationFactory` (another connection) and never verifies what happens at commit time.

**Persistence tests in `Api.Tests`** — Rejected. No new project, but persistence tests mixed with endpoint tests and test helpers that stay tied to the API project.
