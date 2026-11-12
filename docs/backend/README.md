# Backend API

## Scope

The HTTP API of the application (`backend/src/Api`): how endpoints are written and registered, their contracts, validation and errors, authentication and authorization, and the tests of the API. The structure of the solution is in [architecture/backend-structure.md](../architecture/backend-structure.md); entities, rules and persistence in [database/](../database/README.md).

## Micro categories

| Document | Covers | Status |
|---|---|---|
| [api-endpoints.md](api-endpoints.md) | Conventions of the API and the contract of every endpoint, one section per resource | Conventions decided; health and development tools implemented; resources to come |
| [validation-and-errors.md](validation-and-errors.md) | Request validation, error responses, domain errors, concurrency | Validation decided — ADR-0012; error mapping open (block 2) |
| [security.md](security.md) | Authentication cookie, fallback policy, public endpoints, current user, banned APIs, auth endpoints | Foundation implemented — ADR-0001, ADR-0012; auth endpoints open (block 3) |
| [testing.md](testing.md) | Test projects, API hosts on Testcontainers, mechanism and host tests, architecture tests, minimum per endpoint | Implemented for the foundation — ADR-0006, ADR-0011, ADR-0012 |

## Current state

The API is built in blocks, one branch and one pull request each (GitHub Flow, [ADR-0009](../architecture/adr/0009-repository-workflow.md)):

| # | Block | Branch | Status |
|---|---|---|---|
| 1 | Foundation: endpoint discovery, fallback policy, current user, validation, OpenAPI, architecture tests, API test hosts | `feat/backend-api-foundation` | In review |
| 2 | Errors: `DomainException` → ProblemDetails, concurrency, cross-user access | `feat/backend-errors` | To do |
| 3 | Authentication: login, logout, me, change password, CSRF, rate limiting, initial user, Data Protection | `feat/backend-auth` | To do |
| 4 | Accounts | `feat/backend-accounts` | To do |
| 5 | Categories | `feat/backend-categories` | To do |
| 6 | Transactions (with tags and filters) | `feat/backend-transactions` | To do |
| 7 | Transfers | `feat/backend-transfers` | To do |
| 8 | Budgets | `feat/backend-budgets` | To do |
| 9 | Dashboard | `feat/backend-dashboard` | To do |

### Pipeline

`Program.cs` stays short and does not change when features are added:

```
AddPersistence (connection string read from the final configuration)
AddApiSecurity        → Identity cookie (401/403, no redirects), fallback policy, HttpCurrentUser
AddValidation         → built-in Minimal API validation
AddApiDocumentation   → built-in OpenAPI document
migrations → UseAuthentication → UseAuthorization
/api/health (public) · /openapi + /scalar (Development only) · MapApiEndpoints()
```

### Endpoints

Every endpoint belongs to a **module** ([ADR-0012](../architecture/adr/0012-api-foundation.md)). A module is one class per folder of `Features`:

```csharp
// Api/Features/Accounts/AccountsModule.cs
internal sealed class AccountsModule : ApiModule
{
    public override string Prefix => "accounts";   // → /api/accounts
    public override string Tag => "Accounts";       // OpenAPI tag
}
```

A slice declares its endpoint with `IEndpoint<TModule>`; startup finds it and maps it on the group of its module:

```csharp
// Api/Features/Accounts/CreateAccount.cs
public static class CreateAccount
{
    public sealed record Request([Required, MaxLength(100)] string Name, [Required] string Currency, decimal InitialBalance);
    public sealed record Response(Guid Id);

    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) =>
            module.MapPost("", Handle).WithName(nameof(CreateAccount));
    }

    internal static async Task<Created<Response>> Handle(Request request, AppDbContext db, CancellationToken ct)
    {
        // domain constructor, SaveChanges, TypedResults.Created(...)
    }
}
```

Rules:

