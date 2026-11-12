# Backend structure

## Scope

Organization of the .NET solution: projects and the dependencies between them, how a feature is organized, how endpoints are defined and registered, where shared code lives and how duplication is kept under control.

## Current state

Decided ([ADR-0002](adr/0002-backend-structure.md), [ADR-0003](adr/0003-minimal-api.md), [ADR-0012](adr/0012-api-foundation.md)). **Solution created** (`[DevOps]`), **data model implemented** (`[DB]`), **API foundation implemented** (`[Backend]`): endpoint discovery, fallback policy, HTTP current user, validation, OpenAPI. Resource endpoints follow block by block ([backend/README.md](../backend/README.md)).

### Solution layout

```
backend/
├── PersonalFinance.slnx
├── global.json                       # .NET SDK 10, Microsoft Testing Platform for dotnet test
├── Directory.Build.props             # net10.0, nullable, implicit usings, code style in build, warnings as errors in CI
├── Directory.Packages.props          # package versions (Central Package Management)
├── src/
│   ├── Api/                          # PersonalFinance.Api
│   │   ├── Features/                 # one folder per module, one file (or folder) per use case
│   │   │   ├── Accounts/
│   │   │   │   ├── CreateAccount.cs
│   │   │   │   ├── GetAccounts.cs
│   │   │   │   └── …
│   │   │   ├── Categories/
│   │   │   ├── Transactions/
│   │   │   └── Budgets/
│   │   ├── Common/                   # cross-cutting HTTP concerns: Endpoints/ (IEndpoint, ApiModule, discovery),
│   │   │                             # Security/ (cookie, fallback policy, HttpCurrentUser), OpenApi/; errors and paging next
│   │   └── Program.cs
│   ├── Domain/                       # PersonalFinance.Domain
│   │   ├── Accounts/ …               # entities, value objects, business rules, per module
│   │   └── Common/                   # base types (e.g. owned-by-user marker, auditable entity)
│   └── Infrastructure/               # PersonalFinance.Infrastructure
│       ├── Persistence/              # AppDbContext, entity configurations, interceptors, migrations
│       └── Identity/                 # ApplicationUser, Identity setup, initial user seeding (ADR-0001)
└── tests/                            # see testing-strategy.md (ADR-0006)
    ├── Domain.Tests/                 # PersonalFinance.Domain.Tests — unit tests on business rules
    ├── Infrastructure.Tests/         # PersonalFinance.Infrastructure.Tests — persistence tests, real PostgreSQL (ADR-0011)
    ├── Api.Tests/                    # PersonalFinance.Api.Tests — integration tests, real PostgreSQL via Testcontainers
    ├── Architecture.Tests/           # PersonalFinance.Architecture.Tests — dependency rules of this document
    └── Testing/                      # PersonalFinance.Testing — shared test support (PostgreSQL fixture), not a test project
```

Projects and root namespaces are `PersonalFinance.<Project>` ([ADR-0009](adr/0009-repository-workflow.md)); folders keep short names. Each project has an assembly marker class used by tests and reflection (`ApiAssembly`, `DomainAssembly`, `InfrastructureAssembly`).

### Dependencies

```
Api ───────────► Infrastructure ───► Domain
 └──────────────────────────────────► Domain
(future) Worker ► Infrastructure / Domain
```

- `Domain` depends on **nothing**: no EF Core, no ASP.NET Core. Mapping to the database is done with Fluent API configurations in `Infrastructure`.
- Project references enforce the direction: `Domain` cannot reach the database or HTTP by accident.

### What goes where

| Code | Project | Example |
|---|---|---|
| Entities, invariants, rules that protect money correctness or are used by more than one feature | `Domain` | a transfer creates two linked movements; account balance; budget progress |
| Persistence, Identity, integrations with the outside world | `Infrastructure` | `AppDbContext`, entity configurations, migrations, interceptors |
| A use case: request/response, validation, query, orchestration | `Api/Features/<Module>/<Slice>` | `CreateTransaction`, `GetMonthlyBudgets` |
| Cross-cutting HTTP concerns | `Api/Common` | ProblemDetails handling, validation filter, pagination |

