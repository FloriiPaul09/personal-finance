# Security

## Scope

How the API protects data: session cookie, login and password rules, CSRF, rate limiting, the authorization fallback policy, public endpoints, the current user behind the per-user isolation, other users' data, the initial user, Data Protection keys and banned APIs. The overall design is in [architecture/authentication.md](../architecture/authentication.md) ([ADR-0001](../architecture/adr/0001-authentication.md)); the endpoints in [api-endpoints.md](api-endpoints.md#authentication).

## Current state

Implemented ([ADR-0012](../architecture/adr/0012-api-foundation.md), [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md), [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)):

- `Api/Common/Security`: `AddApiSecurity` / `UseApiSecurity`, `AuthOptions`, `AntiforgeryCookie`, `HttpCurrentUser`, `InitialUserStartup`;
- `Api/Features/Auth`: the endpoints;
- `Infrastructure/Identity`: `IdentityPolicy`, `AddIdentityStores`, `InitialUserInitializer`.

### Pipeline

```
AddApiSecurity(configuration)
  Auth options (validated at startup) · Identity stores + SignInManager · session cookie
  Data Protection keys in the database · antiforgery · login rate limiter · fallback policy · HttpCurrentUser
startup: migrations → initial user
UseApiErrors → UseApiSecurity: UseAuthentication → UseAuthorization → UseRateLimiter → UseAntiforgery
/api group: AntiforgeryValidationFilter on every unsafe request
```

### Session

ASP.NET Core Identity application cookie, the default scheme (`IdentityConstants.ApplicationScheme`):

| Setting | Value | Why |
|---|---|---|
| Name | `pf_session` | Short, does not advertise the framework |
| `HttpOnly` | yes | Unreadable by injected scripts |
| `Secure` | always | Never sent over plain HTTP — **HTTPS is required outside `localhost`** (`[DevOps]`) |
| `SameSite` | `Strict` | Never sent with requests started by another site |
| Lifetime | 14 days, sliding | An active user stays signed in |
| Without "remember me" | Browser session cookie | Closing the browser ends the session |
| Security stamp check | Every 5 minutes | A password change ends the other sessions within 5 minutes |

For an API the redirects of the cookie are replaced by status codes: no session → `401`, no permission → `403`. The Angular app shows its login page on `401` (`[Frontend]`).

### Password and lockout

`IdentityPolicy` (in `Infrastructure`, shared by the initial user, registration and password change):

- at least **12 characters**, at most 128 (request validation), **no composition rules** (NIST SP 800-63B: length matters more);
- one account per email (the email is the user name);
- **lockout after 5 failed logins, for 15 minutes**, for every user.

### Login

- `PasswordSignInAsync` with lockout on failure.
- Wrong email, wrong password and locked account give the **same** `401` `auth.invalid_credentials`: the response never tells whether an account exists. The message mentions the temporary lock, so a real user knows to wait.
- On success the session cookie is set and a new antiforgery token is issued for the signed-in user.

### CSRF

`SameSite=Strict` plus antiforgery tokens ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)), in the form Angular supports out of the box (`provideHttpClient(withXsrfConfiguration(...))` with its default names):

| Piece | Value |
|---|---|
| Readable cookie with the token | `XSRF-TOKEN` (not `HttpOnly`, `SameSite=Strict`, `Secure` on HTTPS) |
| Header the client sends | `X-XSRF-TOKEN` |
| Internal cookie of ASP.NET Core antiforgery | `pf_antiforgery` (`HttpOnly`, `SameSite=Strict`, `Secure` on HTTPS) |
| Where the token comes from | `GET /api/auth/antiforgery`, and every login, logout and registration response (the token is bound to the user) |
| Validated on | every `POST`, `PUT`, `PATCH`, `DELETE` of the `/api` group, login included (`AntiforgeryValidationFilter`); reads are never checked |
| Failure | `400` `auth.antiforgery_invalid` |

`UseAntiforgery()` is in the pipeline too: form and file endpoints (CSV import, attachments) are validated by it automatically.

The two antiforgery cookies are `Secure` only on HTTPS requests (`SameAsRequest`), unlike the session cookie (always `Secure`). With `Always`, ASP.NET Core antiforgery throws on every plain-HTTP request — local development on `http://localhost:5080`, or a reverse proxy without forwarded headers — and the API would answer `500` everywhere. Nothing is weakened: over plain HTTP the session cookie is never sent outside `localhost`, so no authenticated request can be made there.

Tools: in Scalar or `.http` files, call `GET /api/auth/antiforgery` first and copy the `XSRF-TOKEN` cookie value into an `X-XSRF-TOKEN` header.

### Rate limiting

Built-in ASP.NET Core rate limiter, policy `auth` on login and registration: a fixed window per client address, **10 attempts per minute** by default (`Auth__LoginRateLimit__PermitLimit`, `Auth__LoginRateLimit__WindowSeconds`), then `429` `request.too_many`. It sits on top of the per-account lockout: the limiter slows down guessing across accounts and keeps accounts from being locked endlessly.

Behind a reverse proxy the client address is the proxy's until forwarded headers are configured (`[DevOps]`): until then all clients share one window.

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
| `GET /api/auth/antiforgery` | Token before the first unsafe request | All |
| `POST /api/auth/login` | Sign in (rate limited) | All |
| `POST /api/auth/register` | Sign up (rate limited) | Only with `Auth__AllowRegistration=true` |
| `/openapi/*` | OpenAPI document | Development only |
| `/scalar*` | Scalar UI | Development only |

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
| Lists and aggregates | Only the user's rows; nothing to report. A filter on another user's account or category finds nothing |
| Client-chosen id already used by another user (`POST /api/transactions` with `id`) | `409` `resource.conflict` on `id`, with no detail about the other row — practically impossible with UUIDv7, and it reveals nothing the client did not send |

