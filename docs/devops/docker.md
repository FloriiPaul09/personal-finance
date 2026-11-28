# Docker

## Scope

Container images and Docker Compose: the services of `compose.yaml` and `compose.app.yaml`, the `api` and `frontend` images, nginx, volumes and ports, and how the PostgreSQL version is pinned once and shared with integration tests.

## Current state

### `compose.yaml`

At the repository root (Compose project name `personal-finance`), the development file. It contains only the database and needs no `.env`:

| Service | Image | Port | Data |
|---|---|---|---|
| `db` | `postgres:18.7` (Debian) | `127.0.0.1:${POSTGRES_PORT:-5432}` → 5432 | volume `db-data` on `/var/lib/postgresql` |

- **Port bound to `127.0.0.1`**: the database is reachable from the developer's machine, never from the network.
- **Volume on `/var/lib/postgresql`**: since PostgreSQL 18 the official image stores data in a per-major subfolder (`/var/lib/postgresql/18/docker`), which makes a future major upgrade possible next to the old data.
- **Health check** with `pg_isready`, so later services can wait for `service_healthy`.
- Credentials come from `POSTGRES_*` variables with development defaults ([configuration.md](configuration.md)).

### One PostgreSQL version for everything

The image of the `db` service is the **only place** where the version is written:

```
compose.yaml (db.image) ──► docker compose up        (development, quick start)
          │
          └────────────────► PostgresImage.Name ──► Testcontainers (integration tests, CI)
```

- `backend/tests/Testing/PostgresImage.cs` (shared test support project, [ADR-0011](../architecture/adr/0011-persistence-testing.md)) finds `compose.yaml` by walking up from the test output folder and reads `services.db.image`.
- The tag is pinned to `major.minor` (`18.7`): reproducible across machines, updated by Dependabot with a pull request. Check it by hand after each quarterly PostgreSQL release: on 2026-11-24 Dependabot closed its own 18.6 → 18.7 pull request as "up-to-date" (probably confused by PostgreSQL 19, released on 2026-11-19, whose major is ignored), and 18.7 (2026-11-12, 52 security fixes) was set by hand.
- The image is Debian-based, never Alpine: Alpine (musl) sorts and compares text differently from Debian (glibc), and tests must behave like production.
- Major upgrades (e.g. 18 → 19) are excluded from Dependabot: they need a data migration (`pg_dump`/restore or `pg_upgrade`) and are planned by hand.

### The whole application: `compose.app.yaml`

[ADR-0032](../architecture/adr/0032-container-images.md). A second file adds the application to the database of `compose.yaml`:

```
browser ──► frontend (nginx, :8080) ──/api/──► api (:8080) ──► db (:5432)
            published on APP_BIND:APP_PORT     not published     not published
            (default 127.0.0.1:8080)
```

| Service | Image | Port | Notes |
|---|---|---|---|
| `db` | as in `compose.yaml` | **not published** (`ports: !reset []`) | `POSTGRES_PASSWORD` required; inspect with `docker compose exec db psql -U personal_finance` |
| `api` | `${API_IMAGE:-personal-finance-api:local}`, built from `backend/` | none (only nginx reaches it) | starts after `db` is healthy; Production; connection string built from `POSTGRES_*`; `Auth__InitialUser__*` required |
| `frontend` | `${FRONTEND_IMAGE:-personal-finance-frontend:local}`, built from `frontend/` | `${APP_BIND:-127.0.0.1}:${APP_PORT:-8080}` → 8080 | health check with `wget` on `/` |

**Why two files**: Compose interpolates the whole file before any command, so a required variable (`${VAR:?}`) in `compose.yaml` would make `docker compose up -d db` fail without `.env`, profiles included. `compose.yaml` stays the development file; `.env.example` sets `COMPOSE_FILE=compose.yaml:compose.app.yaml`:

```powershell
cp .env.example .env      # Windows: Copy-Item .env.example .env — then replace every placeholder
docker compose up -d      # builds the images the first time, then db + api + frontend
docker compose logs -f api
docker compose down       # keeps the data; down -v deletes it
```

Open http://localhost:8080 and sign in with `Auth__InitialUser__*`. The port stays on `127.0.0.1` until HTTPS is set up: the session cookie is `Secure`, and browsers accept it over plain HTTP only on `localhost`.

On the first start the API logs one `fail` of `Microsoft.EntityFrameworkCore.Database.Command`: EF Core reads `__EFMigrationsHistory` before creating it on an empty database. It is expected; the migrations follow. SQL commands are logged only from `Warning` outside Development (`appsettings.json`).

### API image (`backend/Dockerfile`)

| Stage | Image | What happens |
|---|---|---|
| build | `mcr.microsoft.com/dotnet/sdk:10.0.404-noble` | restore from the project files only (cached layer), then `dotnet publish -c Release` |
| runtime | `mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled-extra` | `dotnet PersonalFinance.Api.dll` as user `app`, HTTP on 8080, `Production` |

- **Chiseled** (Ubuntu "distroless"): no shell, no package manager, non-root user. **`extra`** adds ICU and time zones, so text comparison and casing behave as in the tests (which run with ICU).
- No Docker health check: the image has no HTTP client. nginx answers `502` while the API starts (migrations), which the app shows as `server.unavailable`. To debug: `docker compose logs api`, or `docker debug` for a shell next to the container.
- `.dockerignore` keeps tests, build outputs and `appsettings.Development.json` (development credentials) out of the image.

### Frontend image (`frontend/Dockerfile`, `frontend/nginx/`)

