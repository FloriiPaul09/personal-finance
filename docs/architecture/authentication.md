# Authentication

## Scope

How users authenticate, how sessions are kept between the Angular SPA and the API, and how data is tied to its owner. HTTP endpoints are documented in [backend/api-endpoints.md](../backend/api-endpoints.md); the API side of security in [backend/security.md](../backend/security.md); configuration variables in [devops/configuration.md](../devops/configuration.md).

## Current state

Decided ([ADR-0001](adr/0001-authentication.md)) and **implemented** in `[Backend]` ([ADR-0014](adr/0014-authentication-endpoints-and-sessions.md)): endpoints, cookie values, CSRF, rate limiting and keys are detailed in [backend/security.md](../backend/security.md). Configuration variables: [devops/configuration.md](../devops/configuration.md).

### Identity store

- ASP.NET Core Identity with EF Core stores, on the same PostgreSQL database and `DbContext` as the domain (the application `DbContext` derives from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`).
- `ApplicationUser` extends `IdentityUser<Guid>` (table `users`). It lives outside the domain entities: domain entities only reference the owner by `UserId` ([ADR-0010](adr/0010-data-model-and-user-isolation.md)).

### Session

- Identity application cookie: `HttpOnly`, `Secure`, `SameSite=Strict`.
- Angular and the API are served from the same origin (nginx serves the SPA and proxies `/api`; in development the Angular dev server proxies `/api`): no CORS, no tokens in JavaScript-accessible storage.
- The API answers `401` / `403` instead of redirecting to a login page (default cookie behavior is changed for API requests).
- Sliding expiration: browser session cookie by default, 14 days with "remember me" (confirmed in [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md)).

### Registration and first user

- Public registration is **disabled by default**: `Auth__AllowRegistration=false`.
- At startup, if the database has **no users**, the app creates the initial user from `Auth__InitialUser__Email` and `Auth__InitialUser__Password`.
- If there are no users and those variables are missing, the app **fails to start** with a clear error (secure by default: never an open instance).
- Once the user exists, the variables are ignored: changing the password in `.env` does **not** change the user's password. Password change happens from the UI through `POST /api/auth/change-password`.
- In development the initial user comes from `appsettings.Development.json` (`dev@example.com`, development-only password); real instances set it in `.env`.

### Password and lockout policy

- Minimum length 12, no mandatory character classes (length matters more than composition).
- Lockout after 5 failed attempts for 15 minutes, enabled for every user.

### Data ownership

- Every user-owned entity (account, category, transaction, budget, recurrence, tag) has a `UserId` from the first migration.
- Every query is filtered by the current user (named EF Core query filter `UserScope`), and composite foreign keys `(user_id, id)` let the database reject references between users ([database/user-isolation.md](../database/user-isolation.md)).

### Supporting pieces required

| Piece | Why | Where it is decided |
|---|---|---|
| Persisted Data Protection keys — ✅ in the database | Otherwise every container restart invalidates all sessions | [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md) |
| HTTPS when exposed beyond localhost | `Secure` cookies and credentials in transit | `[DevOps]` |
| Rate limiting on login — ✅ built-in limiter | Slows down brute-force attempts, on top of lockout | [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md) |

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| ASP.NET Core Identity from day one | Ready for multi-user, hashing / lockout / 2FA already provided, standard for .NET contributors | [ADR-0001](adr/0001-authentication.md) |
| Cookie instead of JWT in browser storage | Same-origin SPA: an `HttpOnly` cookie cannot be read by injected scripts | [ADR-0001](adr/0001-authentication.md) |
| Registration off, first user from env | Secure default for anyone who self-hosts from the public repo | [ADR-0001](adr/0001-authentication.md) |
| `UserId` on every owned entity | Avoids the costly retrofit when going multi-user | [ADR-0001](adr/0001-authentication.md) |
| Development initial user versioned, real one from `.env` | Clone and run locally; required variables on real instances | [ADR-0008](adr/0008-local-development-environment.md) |

## Tests

Implemented in `Api.Tests` (`AuthTests`, `RegistrationTests`, `StartupTests`, [backend/security.md](../backend/security.md#tests)), following [testing-strategy.md](testing-strategy.md):

- startup: the app refuses to start with no users and no `Auth__InitialUser__*`; it creates the user once; it ignores the variables afterwards;
- every endpoint answers `401` without a session, except login and `/api/health`;
- isolation: user B never reads or changes user A's data;
- lockout after 5 failed attempts; registration endpoint unavailable while `Auth__AllowRegistration=false`;
- end-to-end: login flow on a phone-sized viewport (`[Frontend]`).

## Scalability

- **More users**: set `Auth__AllowRegistration=true`; add email confirmation and password reset (needs an email sender and its configuration); possibly an admin area for user management.
- **Existing data**: already partitioned by `UserId`, no data migration needed.
- **2FA (TOTP)**: supported by Identity, only the UI is missing.
- **External logins** (Google, Microsoft): Identity external providers.
- **PWA** (roadmap): works with cookies, same origin.
- **Native mobile app or third-party clients**: cookies don't fit → bearer tokens (Identity API endpoints) or an OIDC server (e.g. OpenIddict). New ADR.
- **Frontend and API on different domains**: revisit `SameSite` and add CORS. New ADR.

## To do / open questions

- ✅ `[Backend]` Auth endpoints: custom (antiforgery, login, logout, me, change-password; register only when enabled) — [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md).
- ✅ `[Backend]` CSRF: `SameSite=Strict` + antiforgery tokens with Angular's default names — [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md).
- ✅ `[Backend]` Password, lockout and cookie lifetime values; rate limiting on login — [backend/security.md](../backend/security.md).
- ✅ `[Backend]` Storage of Data Protection keys: database — [ADR-0014](adr/0014-authentication-endpoints-and-sessions.md).
- ✅ `[DB]` `UserId` on every owned entity, query filter, Identity key type (`Guid`) and table naming (snake_case, `users`) — [ADR-0010](adr/0010-data-model-and-user-isolation.md), [database/user-isolation.md](../database/user-isolation.md).
- `[DevOps]` HTTPS / reverse proxy; how the app is reached from the phone (LAN/VPN vs public exposure) — still undecided.
- `[Frontend]` Login page, route guard, HTTP interceptor that redirects to login on `401`.

---
_Last updated: 2026-11-12_
