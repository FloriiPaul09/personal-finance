# Database testing

## Scope

How the domain model and the persistence layer are tested: domain unit tests, persistence tests on a real PostgreSQL, the Testcontainers fixture shared by every integration test project, the database-per-class strategy and the conventions for test data. Endpoint tests are in [backend/testing.md](../backend/testing.md); the overall strategy in [testing-strategy.md](../architecture/testing-strategy.md).

## Current state

Decided ([ADR-0011](../architecture/adr/0011-persistence-testing.md)). **Implemented**: `Testing` project and fixture, domain unit tests (`Domain.Tests`), persistence and isolation tests (`Infrastructure.Tests/Persistence`). The template database gets every migration once per run.

### Projects

| Project | Folder | Contains |
|---|---|---|
| `PersonalFinance.Domain.Tests` | `backend/tests/Domain.Tests` | Unit tests of the business rules, no database |
| `PersonalFinance.Infrastructure.Tests` | `backend/tests/Infrastructure.Tests` | Persistence tests: migrations, constraints, isolation, interceptor, default data |
| `PersonalFinance.Api.Tests` | `backend/tests/Api.Tests` | Endpoint tests (`[Backend]`), same fixture |
| `PersonalFinance.Testing` | `backend/tests/Testing` | Support library, **not** a test project: `PostgresImage`, fixtures, builders, fixed current user and clock |

