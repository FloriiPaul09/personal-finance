# Backend testing

## Scope

How the API is tested: the test projects involved, how tests start the API on a real PostgreSQL, tests of the endpoint mechanism, host tests, architecture tests, and the minimum required for every endpoint. Domain and persistence tests are in [database/testing.md](../database/testing.md); the overall strategy in [architecture/testing-strategy.md](../architecture/testing-strategy.md) ([ADR-0006](../architecture/adr/0006-testing-strategy.md)).

## Current state

Implemented for the foundation, the error handling and authentication ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md), [ADR-0011](../architecture/adr/0011-persistence-testing.md), [ADR-0012](../architecture/adr/0012-api-foundation.md), [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)).

### Projects

| Project | Contains | Needs Docker |
|---|---|---|
| `PersonalFinance.Api.Tests` | Discovery and current-user unit tests, mechanism tests on a minimal host, host and endpoint tests on the real API | Yes (assembly fixture) |
| `PersonalFinance.Architecture.Tests` | ArchUnitNET rules on `Api`, `Domain`, `Infrastructure` | No |
| `PersonalFinance.Testing` | Shared support: PostgreSQL fixture, test databases, clock ([database/testing.md](../database/testing.md)) | — |

Run everything from `backend/` with `dotnet test` (Docker running). The placeholder test of `Api.Tests` is gone: the project now has real tests.

### Hosts

```
test run ──► ApiServer : PostgresServerFixture (assembly fixture of Api.Tests)
               └─ container + migrated template, as in ADR-0011

each test class ──► Database (cloned from the template, DatabaseTest)
                └─► Host: ApiFactory = WebApplicationFactory<Program>, Development,
                      created on first use, cached by test class, disposed at the end of the run
```

- `ApiFactory` runs the real `Program.cs` (same services and pipeline) and adds its settings as the **last configuration source**: they win over `appsettings.Development.json` and environment variables, so tests never touch the development database. Settings: the class database's connection string, an initial user (`owner@example.com`), a login rate limit high enough for many tests from one address. Extra settings override them; an empty value removes one (startup tests remove the initial user).
- `ApiFactory` also registers `TestAuthenticationHandler` ([Authentication in tests](#authentication-in-tests)).
- `Program.cs` reads the connection string when the first `DbContext` is created, not while building the host, so the value from the factory is the one used.
- Endpoint test classes derive from `ApiTest`:

  ```csharp
  public sealed class AccountsTests(ApiServer server) : ApiTest(server)
  {
      [Fact]
      public async Task List_ReturnsOnlyTheAccountsOfTheUser()
      {
          var user = await NewUserAsync();
          var other = await NewUserAsync();
          await Database.CreateAccountAsync(user, name: "Carta", cancellationToken: Ct);
          await Database.CreateAccountAsync(other, cancellationToken: Ct);

          var accounts = await ClientFor(user).GetFromJsonAsync<List<AccountResponse>>("/api/accounts", Ct);

          Assert.Equal("Carta", Assert.Single(accounts!).Name);
      }

      [Fact]
      public async Task List_Returns401WithoutSession()
      {
          var response = await AnonymousClient().GetAsync("/api/accounts", Ct);
          await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
      }
  }
  ```

- A test that needs another environment creates its own host: `await using var production = new ApiFactory(Database, Environments.Production);`.
- `PostgresServerFixture.DisposeDependentsAsync` lets `ApiServer` dispose the hosts before the container stops.

### Authentication in tests

Two ways in, both on the same host ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)):

| Tests | How they sign in | Helper |
|---|---|---|
| Resource tests (blocks 4–9) | **Test authentication handler**: the header `X-Test-User: <user id>` signs the request in as that user, no password, no cookie | `ClientFor(userId)`, users from `NewUserAsync()` |
| Authentication tests | **Real login** through `POST /api/auth/login` and the `pf_session` cookie | `AnonymousClient()`, users from `NewUserWithPasswordAsync()` |

- The default scheme of the test host is a selector: the test handler when the header is present, the real Identity cookie otherwise. Requests without the header behave exactly as in production.
- Every test client behaves like the browser app (`TestClients`): base address `https://localhost` (the cookies are `Secure`), a cookie container, and `AntiforgeryHandler`, which fetches `GET /api/auth/antiforgery` before the first unsafe request, sends `X-XSRF-TOKEN`, and keeps the newest `XSRF-TOKEN` the API sets (after login and logout). A test that must not send the token uses `Host.CreateClient(...)` directly.
- The handler exists only in `Api.Tests`; the production host never registers it.

