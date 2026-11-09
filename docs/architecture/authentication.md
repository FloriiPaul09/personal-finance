# Authentication

## Scope

How users authenticate, how sessions are kept between the Angular SPA and the API, and how data is tied to its owner. HTTP endpoints are documented in `backend/api-endpoints.md` once they are defined; configuration variables in [devops/configuration.md](../devops/configuration.md).

## Current state

Decided ([ADR-0001](adr/0001-authentication.md)), **not implemented yet**. Its configuration variables already exist in `.env.example` and in the development defaults ([devops/configuration.md](../devops/configuration.md)). Target design:

### Identity store

- ASP.NET Core Identity with EF Core stores, on the same PostgreSQL database and `DbContext` as the domain (the application `DbContext` derives from `IdentityDbContext<ApplicationUser>`).
- `ApplicationUser` extends `IdentityUser`. It lives outside the domain entities: domain entities only reference the owner by `UserId`.

### Session

- Identity application cookie: `HttpOnly`, `Secure`, `SameSite=Strict`.
- Angular and the API are served from the same origin (nginx serves the SPA and proxies `/api`; in development the Angular dev server proxies `/api`): no CORS, no tokens in JavaScript-accessible storage.
- The API answers `401` / `403` instead of redirecting to a login page (default cookie behavior is changed for API requests).
- Sliding expiration. Initial values (to confirm in `[Backend]`): session cookie by default, 14 days with "remember me".

### Registration and first user

- Public registration is **disabled by default**: `Auth__AllowRegistration=false`.
- At startup, if the database has **no users**, the app creates the initial user from `Auth__InitialUser__Email` and `Auth__InitialUser__Password`.
- If there are no users and those variables are missing, the app **fails to start** with a clear error (secure by default: never an open instance).
- Once the user exists, the variables are ignored: changing the password in `.env` does **not** change the user's password. Password change happens from the UI (to build).
- In development the initial user comes from `appsettings.Development.json` (`dev@example.com`, development-only password); real instances set it in `.env`.

### Password and lockout policy (initial values)

- Minimum length 12, no mandatory character classes (length matters more than composition).
- Lockout after 5 failed attempts for 15 minutes, enabled for every user.

### Data ownership

- Every user-owned entity (account, category, transaction, budget, recurrence, tag) has a `UserId` from the first migration.
- Every query is filtered by the current user (e.g. EF Core global query filters), so a missing filter cannot leak another user's data.

### Supporting pieces required

| Piece | Why | Where it is decided |
|---|---|---|
| Persisted Data Protection keys (DB or volume) | Otherwise every container restart invalidates all sessions | `[Backend]` / `[DevOps]` |
| HTTPS when exposed beyond localhost | `Secure` cookies and credentials in transit | `[DevOps]` |
| Rate limiting on login | Slows down brute-force attempts, on top of lockout | `[Backend]` |

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| ASP.NET Core Identity from day one | Ready for multi-user, hashing / lockout / 2FA already provided, standard for .NET contributors | [ADR-0001](adr/0001-authentication.md) |
| Cookie instead of JWT in browser storage | Same-origin SPA: an `HttpOnly` cookie cannot be read by injected scripts | [ADR-0001](adr/0001-authentication.md) |
| Registration off, first user from env | Secure default for anyone who self-hosts from the public repo | [ADR-0001](adr/0001-authentication.md) |
| `UserId` on every owned entity | Avoids the costly retrofit when going multi-user | [ADR-0001](adr/0001-authentication.md) |
| Development initial user versioned, real one from `.env` | Clone and run locally; required variables on real instances | [ADR-0008](adr/0008-local-development-environment.md) |

## Tests

To write with the implementation (`[Backend]`), following [testing-strategy.md](testing-strategy.md):

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

- `[Backend]` Auth endpoints: `MapIdentityApi` or custom endpoints (login, logout, me, change-password). `MapIdentityApi` also maps `/register`, so disabling registration needs explicit handling.
- `[Backend]` CSRF: `SameSite=Strict` as baseline; decide whether to add antiforgery tokens (Angular has built-in XSRF support).
- `[Backend]` Confirm password, lockout and cookie lifetime values; rate limiting on login.
- `[Backend]` / `[DevOps]` Storage of Data Protection keys.
- `[DB]` `UserId` on every owned entity, global query filters, Identity key type (`string` vs `Guid`) and table naming.
- `[DevOps]` HTTPS / reverse proxy; how the app is reached from the phone (LAN/VPN vs public exposure) — still undecided.
- `[Frontend]` Login page, route guard, HTTP interceptor that redirects to login on `401`.

---
_Last updated: 2026-11-09_
