# API endpoints

## Scope

Conventions shared by every endpoint and the contract of each endpoint: route, authentication, request, responses. One section per resource. How endpoints are written is in [README.md](README.md#endpoints); errors in [validation-and-errors.md](validation-and-errors.md); authentication in [security.md](security.md).

## Current state

The foundation is implemented: health, development tools, authentication, the mechanism that maps every module ([ADR-0012](../architecture/adr/0012-api-foundation.md)), error responses and versions ([ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)). Resource endpoints arrive one block at a time; their contract is written here in the same pull request as the code.

### Conventions

| Topic | Convention |
|---|---|
| Base path | `/api`; each module adds its prefix: `/api/accounts`, `/api/categories`, … The Angular dev server and nginx proxy `/api` ([devops/local-development.md](../devops/local-development.md)) |
| Format | JSON, `camelCase` properties (ASP.NET Core default), enumerations as strings (decided with the first enum in an endpoint) |
| Authentication | Session cookie on every endpoint unless marked **public** ([security.md](security.md)); without a session `401` |
| Identifiers | `Guid` (UUIDv7) in routes: `/api/accounts/{id:guid}` |
| Amounts | JSON numbers with at most 2 decimals, never floats on the server (`decimal`); exposure of signs decided in block 6 |
| Dates | Transaction dates `YYYY-MM-DD` (`DateOnly`); timestamps ISO 8601 in UTC |
| Methods | `GET` read, `POST` create (`201` + `Location` + `ETag`), `PUT` replace (`200` + `ETag`), `DELETE` delete or archive (`204`) — final list per resource. `PUT`, `PATCH` and `DELETE` require `If-Match` ([Versions](#versions)) |
| Errors | ProblemDetails (RFC 9457) with a stable `code` and a `traceId`; validation errors `400` with `errors` by field ([validation-and-errors.md](validation-and-errors.md)) |
| Not found | `404` `resource.not_found` for a route id that is not the user's — another user's id included ([security.md](security.md#another-users-data)) |
| Lists | Paging and sorting decided with the first list that needs them (transactions) |
| OpenAPI | Every endpoint has a name (`WithName`) and its module tag; responses come from `TypedResults` |

### Endpoint template

Each endpoint is documented with this table, then request and response examples:

| | |
|---|---|
| **Route** | `METHOD /api/...` |
| **Auth** | Session / Public |
| **Request** | Body or query parameters, with validation rules |
| **Responses** | Status codes, codes and bodies |
| **Versions** | `ETag` returned / `If-Match` required |
| **Isolation** | What another user gets (`404`, or `400` `{resource}.not_found` for ids in the body) |
| **Tests** | Test class |

### Versions

Every resource has a version: PostgreSQL's `xmin` of its row ([database/conventions.md](../database/conventions.md#concurrency), [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)). It prevents lost updates when the phone and the computer change the same item.

| Where | How |
|---|---|
| Single resource (`GET /api/x/{id}`) and write responses (`POST`, `PUT`, `PATCH`) | Header `ETag: "12345"` — a strong tag, the version in quotes |
| Lists | Each item has `version` (number) in the body, so an item can be edited from a list without reading it again |
| `PUT`, `PATCH`, `DELETE` | Header `If-Match: "12345"` with the last version read — **mandatory** |

| Situation | Response |
|---|---|
| `If-Match` missing | `428` `concurrency.version_required` |
| `If-Match` malformed: `*`, weak tag `W/"…"`, several tags, no quotes | `400` `concurrency.version_invalid` |
| Version different from the current one, or the row changed between the check and the save | `412` `concurrency.version_mismatch` — nothing is changed |
| Resource not the user's | `404` before any version check |

In a slice:

```csharp
internal static async Task<Results<Ok<Response>, NotFound>> Handle(
    Guid id, Request request, IfMatch ifMatch, AppDbContext db, HttpResponse response, CancellationToken ct)
{
    var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct);
    if (account is null) return TypedResults.NotFound();

    db.EnsureVersion(account, ifMatch);          // 412 if the client read an older version
    account.Rename(request.Name);
    await db.SaveChangesAsync(ct);               // 412 if another write landed in between

    var version = RowVersion.Of(db, account);
    response.SetVersion(version);                // ETag of the new version
    return TypedResults.Ok(new Response(account.Id, account.Name, version));
}
```

- Taking an `IfMatch` parameter is what makes the header mandatory; `ApiHostTests.EveryUpdateAndDelete_RequiresIfMatch` fails if an update or delete endpoint does not take it.
- Read projections get the version with `EF.Property<uint>(entity, RowVersion.PropertyName)`.
- `If-None-Match` / `304` for reads is not implemented yet (see Scalability).

### Health

| | |
|---|---|
| **Route** | `GET /api/health` |
| **Auth** | Public |
| **Request** | None |
| **Responses** | `200` `Healthy` (text) when the API runs; `503` `Unhealthy` when a registered health check fails (none yet) |
| **Isolation** | No user data |
| **Tests** | `ApiHostTests.Health_IsPublic` |

Used by container health checks later ([devops/docker.md](../devops/docker.md)).

### OpenAPI

Development only ([ADR-0012](../architecture/adr/0012-api-foundation.md)): in any other environment these routes do not exist.

| Route | Content |
|---|---|
| `GET /openapi/v1.json` | OpenAPI document generated by ASP.NET Core |
| `GET /scalar/` | Scalar UI on that document (`/scalar` redirects here) |

Both are public on a development machine. Requests sent from Scalar carry the cookies of the API origin: call `GET /api/auth/antiforgery`, add the header `X-XSRF-TOKEN` with the value of the `XSRF-TOKEN` cookie, log in with `POST /api/auth/login`, then call the protected endpoints. The document is also the input for a generated Angular client, if `[Frontend]` chooses one.

### Authentication

Module `auth` ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md), [security.md](security.md)). Every unsafe request — these included — needs the antiforgery header `X-XSRF-TOKEN` ([security.md](security.md#csrf)); a missing or wrong token gives `400` `auth.antiforgery_invalid`.

#### Get the antiforgery token

| | |
|---|---|
| **Route** | `GET /api/auth/antiforgery` |
| **Auth** | Public |
| **Request** | None |
| **Responses** | `204`, cookie `XSRF-TOKEN` (readable) + `pf_antiforgery` |
| **Tests** | `AuthTests.AntiforgeryToken_IsAReadableStrictCookie` |

Called by the app at startup, before its first unsafe request.

#### Login

| | |
|---|---|
| **Route** | `POST /api/auth/login` |
| **Auth** | Public, rate limited (10 / minute / address) |
| **Request** | `{ "email": "me@example.com", "password": "…", "rememberMe": false }` — `email` required, valid, ≤ 256; `password` required, ≤ 128 |
| **Responses** | `200` `{ "id": "…", "email": "…" }`, cookies `pf_session` and a new `XSRF-TOKEN` · `400` `validation.failed` · `401` `auth.invalid_credentials` (wrong email, wrong password or locked — same answer) · `429` `request.too_many` |
| **Tests** | `AuthTests` |

`rememberMe: false` gives a cookie that ends with the browser; `true` keeps it 14 days (sliding).

#### Logout

| | |
|---|---|
| **Route** | `POST /api/auth/logout` |
| **Auth** | Session |
| **Request** | None |
| **Responses** | `204`, session cookie removed, new anonymous `XSRF-TOKEN` · `401` `auth.required` |
| **Tests** | `AuthTests.Logout_EndsTheSession` |

#### Current user

| | |
|---|---|
| **Route** | `GET /api/auth/me` |
| **Auth** | Session |
| **Responses** | `200` `{ "id": "…", "email": "…" }` · `401` `auth.required` |
| **Isolation** | Always the user of the session |
| **Tests** | `AuthTests.Me_*` |

The app calls it at startup to decide whether to show the login.

#### Change password

| | |
|---|---|
| **Route** | `POST /api/auth/change-password` |
| **Auth** | Session |
| **Request** | `{ "currentPassword": "…", "newPassword": "…" }` — both required, ≤ 128 |
| **Responses** | `204`, session renewed · `400` `auth.password_incorrect` on `currentPassword` · `400` `auth.password_rejected` on `newPassword` (e.g. shorter than 12) · `401` `auth.required` |
| **Tests** | `AuthTests.ChangePassword_*` |

Other sessions of the user end within 5 minutes (security stamp).

#### Register

| | |
|---|---|
| **Route** | `POST /api/auth/register` — **exists only with `Auth__AllowRegistration=true`** |
| **Auth** | Public, rate limited |
| **Request** | `{ "email": "…", "password": "…" }` — `email` required, valid, ≤ 256; `password` required, ≤ 128 |
| **Responses** | `201` `{ "id": "…", "email": "…" }` with the default categories, session started, new `XSRF-TOKEN` · `400` `auth.password_rejected` on `password` · `400` `validation.failed` · `409` `auth.email_taken` on `email` · `429` `request.too_many` |
| **Tests** | `RegistrationTests`, `AuthTests.Register_DoesNotExist_WhileRegistrationIsOff` |

### Resources

| Resource | Module prefix | Block | Status |
|---|---|---|---|
| Authentication (antiforgery, login, logout, me, change password, register) | `auth` | 3 | Implemented — [Authentication](#authentication) |
| Accounts | `accounts` | 4 | To do |
| Categories | `categories` | 5 | To do |
| Transactions (with tags, filters) | `transactions` | 6 | To do |
| Transfers | `transfers` | 7 | To do |
| Budgets | `budgets` | 8 | To do |
| Dashboard | `dashboard` | 9 | To do |

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| `/api/{module}` routes from `ApiModule.Prefix`, not from folder names | Renaming code never changes a public URL | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Development-only OpenAPI document and Scalar UI | Try endpoints locally without exposing the API surface on real instances | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Contract documented in the same pull request as the endpoint | The document never lags behind the code | [README.md](README.md) |

## Tests

- `ApiHostTests`: health is public; the OpenAPI document and Scalar exist in Development and not in Production; only the listed routes are public; every `PUT` / `PATCH` / `DELETE` requires `If-Match`.
- `ErrorHandlingTests`: `ETag` on create and read; `428` without `If-Match`; `400` on a malformed one; `412` on a stale version and on a write that lands between the check and the save, with nothing changed; new `ETag` after a successful update; `404` for another user's resource, even with a version.
- `EndpointMappingTests`: an endpoint of a module is served under `/api/{prefix}` with the module tag.
- Each resource section names its test class; the minimum per endpoint is in [testing.md](testing.md#minimum-per-endpoint).

## Scalability

- **More resources**: one section per resource; the table above grows.
- **Versioning**: not needed while the only client ships with the API. With third-party or native clients, a version segment (`/api/v2`) or header with a new ADR; `ApiModule` groups make it a change in one place.
- **Generated clients**: the OpenAPI document can be exported at build time (`Microsoft.Extensions.ApiDescription.Server`) if `[Frontend]` generates its client.
- **PWA and caching**: the offline queue stores the version with each pending change and sends it as `If-Match`; a `412` means the item changed elsewhere. Reads can answer `304 Not Modified` to `If-None-Match` when needed.
- **OpenAPI**: `If-Match` and `ETag` are not described in the document yet; an operation transformer can add them for endpoints with `IfMatchRequiredMetadata`.

## To do / open questions

- ✅ Status codes of errors, concurrency and cross-user access — [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md).
- Describe `If-Match` / `ETag` in the OpenAPI document (operation transformer), if the generated client needs it.
- Enum serialization, paging and sorting conventions (with the first endpoints that need them).
- Contracts of every resource (blocks 4–9).

---
_Last updated: 2026-11-12_