| Stage | Image | What happens |
|---|---|---|
| build | `node:26.13.1-trixie-slim` | pnpm at the version of `packageManager`, `pnpm install --frozen-lockfile`, `pnpm build` |
| runtime | `nginx/nginx-unprivileged:1.30.8-alpine` | nginx as a non-root user on 8080, `dist/personal-finance/browser` in `/usr/share/nginx/html` |

`nginx/default.conf`:

| Path | Behavior |
|---|---|
| `/api/` | Proxied to `http://api:8080` with `X-Forwarded-For` / `-Proto` / `-Host`; `api` resolved at each request (Docker DNS), so nginx starts before the API and follows a recreated container; bodies up to 1 MB |
| Hashed files (`main-KR3TOMMA.js`, `chunk-…`, `media/…woff2`) | `Cache-Control: public, max-age=31536000, immutable` |
| `/icons/` | Cached for a day (names do not change between releases) |
| Other files (`favicon.ico`, …) | Served if they exist, `404` otherwise — a missing script never gets `index.html` |
| Any other path | `index.html` (routes of the app), `Cache-Control: no-cache`: a new release is picked up at the next visit |

Every response carries `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin`, `Permissions-Policy` (camera, microphone, geolocation, payment off) and `Cross-Origin-Opener-Policy: same-origin` (`nginx/security-headers.conf`, included in each location because `add_header` in a location replaces the inherited ones). No Content-Security-Policy yet: the build inlines a script and the critical styles in `index.html`.

### Versions of the images

Exact tags, updated by Dependabot (`docker` ecosystem, `backend/` and `frontend/`, minor and patch grouped). .NET and Node.js majors are excluded: they follow `global.json` and `.nvmrc` ([ADR-0031](../architecture/adr/0031-dependency-security.md)). The CI fails when the Dockerfiles drift: Node.js not the major of `.nvmrc`, .NET not the major of `global.json`, or a runtime older than the ASP.NET Core packages (merge the Docker update first).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Only `db` runs in development | Applications run natively with hot reload | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| PostgreSQL 18, `major.minor` tag, Debian | Mature, supported until 2030; reproducible; same text behavior as production | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| Compose as the single source of the version | One line to change, understood by Dependabot | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| `compose.app.yaml` next to `compose.yaml`, selected by `COMPOSE_FILE` in `.env`; secrets required there; database port not published | Development unchanged without `.env`; an instance cannot start without its secrets | [ADR-0032](../architecture/adr/0032-container-images.md) |
| API on `aspnet` chiseled-extra, frontend on `nginx-unprivileged`, exact tags | Minimal attack surface, no root, same text behavior as the tests | [ADR-0032](../architecture/adr/0032-container-images.md) |
| `compose.yaml` instead of `docker-compose.yml` | File name preferred by the Compose specification | — |

## Tests

- `PostgresImageTests`: the image is read from the `db` service, pinned to `major.minor`, not Alpine.
- `docker compose config` validates the file with and without `.env` (the `compose` job of the CI, [ci-cd.md](ci-cd.md#jobs)); the E2E job starts `db` from this file, so a wrong tag fails the CI (PostgreSQL 18.7 was checked this way).
- Integration tests prove that the pinned version works with migrations and queries ([testing-strategy.md](../architecture/testing-strategy.md)).
- The `images` job of the CI builds both images, starts the stack of `compose.app.yaml` with `.env.example` and test values, and runs `.github/scripts/smoke-test.sh` through nginx; it also checks that the database port is not published ([ci-cd.md](ci-cd.md#jobs)).
- Before the first CI run, checked locally without containers (Docker Hub and MCR unreachable from the work environment): the `dotnet publish` of the Dockerfile, the API in Production on an empty PostgreSQL (migrations, initial user, default categories) behind nginx with `default.conf`, the smoke test passing on that stack and failing with a wrong password, `nginx -t`, and `docker compose config` with and without `.env` (and with an empty `POSTGRES_PASSWORD`, refused). In the first CI run the images built and the smoke test passed; the check on the database port failed by mistake (`docker compose port` prints `:0` for an unpublished port) and now reads the ports column of `docker compose ps`. Found and fixed locally: hashed chunk names can contain `-`; `expires` doubled `Cache-Control`; a missing `.js` got `index.html`; icons fell into the regex location; EF Core logged every SQL command in Production.

## Scalability

- **More services**: a `worker` joins `compose.app.yaml` with the same variables as `api`; development-only tools (e.g. a database UI) go under a Compose profile so they never start by default.
- **Extensions**: `pg_trgm` and `unaccent` (useful for text filters) are included in the official image and enabled by migrations. An extension outside the image would need a custom Dockerfile for `db`; only `PostgresImage` and the `db` service change.
- **Published images**: `API_IMAGE` / `FRONTEND_IMAGE` point to images built by the release workflow instead of local builds; nothing else changes.
- **Several API instances**: Data Protection keys are already in the database; nginx would list them in an `upstream`.

## To do / open questions

- ✅ `api` and `frontend` Dockerfiles, nginx configuration, `compose.app.yaml` ([ADR-0032](../architecture/adr/0032-container-images.md)).
- Next block: where the instance runs, HTTPS and the reverse proxy in front of nginx (forwarded headers for the rate limiting), access from the phone, backups of `db-data` (automatic and before migrations).
- Content-Security-Policy: needs the hashes of the inline script and styles of the build, or turning off critical CSS inlining.
- Data Protection keys are stored unencrypted in the database (startup warning): encrypt them with a certificate if the database ever leaves the machine.
- Upgrade path to PostgreSQL 19 (released 2026-11-19) when useful; PostgreSQL 18 is supported until November 2030, so it is not required before then.

---
_Last updated: 2026-11-28_
