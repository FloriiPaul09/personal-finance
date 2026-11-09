# ADR-0001: Authentication with ASP.NET Core Identity

- **Status**: Accepted
- **Date**: 2026-11-09
- **Deciders**: Paul

## Context

- Personal finance app, used today by a single person, but published as a **public GitHub repository** that anyone can self-host, with multi-user support on the roadmap.
- Financial data is sensitive. The app must be usable from a phone, so it may be reachable beyond `localhost`.
- Installation must stay as simple as `docker compose up`, with **secure defaults** and no secrets in the repository.
- Architecture: Angular SPA served by nginx on the same origin as the ASP.NET Core API; PostgreSQL via EF Core.

## Decision

Use **ASP.NET Core Identity from the first version**:

1. Identity stores in PostgreSQL via EF Core, in the same `DbContext` as the domain.
2. Session through the Identity cookie, `HttpOnly`, `Secure`, `SameSite=Strict`. No tokens in browser storage.
3. Public registration disabled by default, enabled through configuration (`Auth__AllowRegistration`).
4. Initial user created at first startup from environment variables; startup fails if no user exists and the variables are missing.
5. Every user-owned entity references its owner (`UserId`) from the first migration, and queries are filtered by the current user.

## Consequences

**Positive**
- Multi-user becomes a configuration change plus some UI, not a rewrite.
- Password hashing, lockout, security stamps and 2FA come from a maintained framework instead of custom code.
- Anyone installing from the repo gets a protected instance by default.
- Familiar setup for .NET contributors.

**Negative**
- More tables, configuration and framework behavior to understand than a single hard-coded user.
- Default cookie behavior must be adapted for an API (no redirects to a login page).
- Data Protection keys must be persisted, or sessions are lost on every container restart.
- Every domain query must respect user scoping (mitigated with global query filters).

**Follow-ups** — tracked in [authentication.md](../authentication.md#to-do--open-questions): auth endpoints and CSRF (`[Backend]`), `UserId` and query filters (`[DB]`), HTTPS and env variables (`[DevOps]`), login UI and guards (`[Frontend]`).

## Alternatives considered

**A. No authentication, local use only** — Rejected. Zero effort, but unusable from the phone outside the home network without a VPN; anyone exposing a clone would publish their finances; the most expensive path to multi-user.

**B. Single user configured through environment variables** — Rejected. Simple and safe enough behind HTTPS, but no password change, reset or lockout without custom code, and it would be thrown away when moving to multi-user.

**JWT stored in browser storage** (instead of a cookie) — Rejected. Readable by any injected script (XSS), and brings refresh-token handling with no benefit for a same-origin SPA. To be reconsidered only for native or third-party clients.
