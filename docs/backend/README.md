# Backend API

## Scope

The HTTP API of the application (`backend/src/Api`): how endpoints are written and registered, their contracts, validation and errors, authentication and authorization, and the tests of the API. The structure of the solution is in [architecture/backend-structure.md](../architecture/backend-structure.md); entities, rules and persistence in [database/](../database/README.md).

## Micro categories

| Document | Covers | Status |
|---|---|---|
| [api-endpoints.md](api-endpoints.md) | Conventions of the API, versions (`ETag` / `If-Match`) and the contract of every endpoint, one section per resource | Conventions, authentication and accounts implemented; other resources to come |
| [validation-and-errors.md](validation-and-errors.md) | Request validation, error responses and codes, domain errors, duplicates, references | Implemented — ADR-0012, ADR-0013, ADR-0015 |
| [security.md](security.md) | Session cookie, password and lockout, login, CSRF, rate limiting, fallback policy, public endpoints, current user, other users' data, initial user, Data Protection keys, banned APIs | Implemented — ADR-0001, ADR-0012, ADR-0013, ADR-0014 |
| [testing.md](testing.md) | Test projects, API hosts on Testcontainers, authentication in tests, test data, mechanism, startup, host and architecture tests, minimum per endpoint | Implemented — ADR-0006, ADR-0011, ADR-0012, ADR-0013, ADR-0014, ADR-0015 |

## Current state

The API is built in blocks, one branch and one pull request each (GitHub Flow, [ADR-0009](../architecture/adr/0009-repository-workflow.md)):

