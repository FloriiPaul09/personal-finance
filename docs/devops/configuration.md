# Configuration

## Scope

Every configuration value of the application and of the containers: where it is read, its default, and how it is provided in development and on real instances.

## Current state

### Sources

| Where | Used by | Versioned |
|---|---|---|
| `compose.yaml` defaults (`${VAR:-default}`) | Docker Compose | yes — development values only |
| `backend/src/Api/appsettings.Development.json` | API run with `dotnet run` (`ASPNETCORE_ENVIRONMENT=Development`) | yes — development values only |
| `.env` (copied from `.env.example`) | Docker Compose, and the API container once it exists | **no** (`.gitignore`) |
| Environment variables | Everything (highest priority) | no |

ASP.NET Core maps `__` in variable names to nested keys: `Auth__InitialUser__Email` is `Auth:InitialUser:Email`.

### Variables

| Variable | Default (development) | Purpose |
|---|---|---|
| `POSTGRES_DB` | `personal_finance` | Database created by the `db` container |
| `POSTGRES_USER` | `personal_finance` | Database owner |
| `POSTGRES_PASSWORD` | `personal_finance_dev` | Database password — **set a real one outside a local machine** |
| `POSTGRES_PORT` | `5432` | Host port of the database, bound to `127.0.0.1` |
| `ASPNETCORE_ENVIRONMENT` | `Development` (launch profile) | `Production` in containers |
| `ConnectionStrings__Default` | `Host=localhost;Port=5432;…` | Npgsql connection string; host `db` inside Compose |
| `Auth__AllowRegistration` | `false` | Public registration ([ADR-0001](../architecture/adr/0001-authentication.md)) |
| `Auth__InitialUser__Email` | `dev@example.com` | User created at first start when there are no users |
| `Auth__InitialUser__Password` | `dev-only-password` | Its password (at least 12 characters); ignored once the user exists |
| `Auth__LoginRateLimit__PermitLimit` | `10` | Login and registration attempts per client address per window ([backend/security.md](../backend/security.md#rate-limiting)) |
| `Auth__LoginRateLimit__WindowSeconds` | `60` | Length of the rate limiting window, in seconds |

The `Auth` section is validated at startup: email and password of the initial user together or neither, a valid email, rate limit values in range. Data Protection keys need no variable: they live in the database ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)).

`.env.example` lists every variable with placeholder values and comments.

### Rules

- **No secrets in the repository.** The versioned values are development-only, documented as such, and only work on a database bound to `127.0.0.1`.
- **Real instances use `.env`.** When the `api` service is added to Compose, its `Auth__*` and connection string variables are required (`${VAR:?message}`): Compose refuses to start without them, so a public instance can never run with development values.
- Every new variable is added to `.env.example` and to this table in the same pull request.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Versioned development defaults shared by Compose and `appsettings.Development.json` | A fresh clone runs without configuration | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| `.env` for overrides and real instances, required variables in production | Secure default for self-hosters | [ADR-0001](../architecture/adr/0001-authentication.md), [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| Initial user from `Auth__InitialUser__*` | First user without public registration | [ADR-0001](../architecture/adr/0001-authentication.md) |

## Tests

- `StartupTests` (`[Backend]`): the app refuses to start without users and without `Auth__InitialUser__*`, or with invalid `Auth` values; the variables are ignored once a user exists ([backend/testing.md](../backend/testing.md#authentication-and-startup-tests)).
- `docker compose config` with and without `.env` (CI, part 2).

## Scalability

- **More settings**: grouped by section (`Auth`, later `Email`, `Storage`) and bound to options classes in `[Backend]`.
- **Multi-user**: `Auth__AllowRegistration=true` plus email settings for confirmation and password reset.
- **Orchestrators** (Kubernetes, managed hosting): the same variables, provided as secrets of the platform.

## To do / open questions

- ✅ `[Backend]` Connection string: read from the final configuration when the first `DbContext` is created; startup still fails at once without it (migrations run before serving requests) — [backend/testing.md](../backend/testing.md#hosts).
- ✅ `[Backend]` Options class and validation at startup for `Auth` (`AuthOptions`).
- ✅ Data Protection keys: in the database, no variable ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)).
- Production variables of the `api` service when it joins `compose.yaml`.

---
_Last updated: 2026-11-12_