`PostgresImage` lives in `Testing`, `PostgresImageTests` in `Infrastructure.Tests`. `Api.Tests` registers `ApiServer` (a `PostgresServerFixture` that also caches one API host per test class, [backend/testing.md](../backend/testing.md#hosts)); its placeholder test was removed with the first API tests. Subclasses dispose what they build on the databases in `DisposeDependentsAsync`, before the container stops.

### Fixture

```
test run ──► DatabaseServer : PostgresServerFixture (assembly fixture, once)
               ├─ starts PostgresImage.Name with Testcontainers
               └─ creates pf_template and applies all migrations (InitializeTemplateAsync)

each test class ──► DatabaseForClassAsync(test class), created on first use and cached
               ├─ CREATE DATABASE pf_test_<random> TEMPLATE pf_template
               └─ TestDatabase: name + connection string for DbContexts or WebApplicationFactory
```

- **One container per run**: each integration test project declares a `DatabaseServer` deriving from `PostgresServerFixture` (in `Testing`) and registers it with `[assembly: AssemblyFixture(typeof(DatabaseServer))]` (xUnit v3). The fixture applies every migration to the template with pooling off, so no connection stays open on it.
- **Database per class without a class fixture**: xUnit fixtures cannot depend on each other, so the assembly fixture creates the class databases itself and caches them by test class. Test classes derive from `DatabaseTest`, which receives the server in its constructor and exposes the class `Database`:

  ```csharp
  public sealed class AccountPersistenceTests(DatabaseServer server) : DatabaseTest(server)
  {
      [Fact]
      public async Task Account_RoundTrips() { /* uses Database.ConnectionString */ }
  }
  ```

- **Extra databases**: `Server.CreateDatabaseAsync()` clones one more database for a test that needs it (e.g. to compare two databases).
- Clones are serialized by the server (a lock around `CREATE DATABASE`): PostgreSQL copies the template safely and the cost stays in the tens of milliseconds per class.
- **Template database**: migrated once; connections to it are closed before cloning (PostgreSQL refuses to copy a database in use).
- **One database per test class**: cloned from the template in a few tens of milliseconds; classes run in parallel; no reset, no Respawn.
- The container and every database disappear at the end of the run.
- Docker must be running (Docker Desktop on Windows).

### Test data conventions

- **Every test creates its own users** (`TestUsers.Create()`, unique email) and works only with them. Tests in the same class share a database, never data.
- **Never assert on global counts** (`db.Accounts.Count()` without a user): another test of the class may have added rows. Assert through the current user, or by id.
- **Builders** in `Testing` create valid entities with sensible defaults (`AnAccount().WithCurrency("EUR")`), so a test states only what matters to it.
- **Fixed clock** (`TestClock`, default 2026-11-10 12:00 UTC) and **fixed current user** for contexts created in tests: `Database.CreateContext(userId, clock)` builds an `AppDbContext` configured as in production and acting for that user (nobody when null).
- `Database.CreateUserAsync()` creates a user with a unique email; `SampleData.CreateAsync` (in `Infrastructure.Tests`) saves one of each entity for a user through the domain.
- PostgreSQL errors are asserted by `SqlState` (`23503` foreign key, `23505` unique, `23514` check), not by message.

### Domain unit tests

`Domain.Tests`, one folder per module (`Accounts/`, `Categories/`, `Transactions/`, `Budgets/`, `Tags/`). Mandatory cases are listed in [domain-model.md](domain-model.md#tests); test names describe the behavior (`Transfer_RejectsSameAccount`, `Budget_IncludesSubcategories`). Edge cases always include: zero, negative, three decimals, first and last day of the month, leap year.

### Persistence tests

`Infrastructure.Tests`:

| Area | Examples |
|---|---|
| Test infrastructure | The class database runs the PostgreSQL version pinned in Compose; it is a clone, not the template; it is shared by the tests of the class; two clones do not share data (`DatabaseServerTests`); the image is pinned (`PostgresImageTests`) |
| Model | No pending model changes; every migration applied to the template; every owned entity has the `UserScope` filter and the `(user_id, id)` key; snake_case tables (`ModelTests`) |
| Mapping | Every entity round-trips (save, read in a new context, compare); `DateOnly` stays the same day; `numeric(18,2)` keeps the value |
| Constraints | Each `CHECK`, unique key and delete behavior of [conventions.md](conventions.md#tests) |
| Concurrency | A stale `xmin` raises `DbUpdateConcurrencyException` |
| Balance | The SQL balance equals the domain balance on the same movements |
| Default data | [default-data.md](default-data.md#tests) |

### Isolation tests

Listed in [user-isolation.md](user-isolation.md#tests). Every owned entity type is covered by the same pattern: user A creates data, user B's context must not read, update, delete or reference it. A model test fails if a new owned entity lacks the `UserScope` filter or the `(user_id, id)` key.

### Running

```powershell
cd backend
dotnet test                                                    # everything (Docker running)
dotnet test --project tests/Domain.Tests                       # fast, no Docker
dotnet test --project tests/Infrastructure.Tests
```

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| One container per run, migrated template, one database per class | Parallel, isolated, no reset code; migrations run once | [ADR-0011](../architecture/adr/0011-persistence-testing.md) |
| Every test creates its own users | Exploits per-user data: tests never see each other | [ADR-0011](../architecture/adr/0011-persistence-testing.md) |
| `Infrastructure.Tests` + `Testing` support project | Each level in its place; one fixture for every integration project | [ADR-0011](../architecture/adr/0011-persistence-testing.md) |
| Real PostgreSQL, never an in-memory provider | Constraints, filters and migrations behave as in production | [ADR-0006](../architecture/adr/0006-testing-strategy.md) |

## Scalability

- **More test classes**: databases per class scale with parallelism; if the container becomes a bottleneck, raise `max_connections` in the container or limit parallel classes.
- **Worker** (roadmap): `Worker.Tests` references `Testing` and gets the same fixture.
- **CI**: Docker is available on GitHub-hosted Linux runners; the template build is the only fixed cost per job.
- **Slow suite**: split persistence and endpoint tests into separate CI jobs.

## To do / open questions

- `[Backend]` Authentication in endpoint tests and the expected status for cross-user access ([testing-strategy.md](../architecture/testing-strategy.md#to-do--open-questions)).
- `[DevOps]` CI job with Docker for `Infrastructure.Tests` and `Api.Tests` (part 2).

---
_Last updated: 2026-11-12_
