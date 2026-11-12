# Backend testing

## Scope

How the API is tested: the test projects involved, how tests start the API on a real PostgreSQL, tests of the endpoint mechanism, host tests, architecture tests, and the minimum required for every endpoint. Domain and persistence tests are in [database/testing.md](../database/testing.md); the overall strategy in [architecture/testing-strategy.md](../architecture/testing-strategy.md) ([ADR-0006](../architecture/adr/0006-testing-strategy.md)).

## Current state

Implemented for the foundation ([ADR-0011](../architecture/adr/0011-persistence-testing.md), [ADR-0012](../architecture/adr/0012-api-foundation.md)).

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

- `ApiFactory` runs the real `Program.cs` (same services and pipeline) and adds the class database's connection string as the **last configuration source**: it wins over `appsettings.Development.json` and environment variables, so tests never touch the development database.
- `Program.cs` reads the connection string when the first `DbContext` is created, not while building the host, so the value from the factory is the one used.
- Endpoint test classes derive from `ApiTest`:

  ```csharp
  public sealed class AccountsTests(ApiServer server) : ApiTest(server)
  {
      [Fact]
      public async Task List_Returns401WithoutSession()
      {
          var response = await AnonymousClient().GetAsync("/api/accounts", Ct);
          Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
      }
  }
  ```

- A test that needs another environment creates its own host: `await using var production = new ApiFactory(Database, Environments.Production);`.
- `PostgresServerFixture.DisposeDependentsAsync` lets `ApiServer` dispose the hosts before the container stops.

### Mechanism tests

`EndpointMappingTests` start a minimal host (`WebApplication` + `TestServer`, no database) with `AddApiSecurity()` and sample endpoints declared in the test, to check what the real API cannot show yet: module prefix and tag, module conventions, `401` on protected endpoints and on unknown routes.

`EndpointDiscoveryTests` call the discovery directly with lists of sample types: grouping and order, modules without endpoints, abstract types, and every mistake that must stop startup. They also check that the API assembly is discovered without errors and that every module has at least one endpoint.

### Host tests

`ApiHostTests` run on the real host: health public, unknown route `401`, OpenAPI document and Scalar in Development, absent in Production, `HttpCurrentUser` registered, and the **list of public routes** — any endpoint with `AllowAnonymous()` outside the list fails the test.

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
4. **Isolation**: user B neither sees nor changes user A's data (status decided in block 2).

How tests sign in and the test data builders are decided in block 3.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| One API host per test class on the class database, cached by the assembly fixture | Same per-class isolation as persistence tests; one host startup per class, not per test | [ADR-0011](../architecture/adr/0011-persistence-testing.md), [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Connection string as last configuration source, read lazily by `Program.cs` | Tests can never hit the development database | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Mechanism tests on a minimal host with sample endpoints | The foundation is tested before any real endpoint exists | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| ArchUnitNET for architecture tests, each "forbidden call" rule checked against a known violation | Rules on types and method calls; a rule that sees nothing cannot pass silently | [ADR-0012](../architecture/adr/0012-api-foundation.md) |

## Tests

This document describes tests; it is verified by `dotnet test` passing locally and, once the pipeline exists, in CI ([devops](../devops/README.md)).

## Scalability

- **Many endpoint tests**: hosts are per class, databases per class, classes run in parallel; nothing is reset.
- **Worker**: `Worker.Tests` reuses `Testing` the same way.
- **Module boundaries**: new ArchUnitNET rules (a module does not use another module's internals) when modules become explicit.
- **Slower suite**: split `Api.Tests` by module or run architecture tests in a separate CI job.

## To do / open questions

- Block 3: how tests authenticate (real login through the API or a test authentication handler), authenticated `HttpClient` helper, test data builders for endpoints.
- Block 2: expected status for another user's data, used by every isolation test.
- Block 3: startup tests — no users and no `Auth__InitialUser__*` → startup fails; the initial user gets the default categories; variables ignored once a user exists.
- `[DevOps]` CI job with Docker for `Api.Tests`.

---
_Last updated: 2026-11-12_