- **Routes**: `""` for the collection, `"{id:guid}"` for one item. The prefix `/api/{module}` comes from the group.
- **No authorization code in slices**: the fallback policy protects everything. Only a public endpoint calls `AllowAnonymous()`, and it must be added to the list of public routes ([security.md](security.md#public-endpoints)).
- **Request and response records are `public`** (the validation generator skips other types); the endpoint class is `internal sealed` and lives in `PersonalFinance.Api.Features.*` (architecture test).
- **Handlers return `TypedResults`** (`Results<…>` for several outcomes) for exact OpenAPI metadata.
- **Mapping is manual**: `Select` projections for reads (only the needed columns reach the API), a small static method for writes. No mapping library.
- **No mediator**: the endpoint calls `Handle` directly. Cross-cutting behavior goes in endpoint filters on the module (`ApiModule.Configure`).
- **Thin slices**: every business rule lives in `Domain`, so the future `Worker` and the CSV import apply the same rules without calling the API.
- **Never `IgnoreQueryFilters`** in `Api`: it does not compile (banned API) and an architecture test checks it again.

Mistakes in modules stop startup with the name of the type to fix: an endpoint with no module or two modules, an invalid prefix (lowercase words with hyphens only), two modules with the same prefix, a module with no tag.

## Decisions

| Date | Decision | Reference |
|---|---|---|
| 2026-11-12 | Endpoints implement `IEndpoint<TModule>`; modules are `ApiModule` classes with prefix and tag; reflection scan at startup | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| 2026-11-12 | Fallback policy requiring authentication; public endpoints listed and tested | [ADR-0012](../architecture/adr/0012-api-foundation.md), [security.md](security.md) |
| 2026-11-12 | `HttpCurrentUser` reads the `NameIdentifier` claim on every access | [ADR-0012](../architecture/adr/0012-api-foundation.md), [security.md](security.md#current-user) |
| 2026-11-12 | Built-in .NET 10 validation for the shape of requests; rules stay in `Domain` | [ADR-0012](../architecture/adr/0012-api-foundation.md), [validation-and-errors.md](validation-and-errors.md) |
| 2026-11-12 | Manual mapping, no mediator | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| 2026-11-12 | Built-in OpenAPI document and Scalar UI, Development only | [ADR-0012](../architecture/adr/0012-api-foundation.md), [api-endpoints.md](api-endpoints.md#openapi) |
| 2026-11-12 | ArchUnitNET for architecture tests; `IgnoreQueryFilters` banned in `Api` at compile time | [ADR-0012](../architecture/adr/0012-api-foundation.md), [testing.md](testing.md#architecture-tests) |

## Tests

Summary of [testing.md](testing.md):

- discovery rules (unit), module groups and the fallback policy on a minimal host, the real host on PostgreSQL (public routes, development tools by environment, current user);
- architecture rules with ArchUnitNET, with a check that the `IgnoreQueryFilters` rule really detects a call;
- from block 4 on, every endpoint: success, `401` without session, validation error, isolation between users.

## Scalability

- **More features**: one slice per use case, one module class per new folder; `Program.cs` never changes.
- **Modular monolith**: modules already have an identity in code (`IEndpoint<TModule>`); ArchUnitNET can forbid a module from using another module's internals.
- **Worker** (recurring transactions, CSV import): reuses `Domain` and `Infrastructure` with `FixedCurrentUser`. Slices are not reachable from it, which is why rules never live in slices. Where shared orchestration lives is decided with its own ADR; a mediator is reconsidered then.
- **More instances / other clients**: the API keeps no state; the current user comes from claims, so bearer tokens for native clients work without changes (new ADR, [authentication.md](../architecture/authentication.md#scalability)).
- **More data** (exports, charts): projections and SQL aggregation; streaming responses for exports.

## To do / open questions

- Block 2 — error mapping: codes of `DomainException` → status and ProblemDetails; `409` or `412` for concurrency conflicts and how the client sends the version (the PWA offline sync favors `ETag` / `If-Match`); `404` or `403` for another user's data.
- Block 3 — authentication: `MapIdentityApi` or custom endpoints; CSRF (antiforgery is also required by file uploads: CSV import, attachments); password, lockout and cookie values; login rate limiting; initial user and default categories; Data Protection keys; how tests authenticate; test data builders.
- Block 6 — signed amounts or kind + positive amount in the API; client-generated ids or an idempotency key so an expense sent again by the PWA after going offline is not saved twice.
- Validation of request records is tested with the first endpoint that accepts input (block 4).
- Nice to have: a `dotnet new` template that scaffolds a slice.

---
_Last updated: 2026-11-12_