### Test data

`TestData` in `PersonalFinance.Testing` ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)): one extension method per entity on `TestDatabase`, saving through the domain and EF Core as the user, with every value optional — a test names only what it checks.

```csharp
var account = await Database.CreateAccountAsync(userId, name: "Carta", initialBalance: -50m, cancellationToken: Ct);
```

- Default names are unique (`TestData.UniqueName`), so several calls never collide on unique indexes.
- Available: `CreateAccountAsync`, `CreateCategoryAsync` (a subcategory with `parent:`, which also gives the kind), `CreateBudgetAsync`, `CreateIncomeAsync` / `CreateExpenseAsync` (in a new category of the right kind unless one is given, dated `TestData.DefaultDate`), `CreateTransferAsync`. Each block adds what its entities need (tags). Persistence tests keep `SampleData` for their "one of everything" scenario.

`ApiRequests` (in `Api.Tests/Support`) sends and reads like the app: `ReadAsync<T>()` and `GetJsonAsync<T>()` with the API JSON options (enumerations as strings), `PutWithVersionAsync` / `DeleteWithVersionAsync` with `If-Match`, `PostEmptyAsync` for actions. Request bodies are anonymous objects or dictionaries with the JSON names (`type = "checking"`), so tests check the contract the app sees, not the C# records.

### Mechanism tests

`EndpointMappingTests` start a minimal host (`WebApplication` + `TestServer`, no database) with `AddApiSecurity()` and sample endpoints declared in the test, to check what the real API cannot show yet: module prefix and tag, module conventions, `401` on protected endpoints and on unknown routes.

`ErrorHandlingTests` start a similar host (Production environment, no details in errors) with the class database, `AddApiErrors()`, `AddApiSecurity()`, a fixed current user and sample endpoints that throw domain errors, create and rename accounts, and simulate a write from another device between the version check and the save. They cover every row of the error codes table that does not need a real resource, versions (`ETag`, `428`, `400`, `412`), duplicates and other users' data. From block 4 the real resources reuse exactly these mechanisms, so their tests only check the codes of their own cases.

`EndpointDiscoveryTests` call the discovery directly with lists of sample types: grouping and order, modules without endpoints, abstract types, and every mistake that must stop startup. They also check that the API assembly is discovered without errors and that every module has at least one endpoint.

### Authentication and startup tests

