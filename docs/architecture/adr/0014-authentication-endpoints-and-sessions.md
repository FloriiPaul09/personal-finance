# ADR-0014: Authentication endpoints, CSRF, sessions and authentication in tests

- **Status**: Accepted
- **Date**: 2026-11-12
- **Deciders**: Paul

## Context

- [ADR-0001](0001-authentication.md) chose ASP.NET Core Identity with an `HttpOnly` cookie, registration off by default and a first user from environment variables; the endpoints and the values were left to `[Backend]`.
- The API already has a fallback policy, error responses with stable codes and versions on writes ([ADR-0012](0012-api-foundation.md), [ADR-0013](0013-error-responses-and-concurrency.md)).
- The repository is public: anyone can self-host it, possibly on a shared domain, and multi-user is on the roadmap. File uploads (CSV import, attachments) and a PWA used from the phone are on the roadmap too.
- Without persistent Data Protection keys, every restart of the container invalidates every session; with several instances, a session from one is unreadable on another.
- Every endpoint test needs a signed-in user; hashing a password for each test user and logging in through the API in every test is slow.

## Decision

1. **Custom endpoints in the `auth` module**: `GET antiforgery`, `POST login`, `POST logout`, `GET me`, `POST change-password`, written with `SignInManager` / `UserManager`. No `MapIdentityApi`.
2. **Registration implemented and mapped only when `Auth:AllowRegistration` is `true`**: with `false` the route does not exist. It creates the user, gives the default categories and starts the session.
3. **CSRF: `SameSite=Strict` plus antiforgery tokens** in the form Angular supports natively: readable cookie `XSRF-TOKEN`, header `X-XSRF-TOKEN`, validated on every `POST` / `PUT` / `PATCH` / `DELETE` of the `/api` group (login included); a new token after login, logout and registration. `UseAntiforgery` is in the pipeline for future form and file endpoints.
4. **Values**:
   - password of at least 12 characters, no composition rules;
   - lockout after 5 failures for 15 minutes;
   - cookie `pf_session`, `HttpOnly`, `Secure`, `SameSite=Strict`, sliding 14 days, a browser session cookie unless "remember me";
   - security stamp checked every 5 minutes, so a password change ends the other sessions.
5. **Rate limiting** with the built-in limiter: fixed window per client address on login and registration (10 per minute, configurable), `429` `request.too_many`.
6. **Data Protection keys in the database** (`PersistKeysToDbContext`, table `data_protection_keys`, application name `PersonalFinance`).
7. **Tests**: resource tests sign in through a test authentication handler (header `X-Test-User`), selected only when the header is present; the real cookie, lockout, rate limiting and CSRF are tested in the `auth` tests. Test clients use HTTPS, cookies and antiforgery like the browser app.
8. **Test data**: one factory method per entity with optional parameters in `PersonalFinance.Testing` (`TestData`), saving through the domain as the user.
9. **Initial user** at startup after the migrations (ADR-0001), with the `Auth` options validated at startup: no users and no `Auth:InitialUser` stops the application.

Details: [backend/security.md](../../backend/security.md), [backend/api-endpoints.md](../../backend/api-endpoints.md#authentication), [backend/testing.md](../../backend/testing.md#authentication-in-tests).

## Consequences

**Positive**
- Only the endpoints the app uses exist, with the same error shape as the rest of the API.
- Login, registration and password errors never reveal whether an account exists (except registration with an email in use, which cannot be avoided).
- CSRF is covered even on a shared domain, and future uploads already have the antiforgery they require.
- Sessions survive restarts and work across instances; the keys are backed up with the data.
- Multi-user is a configuration switch; bearer tokens for native clients can be added next to the cookie, since the current user reads claims only.
- Resource tests stay fast; the security-critical paths still run on the real cookie.

**Negative**
- The app must call `GET /api/auth/antiforgery` before its first unsafe request, and tools (Scalar, `.http` files) must send the header.
- `Secure` cookies require HTTPS outside `localhost`: a plain-HTTP LAN install cannot log in (HTTPS is `[DevOps]`).
- Behind a reverse proxy, rate limiting sees the proxy address until forwarded headers are configured (`[DevOps]`).
- Data Protection keys are stored unencrypted in the database; anyone with a database dump can forge cookies (see Scalability in [security.md](../../backend/security.md#scalability)).
- One more migration and package (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`).

## Alternatives considered

**`MapIdentityApi`** — Rejected. Ready-made login, reset and 2FA, but it exposes registration, refresh tokens, email confirmation and 2FA endpoints to block one by one, has no logout, and returns errors in another shape.

**Registration postponed** — Rejected. Less work now, but `Auth__AllowRegistration` would exist without effect and the default categories would be wired only for the initial user.

**`SameSite=Strict` only** — Rejected. No code, but no protection from a sibling subdomain of the same site, and file uploads need antiforgery anyway.

**Composition rules for passwords; 30-day sessions** — Rejected. Composition rules lead to predictable passwords (NIST SP 800-63B); longer sessions raise the risk of a lost phone.

**Rate limiting per address and per email; no rate limiting** — Rejected. The lockout already covers the per-account case; without a limiter an attacker can keep accounts locked for free.

**Keys in a Docker volume** — Rejected. No migration, but one more volume and variable, no sharing between instances, easy to lose on reinstall.

**Real login in every test; test handler only** — Rejected. The first is slow and makes every test depend on login; the second never exercises the real cookie.

**Fluent builders; data created through the API** — Rejected. Builders add a class per entity for what optional parameters already give; API-created data makes tests slow and dependent on other endpoints.