`403` is never used for this: it would reveal that the id exists, and telling "exists but not yours" apart would need reading without the filter, which is banned in `Api`. Even the database refuses cross-user references through the composite foreign keys ([database/user-isolation.md](../database/user-isolation.md)).

### Initial user

At startup, after the migrations and before serving requests (`EnsureInitialUserAsync`, ADR-0001):

| Database | `Auth__InitialUser__*` | Result |
|---|---|---|
| No users | Email and password set | The user is created with Identity's rules, then gets the default categories |
| No users | Missing | **Startup fails**: "The database has no users. Set Auth__InitialUser__Email and Auth__InitialUser__Password…" |
| No users | Password refused by the rules (e.g. too short) | Startup fails with Identity's message; no user is created |
| Users exist | Anything | Ignored: changing the values never changes a user |

The `Auth` options are validated at startup (`AuthOptionsValidator`): email and password together or neither, a valid email, rate limit values in range. Two instances starting at the same time on an empty database: one creates the user, the other fails on the unique email and is restarted by its orchestrator.

### Data Protection keys

The keys that protect the session and antiforgery cookies live in the database, table `data_protection_keys` (`PersistKeysToDbContext<AppDbContext>`, application name `PersonalFinance`):

- sessions survive restarts and redeployments;
- every API instance reads the same keys;
- the keys are in the database backup, together with the data they protect.

The table is not user data: no owner, no filter.

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
| Custom auth endpoints; registration mapped only when enabled | Only what the app uses, same error shape; secure default | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md) |
| `SameSite=Strict` + antiforgery tokens (`XSRF-TOKEN` / `X-XSRF-TOKEN`) | Covers shared domains and future uploads; native in Angular | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md) |
| 12+ characters, lockout 5 / 15 min, sliding 14-day cookie, stamp check 5 min | NIST guidance; convenience without long-lived risk | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md) |
| Built-in rate limiter per address on login and registration | No dependency; complements the lockout | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md) |
| Data Protection keys in the database | Sessions survive restarts, shared by instances, backed up with the data | [ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md) |

## Tests

- `AuthTests` (real cookie): login starts a session seen by `me`; antiforgery and login work over plain HTTP (regression test for the `500` caused by an always-`Secure` antiforgery cookie); cookie `HttpOnly`, `Secure`, `SameSite=Strict`, session-only without "remember me" and persistent with it; same `401` for wrong password and unknown email; lockout after 5 failures; invalid email `400` on the field; login public and rate limited, `429` after the limit; unsafe request without token `400`; antiforgery cookie readable and strict; logout ends the session; `me` without session `401` and with a session returns that user only; **a session works on another instance of the API** (keys from the database); password change (works, wrong current password `400` on `currentPassword`, too short `400` on `newPassword`, no session `401`); registration route absent while off.
- `RegistrationTests`: user created with the default categories and signed in; email in use `409`; short password `400`; public and rate limited.
- `StartupTests`: no users and no initial user → startup fails; initial user created once with the default categories; ignored when users exist; short password → startup fails, no user; invalid options → startup fails.
- `AuthOptionsValidatorTests`: defaults valid; incomplete or invalid initial user and out-of-range rate limits rejected.
- `HttpCurrentUserTests`: signed-in user with a valid id; anonymous request; no request; unauthenticated identity; missing, empty, invalid or empty-`Guid` claim; value read on every access.
- `EndpointMappingTests`: an endpoint without `AllowAnonymous` returns `401` (not a redirect); an unknown route returns `401`.
- `ApiHostTests`: only the listed routes are public; the registered `ICurrentUser` is `HttpCurrentUser`; development tools do not exist in Production.
- `ApiRulesTests` (architecture): no call to `IgnoreQueryFilters` in `Api`, and the rule detects a known call.
- `ErrorHandlingTests`: another user's resource gives `404` like a nonexistent one; another user's id in the body gives `400` `account.not_found`; `401` has a ProblemDetails body with `auth.required`.
- From block 4, every endpoint: `401` without a session and isolation between two users ([testing.md](testing.md#minimum-per-endpoint)).

## Scalability

- **Multi-user**: `Auth__AllowRegistration=true`. Next steps: email confirmation and password reset (an email sender), possibly an admin area.
- **2FA (TOTP) or passkeys**: supported by Identity; endpoints and UI to add.
- **Roles / admin area**: named policies on top of the fallback (`RequireRole`), applied to an admin module.
- **Native or third-party clients**: add a bearer scheme next to the cookie; `HttpCurrentUser` reads the same claim (new ADR, [authentication.md](../architecture/authentication.md#scalability)). Antiforgery is for cookies only: bearer requests would skip it.
- **Several API instances**: already supported (keys in the database). Rate limiting is per instance: a shared limiter (e.g. Redis) only if instances multiply.
- **Keys at rest**: protect them with a certificate (`ProtectKeysWithCertificate`) when the database is shared or backed up off-site.
- **File uploads** (CSV import, attachments): antiforgery already in place. Attachments are always served by an endpoint that goes through `UserScope`, never as static files (static files bypass endpoint authorization).

## To do / open questions

- `[DevOps]` HTTPS is required for login outside `localhost` (`Secure` cookies); forwarded headers behind the reverse proxy, so rate limiting sees client addresses.
- `[Frontend]` Call `GET /api/auth/antiforgery` at startup; keep Angular's default XSRF names; on `401` show the login; on `auth.invalid_credentials` show a generic message.
- Password reset and email confirmation, with multi-user.

---
_Last updated: 2026-11-12_