- `AuthTests`, `RegistrationTests`: the real session cookie end to end — see [security.md](security.md#tests). One of them starts a second host on the same database and reuses the session cookie: Data Protection keys are shared through the database.
- `StartupTests`: each test clones its own database (`Server.CreateDatabaseAsync`) and starts a Production host on it, to check what happens with no users, with existing users and with invalid settings. A failed startup is read from the exception chain of the host.
- `AuthOptionsValidatorTests`: unit tests of the startup validation of the `Auth` section.

### Host tests

`ApiHostTests` run on the real host: health public, unknown route `401` with a ProblemDetails body, OpenAPI document and Scalar in Development (schema names prefixed by slice, enum values as strings), absent in Production, `HttpCurrentUser` registered, the **list of public routes** — any endpoint with `AllowAnonymous()` outside the list fails the test — and **`If-Match` on every `PUT` / `PATCH` / `DELETE`**.

### Mapping tests

- `ErrorMappingTests` (unit): every constant of `DomainErrors` has a status family and nothing else is mapped; status → default code; entity tag format and parsing (weak tags, `*`, lists, overflow rejected).
- `UniqueConstraintErrorsTests`: reads the unique indexes of user tables from `pg_indexes` and compares them with the mapped and internal lists — a new unique index in a migration fails the test until it has a code.

### Architecture tests

ArchUnitNET (`TngTech.ArchUnitNET.xUnitV3`) loads the compiled `Api`, `Domain`, `Infrastructure` assemblies and the test assembly once (`Solution.Model`).

| Test | Rule |
|---|---|
| `ProjectDependencyTests.Domain_DependsOnNoOtherProjectAndNoFramework` | `Domain` uses nothing from `Api`, `Infrastructure`, EF Core, ASP.NET Core, Npgsql (ADR-0002) |
| `ProjectDependencyTests.Infrastructure_DoesNotDependOnApi` | Persistence is reusable by other hosts |
| `ApiRulesTests.Endpoints_AreSealedInternalClassesOfAFeature` | Every `IEndpoint` is `sealed`, not `public`, in `PersonalFinance.Api.Features.*` |
| `ApiRulesTests.Modules_AreSealedClassesOfAFeature` | Every `ApiModule` is `sealed`, in `PersonalFinance.Api.Features.*` |
| `ApiRulesTests.Api_NeverBypassesTheUserScopeFilter` | No call to `IgnoreQueryFilters` in `Api` |
| `ApiRulesTests.IgnoreQueryFiltersRule_DetectsACall` | The rule above fails on a known call in the test assembly, so it cannot pass by not seeing calls |

The `IgnoreQueryFilters` rule matches called methods by declaring type and name (`FollowCustomCondition`), not with `NotCallAny(MethodMembers()…)`: ArchUnitNET records a call to a generic method (`IgnoreQueryFilters<Account>`) with the signature seen at the call site, which never equals the member loaded from EF Core, so `NotCallAny` passed even with a violation. `IgnoreQueryFiltersRule_DetectsACall` caught exactly this on the first run.

The endpoint and module rules use `WithoutRequiringPositiveResults()` because the API has no module yet; they apply from the first one. The `IgnoreQueryFilters` rule is also enforced at compile time by the banned API analyzer ([security.md](security.md#banned-apis)).

ArchUnitNET reads the compiled binaries: run the tests in Debug (`dotnet test` default).

### Minimum per endpoint

From block 4, following the Definition of Done ([ADR-0006](../architecture/adr/0006-testing-strategy.md)):

1. **Success**: status, body and, for writes, the saved data.
2. **`401` without a session**.
3. **Validation error** if the endpoint accepts input: `400` and the field in `errors`.
4. **Isolation**: user B neither sees nor changes user A's data — `404` `resource.not_found` for a route id, `400` `{resource}.not_found` for an id in the body ([ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)).
5. **Versions**, for updates and deletes: `412` with a stale `If-Match`, nothing changed (`428` and malformed headers are covered once by `ErrorHandlingTests`; resources repeat `428` as a cheap check that the slice takes the version).

Resource tests sign in with `ClientFor(userId)` and create data with `TestData`; the antiforgery token is handled by the client. `AccountsTests` is the reference for the next resources: one class per module, one region per endpoint, tests named `Endpoint_Situation_Outcome`.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| One API host per test class on the class database, cached by the assembly fixture | Same per-class isolation as persistence tests; one host startup per class, not per test | [ADR-0011](../architecture/adr/0011-persistence-testing.md), [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Connection string as last configuration source, read lazily by `Program.cs` | Tests can never hit the development database | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Mechanism tests on a minimal host with sample endpoints | The foundation is tested before any real endpoint exists | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Mapping tables checked against their source (`DomainErrors`, PostgreSQL unique indexes, endpoints with `PUT` / `PATCH` / `DELETE`) | A new code, index or endpoint cannot be forgotten | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md) |
| ArchUnitNET for architecture tests, each "forbidden call" rule checked against a known violation | Rules on types and method calls; a rule that sees nothing cannot pass silently | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Test authentication handler for resource tests, real login for authentication tests | Fast resource tests; the real cookie, lockout, rate limit and CSRF still tested | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md) |
| Test clients with HTTPS, cookies and antiforgery | Tests see the API as the browser app does | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md) |
| Factory methods with optional parameters for test data (`TestData`) | Less ceremony than builders, faster than creating data through the API | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md) |

## Tests

This document describes tests; it is verified by `dotnet test` passing locally and, once the pipeline exists, in CI ([devops](../devops/README.md)).

## Scalability

- **Many endpoint tests**: hosts are per class, databases per class, classes run in parallel; nothing is reset.
- **Worker**: `Worker.Tests` reuses `Testing` the same way.
- **Module boundaries**: new ArchUnitNET rules (a module does not use another module's internals) when modules become explicit.
- **Slower suite**: split `Api.Tests` by module or run architecture tests in a separate CI job.

## To do / open questions

- ✅ How tests authenticate, authenticated client helper, test data ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)).
- ✅ Expected status for another user's data: `404` / `400` `{resource}.not_found` ([ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)).
- ✅ Startup tests: `StartupTests`, each on a database of its own (`Server.CreateDatabaseAsync`).
- `[DevOps]` CI job with Docker for `Api.Tests`.

---
_Last updated: 2026-11-12_
