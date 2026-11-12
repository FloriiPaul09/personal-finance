# Security

## Scope

How the API protects data: the authentication cookie, the authorization fallback policy, public endpoints, the current user behind the per-user isolation, banned APIs, and — from block 3 — authentication endpoints, CSRF, password and lockout policy, rate limiting, the initial user and Data Protection keys. The overall design is in [architecture/authentication.md](../architecture/authentication.md) ([ADR-0001](../architecture/adr/0001-authentication.md)).

## Current state

The foundation is implemented ([ADR-0012](../architecture/adr/0012-api-foundation.md)) in `Api/Common/Security`. Signing in arrives with block 3: until then no endpoint can create a session.

### Authentication scheme

`AddApiSecurity()` registers the ASP.NET Core Identity application cookie as the default scheme (`IdentityConstants.ApplicationScheme`). For an API, its redirects are replaced by status codes:

| Situation | Default cookie behavior | API behavior |
|---|---|---|
| No session on a protected endpoint | `302` to `/Account/Login` | `401` |
| Session without the required permission | `302` to `/Account/AccessDenied` | `403` |

The Angular app sends the user to its login page when it receives `401` (`[Frontend]`). Cookie settings (lifetime, sliding expiration, "remember me") are set in block 3.

> The cookie handler validates the security stamp of an existing session, which needs the Identity sign-in services. They are registered in block 3 together with login; until then no cookie can be issued.

### Fallback policy

Every endpoint requires an authenticated user unless it says otherwise:

```csharp
services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
```

- Slices write no authorization code. A forgotten call leaves an endpoint **closed**, never open.
- The policy also applies to requests that match no endpoint: an anonymous caller gets `401` for an unknown route too, so routes are not revealed.

### Public endpoints

The only routes reachable without a session. Each one is listed here and in `ApiHostTests.PublicRoutePrefixes`; the test fails if any other endpoint has `AllowAnonymous()`.

| Route | Why | Environments |
|---|---|---|
| `GET /api/health` | Container health checks | All |
| `/openapi/*` | OpenAPI document | Development only |
| `/scalar*` | Scalar UI | Development only |
| `POST /api/auth/login` | Sign in | All — block 3 |

### Current user

`HttpCurrentUser` is the `ICurrentUser` of the API. It is the user the query filter `UserScope` reads for and the `SaveChanges` interceptor writes for ([database/user-isolation.md](../database/user-isolation.md)).

- Reads the `ClaimTypes.NameIdentifier` claim of the request on **every access**, through `IHttpContextAccessor`. That is the claim ASP.NET Core Identity puts the user id in (`ClaimsIdentityOptions.UserIdClaimType`, default — keep it).
- No authenticated request, no claim, a value that is not a `Guid`, or `Guid.Empty` → no user: queries on user data return nothing and changes are refused.
- Registered with `Replace` over the "nobody" default of `AddPersistence`, so the order of registration does not matter.
- Outside HTTP (default categories for a new user, the future `Worker`, tests) code uses `FixedCurrentUser`.

### Another user's data

The query filter `UserScope` makes another user's rows invisible: for the API they do not exist ([ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)).

| Case | Response |
|---|---|
| Route id of another user (`GET`, `PUT`, `DELETE /api/x/{id}`) | `404` `resource.not_found` — identical to an id that never existed, before any version check |
| Id of another user in the body (e.g. `accountId` of a transaction) | `400` `{resource}.not_found` on that field (`GetReferenceAsync`) — identical to a nonexistent id |
| Lists and aggregates | Only the user's rows; nothing to report |

`403` is never used for this: it would reveal that the id exists, and telling "exists but not yours" apart would need reading without the filter, which is banned in `Api`. Even the database refuses cross-user references through the composite foreign keys ([database/user-isolation.md](../database/user-isolation.md)).

### Banned APIs

`Microsoft.CodeAnalysis.BannedApiAnalyzers` checks `backend/src/Api/BannedSymbols.txt` at compile time; `.editorconfig` makes the diagnostic (`RS0030`) an error.

| Banned in `Api` | Why |
|---|---|
| `IgnoreQueryFilters()` (both overloads) | Would read or change other users' data ([database/user-isolation.md](../database/user-isolation.md#query-filter)) |

The same rule is checked by an architecture test ([testing.md](testing.md#architecture-tests)). Admin features that really need to bypass the filter go in `Infrastructure`, behind an admin policy, with their own ADR.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Identity cookie answering `401` / `403` | SPA on the same origin, no redirects for an API | [ADR-0001](../architecture/adr/0001-authentication.md), [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Fallback policy requiring authentication, public routes listed and tested | Secure by default; a mistake closes an endpoint instead of opening it | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| `HttpCurrentUser` from the `NameIdentifier` claim, read on every access | Works with cookies and future bearer tokens, behind several instances | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| `IgnoreQueryFilters` banned in `Api` at compile time | Isolation cannot be bypassed from a slice | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Another user's data is `404` (route) or `400` `{resource}.not_found` (body), never `403` | Existence is not revealed; follows from the query filter | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md) |

## Tests

- `HttpCurrentUserTests`: signed-in user with a valid id; anonymous request; no request; unauthenticated identity; missing, empty, invalid or empty-`Guid` claim; value read on every access.
- `EndpointMappingTests`: an endpoint without `AllowAnonymous` returns `401` (not a redirect); an unknown route returns `401`.
- `ApiHostTests`: health is public; only the listed routes are public; the registered `ICurrentUser` is `HttpCurrentUser`; development tools do not exist in Production.
- `ApiRulesTests` (architecture): no call to `IgnoreQueryFilters` in `Api`, and the rule detects a known call.
- `ErrorHandlingTests`: another user's resource gives `404` like a nonexistent one, also on update with a version; another user's id in the body gives `400` `account.not_found` like a nonexistent one; `401` has a ProblemDetails body with `auth.required`.
- From block 4, every endpoint: `401` without a session and isolation between two users ([testing.md](testing.md#minimum-per-endpoint)).

## Scalability

- **Multi-user**: nothing to change here; registration adds users, the fallback policy and the current user already apply to everyone.
- **Roles / admin area**: named policies on top of the fallback (`RequireRole`), applied to an admin module.
- **Native or third-party clients**: add a bearer scheme; `HttpCurrentUser` reads the same claim (new ADR, [authentication.md](../architecture/authentication.md#scalability)).
- **Several API instances**: needs shared Data Protection keys (block 3).
- **File uploads** (CSV import, attachments): Minimal API requires antiforgery on form and file endpoints; the CSRF choice of block 3 takes it into account. Attachments are always served by an endpoint that goes through `UserScope`, never as static files (static files bypass endpoint authorization).

## To do / open questions

Block 3 ([authentication.md](../architecture/authentication.md#to-do--open-questions)):

- Auth endpoints: `MapIdentityApi` or custom (login, logout, me, change password), registration disabled while `Auth__AllowRegistration=false`.
- CSRF: `SameSite=Strict` alone or with antiforgery tokens (Angular XSRF support), considering file uploads.
- Password, lockout and cookie lifetime values; rate limiting on login.
- Initial user at startup from `Auth__InitialUser__*`, then `DefaultCategoriesInitializer.EnsureForUserAsync`; startup fails without users and without the variables.
- Data Protection keys: database or volume (with `[DevOps]`).
- Options classes validated at startup for `Auth`.

---
_Last updated: 2026-11-12_