Slices use `AppDbContext` **directly**: no repository layer on top of EF Core, which already is a repository and unit of work.

### Endpoints (Minimal API)

Endpoints use **native ASP.NET Core Minimal API**, with no third-party endpoint library ([ADR-0003](adr/0003-minimal-api.md)).

- **One endpoint per slice**, defined in the slice file next to its request, response and handling.
- Each endpoint implements a small project-owned interface (`IEndpoint`) and is **discovered automatically at startup**: adding a slice never requires touching `Program.cs`.
- **One route group per module** sets, once, the prefix (`/api/<module>`), authorization and OpenAPI tag.
- Handlers return `TypedResults` (with `Results<…>` when there are several outcomes): explicit responses, accurate OpenAPI metadata, handlers testable as plain methods.
- Cross-cutting behavior uses **endpoint filters** (e.g. validation) applied at group level.
- The OpenAPI document is generated by the built-in ASP.NET Core support.

Shape of a slice, as implemented in `[Backend]` ([ADR-0012](adr/0012-api-foundation.md), full rules in [backend/README.md](../backend/README.md#endpoints)):

```csharp
// Api/Features/Accounts/AccountsModule.cs — one per module: prefix and OpenAPI tag
internal sealed class AccountsModule : ApiModule
{
    public override string Prefix => "accounts";
    public override string Tag => "Accounts";
}

// Api/Features/Accounts/CreateAccount.cs
public static class CreateAccount
{
    public sealed record Request(string Name, string Currency, decimal InitialBalance);
    public sealed record Response(Guid Id);

    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) =>   // the /api/accounts group
            module.MapPost("", Handle).WithName(nameof(CreateAccount));
    }

    internal static async Task<Created<Response>> Handle(
        Request request, AppDbContext db, CancellationToken ct)
    {
        // create the entity, save, return TypedResults.Created(...)
    }
}
```

### Keeping duplication under control

Principle: **share mechanisms and business rules, accept duplication of shapes.** Request/response records and small queries that look alike between slices may stay duplicated: they change for different reasons. Logic is never duplicated. Extract a shared helper at the third occurrence, when the copies change for the same reason (*rule of three*).

Mechanisms written once and applied everywhere:

| Repetition avoided | Mechanism | Where |
|---|---|---|
| Filtering every query by the current user | EF Core global query filters on owned entities | `Infrastructure` |
| Setting `UserId`, `CreatedAt`, `UpdatedAt` on save | `SaveChanges` interceptor | `Infrastructure` |
| Route prefix, authorization, OpenAPI tags per module | One endpoint group per module | `Api/Features/<Module>` |
| Registering each endpoint by hand | Automatic discovery of endpoint definitions | `Api/Common` |
| Validating each request | One generic validation step in the pipeline | `Api/Common` |
| Mapping errors to HTTP responses | Global exception handler → ProblemDetails | `Api/Common` |
| Paging and sorting lists | Shared paging request/result + query extensions | `Api/Common` |
| "Find by id or 404" | Small query/endpoint helpers | `Api/Common` |
| Business rules (transfers, balances, budgets) | Methods on domain entities / domain services | `Domain` |

### Adding a feature

1. Create the slice in `Api/Features/<Module>/` (request, response, validation, handling, endpoint).
2. If it introduces a business rule, put it in `Domain` and unit test it there.
3. If it needs schema changes, add configuration and a migration in `Infrastructure`.
4. Update [backend/api-endpoints.md](../backend/api-endpoints.md) and, if needed, [database/domain-model.md](../database/domain-model.md).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Three projects: `Api` with vertical slices, `Domain`, `Infrastructure` | Low ceremony per feature, one protected home for business rules, persistence reusable by future hosts | [ADR-0002](adr/0002-backend-structure.md) |
| Slices grouped by module | Modules can later become explicit boundaries (modular monolith) without rewrites | [ADR-0002](adr/0002-backend-structure.md) |
| No repository layer over EF Core | Avoids interfaces and boilerplate with no benefit for this domain | [ADR-0002](adr/0002-backend-structure.md) |
| Native Minimal API, one endpoint per slice, automatic registration, one route group per module | Each slice owns its endpoint; no external dependency; consistent with the direction of ASP.NET Core | [ADR-0003](adr/0003-minimal-api.md) |
| Projects named `PersonalFinance.*`, short folders, `.slnx`, Central Package Management | Follows the repository name; one place for package versions | [ADR-0009](adr/0009-repository-workflow.md) |

## Tests

- `Architecture.Tests` checks with ArchUnitNET that `Domain` depends on no other project and neither EF Core, ASP.NET Core nor Npgsql, that `Infrastructure` does not depend on `Api`, that endpoints and modules live in `Features` and are sealed, and that `Api` never calls `IgnoreQueryFilters` ([backend/testing.md](../backend/testing.md#architecture-tests)).
- Endpoint discovery and module groups are tested in `Api.Tests` (`EndpointDiscoveryTests`, `EndpointMappingTests`).
- Slices are covered by API integration tests and business rules by domain unit tests ([testing-strategy.md](testing-strategy.md)).

## Scalability

- **More users / more data**: independent of this structure. Stateless API scaled horizontally behind nginx (requires shared Data Protection keys, see [authentication.md](authentication.md)); indexes, pagination and caching on the database side.
- **More features**: one new slice per use case, one new module folder per area; existing code is rarely touched.
- **Background processing** (recurring transactions, CSV import): a `Worker` project referencing `Domain` and `Infrastructure`, deployed as its own container.
- **Growing codebase**: modules become explicit boundaries — a module folder in each project, `internal` visibility, architecture tests; a per-module `DbContext` or schema only if really needed (new ADR).
- **More contributors**: dependency direction enforced by project references, architecture tests, a slice template, this document as the guide.
- **Microservices**: not planned; they would require their own ADR.

## To do / open questions

- ✅ `[Backend]` `IEndpoint` discovery: `IEndpoint<TModule>` + `ApiModule`, reflection scan at startup (Native AOT is not a goal; a source generator only if that changes) — [ADR-0012](adr/0012-api-foundation.md).
- ✅ `[Backend]` Authorization fallback policy requiring authentication, `AllowAnonymous` only on listed public routes — [backend/security.md](../backend/security.md).
- ✅ `[Backend]` OpenAPI UI: Scalar, Development only — [ADR-0012](adr/0012-api-foundation.md).
- ✅ `[Backend]` Validation: built-in .NET 10 validation — [backend/validation-and-errors.md](../backend/validation-and-errors.md).
- `[Backend]` Error model of slices and HTTP mapping (block 2). The domain throws `DomainException` with a stable code ([ADR-0010](adr/0010-data-model-and-user-isolation.md)).
- ✅ `[Backend]` Mapping: manual projections and static mapping methods, no library — [ADR-0012](adr/0012-api-foundation.md).
- ✅ `[Backend]` No mediator: the endpoint calls the slice directly; reconsidered when the `Worker` arrives — [ADR-0012](adr/0012-api-foundation.md).
- ✅ `[DB]` Query filter and `SaveChanges` interceptor — [database/user-isolation.md](../database/user-isolation.md).
- ✅ Architecture-test library: ArchUnitNET — [ADR-0012](adr/0012-api-foundation.md).
- When the `Worker` arrives: where orchestration shared by API and `Worker` lives (new ADR).
- Nice to have: a `dotnet new` template that scaffolds a slice.

---
_Last updated: 2026-11-12_