| # | Block | Branch | Status |
|---|---|---|---|
| 1 | Foundation: endpoint discovery, fallback policy, current user, validation, OpenAPI, architecture tests, API test hosts | `feat/backend-api-foundation` | Merged (#10) |
| 2 | Errors: `DomainException` → ProblemDetails, duplicates, versions with `ETag` / `If-Match`, cross-user access | `feat/backend-errors` | Merged (#11) |
| 3 | Authentication: antiforgery, login, logout, me, change password, registration, rate limiting, initial user, Data Protection keys, test authentication | `feat/backend-auth` | Merged (#12) |
| 4 | Accounts, plus the conventions of every resource: enums as strings, amounts as numbers, list envelope, schema names | `feat/backend-accounts` | Merged (#13) |
| 5 | Categories | `feat/backend-categories` | To do |
| 6 | Transactions (with tags and filters) | `feat/backend-transactions` | To do |
| 7 | Transfers | `feat/backend-transfers` | To do |
| 8 | Budgets | `feat/backend-budgets` | To do |
| 9 | Dashboard | `feat/backend-dashboard` | To do |

### Pipeline

`Program.cs` stays short and does not change when features are added:

```
AddPersistence (connection string read from the final configuration)
AddApiErrors          → ProblemDetails with code + traceId, ApiExceptionHandler
AddApiJson            → enumerations as camelCase strings (JsonConventions)
AddApiSecurity        → Auth options, Identity + session cookie (401/403, no redirects), Data Protection keys in the DB,
                        antiforgery, login rate limiter, fallback policy, HttpCurrentUser
AddValidation         → built-in Minimal API validation
AddApiDocumentation   → built-in OpenAPI document, schema names prefixed by slice
migrations → initial user → UseApiErrors (exception handler, status code pages)
           → UseApiSecurity (authentication, authorization, rate limiter, antiforgery)
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
    public sealed record Request(
        [Required, MaxLength(Account.NameMaxLength)] string Name,
        [Required] AccountType? Type,
        [Required, Length(3, 3)] string Currency,
        [Required] decimal? InitialBalance);

    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) =>
            module.MapPost("", Handle).WithName(nameof(CreateAccount));
    }

    internal static async Task<Created<AccountResponse>> Handle(
        Request request, AppDbContext db, ICurrentUser currentUser, HttpResponse response, CancellationToken ct)
    {
        var account = new Account(currentUser.RequiredUserId(), request.Name, request.Type!.Value, request.Currency, request.InitialBalance!.Value);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);

        var created = await db.FindAccountResponseAsync(account.Id, ct);   // the same projection as reads
        response.SetVersion(created!.Version);
        return TypedResults.Created($"/api/accounts/{account.Id}", created);
    }
}
```

The real slices are in `Api/Features/Accounts`: the reference for the next resources.

Rules:

- **Routes**: `""` for the collection, `"{id:guid}"` for one item. The prefix `/api/{module}` comes from the group.
- **No authorization code in slices**: the fallback policy protects everything. Only a public endpoint calls `AllowAnonymous()`, and it must be added to the list of public routes ([security.md](security.md#public-endpoints)).
- **Request and response records are `public`** (the validation generator skips other types); the endpoint class is `internal sealed` and lives in `PersonalFinance.Api.Features.*` (architecture test).
- **Handlers return `TypedResults`** (`Results<…>` for several outcomes) for exact OpenAPI metadata.
- **Mapping is manual**: one response type per resource and one `Select` projection for it (`AccountQueries.SelectResponses`), used by reads and, after saving, by writes. No mapping library.
- **Lists return `ListResponse<T>`** (`{ "items": [...] }`); enumerations travel as `camelCase` strings ([ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md)).
- **State changes that cannot lose an edit** (archive, unarchive) are idempotent `POST /{id}/{action}` without `If-Match`; everything that replaces or deletes data takes the version.
- **No mediator**: the endpoint calls `Handle` directly. Cross-cutting behavior goes in endpoint filters on the module (`ApiModule.Configure`).
- **Thin slices**: every business rule lives in `Domain`, so the future `Worker` and the CSV import apply the same rules without calling the API.
- **Never `IgnoreQueryFilters`** in `Api`: it does not compile (banned API) and an architecture test checks it again.
- **Errors are thrown or returned, never formatted by hand**: the domain throws `DomainException`; `GetReferenceAsync` checks ids in the body; `TypedResults.NotFound()` for a route id; duplicates are left to the database. The response shape comes from one handler ([validation-and-errors.md](validation-and-errors.md)).
- **Updates and deletes take `IfMatch`** and call `db.EnsureVersion(entity, ifMatch)`; single reads and writes send the version with `response.SetVersion(...)` ([api-endpoints.md](api-endpoints.md#versions)).

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
| 2026-11-12 | One global `IExceptionHandler`; ProblemDetails with stable `code` and `traceId` for every error | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md), [validation-and-errors.md](validation-and-errors.md) |
| 2026-11-12 | Domain codes by family: `400` invalid values, `409` state conflicts, `500` programming errors | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md) |
| 2026-11-12 | Duplicates from unique indexes (`23505`) → `409` `*.name_taken` | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md) |
| 2026-11-12 | Versions with `ETag` / mandatory `If-Match`: `428` / `400` / `412` | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md), [api-endpoints.md](api-endpoints.md#versions) |
| 2026-11-12 | Another user's data: `404` for route ids, `400` `{resource}.not_found` for ids in the body | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md), [security.md](security.md#another-users-data) |
| 2026-11-12 | Custom auth endpoints (`antiforgery`, `login`, `logout`, `me`, `change-password`), registration mapped only when enabled | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md), [api-endpoints.md](api-endpoints.md#authentication) |
| 2026-11-12 | `SameSite=Strict` + antiforgery tokens on every unsafe request; password ≥ 12, lockout 5 / 15 min, sliding 14-day cookie; rate limiter on login and registration; Data Protection keys in the database | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md), [security.md](security.md) |
| 2026-11-12 | Tests: test authentication handler for resources, real login for authentication; `TestData` factories | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md), [testing.md](testing.md#authentication-in-tests) |
| 2026-11-12 | Resource conventions: enums as `camelCase` strings, amounts as JSON numbers, list envelope `{ items }`, one response type per resource, schema names prefixed by slice, `request.invalid` for unreadable requests | [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md), [api-endpoints.md](api-endpoints.md#conventions) |
| 2026-11-12 | Accounts: balance in SQL at every read, `PUT` of the whole account, archive / unarchive actions, delete only without movements (`account.has_movements`), archived hidden unless `includeArchived=true` | [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md), [api-endpoints.md](api-endpoints.md#accounts) |

## Tests

Summary of [testing.md](testing.md):

- discovery rules (unit), module groups and the fallback policy on a minimal host, the real host on PostgreSQL (public routes, development tools by environment, current user, `If-Match` on every update and delete);
- error responses, duplicates, versions and other users' data on a host with sample endpoints over a real database; mapping tables checked against `DomainErrors` and the unique indexes of PostgreSQL;
- architecture rules with ArchUnitNET, with a check that the `IgnoreQueryFilters` rule really detects a call;
- from block 4 on, every endpoint: success, `401` without session, validation error, isolation between users, versions on writes — `AccountsTests` is the first and the reference.

## Scalability

- **More features**: one slice per use case, one module class per new folder; `Program.cs` never changes.
- **Modular monolith**: modules already have an identity in code (`IEndpoint<TModule>`); ArchUnitNET can forbid a module from using another module's internals.
- **Worker** (recurring transactions, CSV import): reuses `Domain` and `Infrastructure` with `FixedCurrentUser`. Slices are not reachable from it, which is why rules never live in slices. Where shared orchestration lives is decided with its own ADR; a mediator is reconsidered then.
- **More instances / other clients**: the API keeps no state; the current user comes from claims, so bearer tokens for native clients work without changes (new ADR, [authentication.md](../architecture/authentication.md#scalability)).
- **More data** (exports, charts): projections and SQL aggregation; streaming responses for exports.

## To do / open questions

- ✅ Block 2 — error mapping, concurrency and cross-user access: [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md).
- ✅ Block 3 — authentication: [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md).
- ✅ Block 4 — accounts and resource conventions: [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md).
- Block 6 — signed amounts or kind + positive amount in the API; client-generated ids or an idempotency key so an expense sent again by the PWA after going offline is not saved twice.
- ✅ Validation of request records is tested (login).
- Nice to have: a `dotnet new` template that scaffolds a slice.

---
_Last updated: 2026-11-12_
