# Configuration

## Scope

Every configuration value of the application and of the containers: where it is read, its default, and how it is provided in development and on real instances.

## Current state

### Sources

| Where | Used by | Versioned |
|---|---|---|
| `compose.yaml` defaults (`${VAR:-default}`) | Docker Compose | yes — development values only |
| `backend/src/Api/appsettings.Development.json` | API run with `dotnet run` (`ASPNETCORE_ENVIRONMENT=Development`) | yes — development values only |
| `.env` (copied from `.env.example`) | Docker Compose: the database, and on a real instance the `api` and `frontend` containers of `compose.app.yaml` | **no** (`.gitignore`) |
| Environment variables | Everything (highest priority) | no |

ASP.NET Core maps `__` in variable names to nested keys: `Auth__InitialUser__Email` is `Auth:InitialUser:Email`.

### Variables

| Variable | Default (development) | Purpose |
|---|---|---|
| `POSTGRES_DB` | `personal_finance` | Database created by the `db` container |
| `POSTGRES_USER` | `personal_finance` | Database owner |
| `COMPOSE_FILE` | unset (only `compose.yaml`) | `compose.yaml:compose.app.yaml` in `.env.example`: adds `api` and `frontend` ([docker.md](docker.md#the-whole-application-composeappyaml)) |
| `APP_BIND` / `APP_PORT` | `127.0.0.1` / `8080` | Address and port where `compose.app.yaml` publishes the app (nginx) |
| `API_IMAGE` / `FRONTEND_IMAGE` | `personal-finance-api:local` / `personal-finance-frontend:local` | Images of `compose.app.yaml`: built from the repository unless set to published ones |
| `POSTGRES_PASSWORD` | `personal_finance_dev` | Database password — **required by `compose.app.yaml`**; generate one with `openssl rand -base64 32` (no `;` or `$`) |
| `POSTGRES_PORT` | `5432` | Host port of the database, bound to `127.0.0.1` |
| `ASPNETCORE_ENVIRONMENT` | `Development` (launch profile) | `Production` in containers |
| `ConnectionStrings__Default` | `Host=localhost;Port=5432;…` | Npgsql connection string. In `compose.app.yaml` it is built from `POSTGRES_*` (host `db`); set it only to run the API against another database |
| `Auth__AllowRegistration` | `false` | Public registration ([ADR-0001](../architecture/adr/0001-authentication.md)) |
| `Auth__InitialUser__Email` | `dev@example.com` | User created at first start when there are no users — **required by `compose.app.yaml`** |
| `Auth__InitialUser__Password` | `dev-only-password` | Its password (at least 12 characters); ignored once the user exists — **required by `compose.app.yaml`** |
| `Auth__LoginRateLimit__PermitLimit` | `10` | Login and registration attempts per client address per window ([backend/security.md](../backend/security.md#rate-limiting)) |
| `Auth__LoginRateLimit__WindowSeconds` | `60` | Length of the rate limiting window, in seconds |

The `Auth` section is validated at startup: email and password of the initial user together or neither, a valid email, rate limit values in range. Data Protection keys need no variable: they live in the database ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)).

`.env.example` lists every variable with placeholder values and comments.

### Rules

- **No secrets in the repository.** The versioned values are development-only, documented as such, and only work on a database bound to `127.0.0.1`.
- **Real instances use `.env`.** `compose.app.yaml` requires `POSTGRES_PASSWORD` and `Auth__InitialUser__*` (`${VAR:?message}`): Compose refuses to start without them, so an instance never runs with the development values. They live in a second file so that development, which reads only `compose.yaml`, still needs no `.env` ([ADR-0032](../architecture/adr/0032-container-images.md)).
- Every new variable is added to `.env.example` and to this table in the same pull request.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Versioned development defaults shared by Compose and `appsettings.Development.json` | A fresh clone runs without configuration | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| `.env` for overrides and real instances, required variables in production | Secure default for self-hosters | [ADR-0001](../architecture/adr/0001-authentication.md), [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| Initial user from `Auth__InitialUser__*` | First user without public registration | [ADR-0001](../architecture/adr/0001-authentication.md) |
| Secrets required in `compose.app.yaml`, not in `compose.yaml`; connection string built from `POSTGRES_*` | Development without `.env`; the password written once | [ADR-0032](../architecture/adr/0032-container-images.md) |

## Tests

- `StartupTests` (`[Backend]`): the app refuses to start without users and without `Auth__InitialUser__*`, or with invalid `Auth` values; the variables are ignored once a user exists ([backend/testing.md](../backend/testing.md#authentication-and-startup-tests)).
- `docker compose config` with and without `.env` (the `compose` job of the CI); with `.env.example` it also validates `compose.app.yaml`. An empty `POSTGRES_PASSWORD` is refused (checked locally).
- The `images` job starts the stack with `.env.example` and test values and signs in with the initial user ([ci-cd.md](ci-cd.md#jobs)).

## Scalability

- **More settings**: grouped by section (`Auth`, later `Email`, `Storage`) and bound to options classes in `[Backend]`.
- **Multi-user**: `Auth__AllowRegistration=true` plus email settings for confirmation and password reset.
- **Orchestrators** (Kubernetes, managed hosting): the same variables, provided as secrets of the platform.

## To do / open questions

- ✅ `[Backend]` Connection string: read from the final configuration when the first `DbContext` is created; startup still fails at once without it (migrations run before serving requests) — [backend/testing.md](../backend/testing.md#hosts).
- ✅ `[Backend]` Options class and validation at startup for `Auth` (`AuthOptions`).
- ✅ Data Protection keys: in the database, no variable ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)).
- ✅ Production variables of the `api` service — in `compose.app.yaml` ([ADR-0032](../architecture/adr/0032-container-images.md)).
- Next block: variables of the reverse proxy and HTTPS (e.g. trusted proxy networks for forwarded headers).

---
_Last updated: 2026-11-28_
