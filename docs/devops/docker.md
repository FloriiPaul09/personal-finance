# Docker

## Scope

Container images and Docker Compose: the services in `compose.yaml`, volumes and ports, and how the PostgreSQL version is pinned once and shared with integration tests.

## Current state

### `compose.yaml`

One file at the repository root (Compose project name `personal-finance`). Today it contains only the database:

| Service | Image | Port | Data |
|---|---|---|---|
| `db` | `postgres:18.6` (Debian) | `127.0.0.1:${POSTGRES_PORT:-5432}` → 5432 | volume `db-data` on `/var/lib/postgresql` |

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

- `backend/tests/Api.Tests/Infrastructure/PostgresImage.cs` finds `compose.yaml` by walking up from the test output folder and reads `services.db.image`.
- The tag is pinned to `major.minor` (`18.6`): reproducible across machines, updated by Dependabot with a pull request.
- The image is Debian-based, never Alpine: Alpine (musl) sorts and compares text differently from Debian (glibc), and tests must behave like production.
- Major upgrades (e.g. 18 → 19) are excluded from Dependabot: they need a data migration (`pg_dump`/restore or `pg_upgrade`) and are planned by hand.

### Images for the application

Not created yet. Target: an `api` image (multi-stage .NET build) and a `frontend` image (Angular build served by nginx, which also proxies `/api`), added to `compose.yaml` so that `docker compose up` starts the whole application.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Only `db` runs in development | Applications run natively with hot reload | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| PostgreSQL 18, `major.minor` tag, Debian | Mature, supported until 2030; reproducible; same text behavior as production | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| Compose as the single source of the version | One line to change, understood by Dependabot | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| `compose.yaml` instead of `docker-compose.yml` | File name preferred by the Compose specification | — |

## Tests

- `PostgresImageTests`: the image is read from the `db` service, pinned to `major.minor`, not Alpine.
- `docker compose config` validates the file (CI, part 2).
- Integration tests prove that the pinned version works with migrations and queries ([testing-strategy.md](../architecture/testing-strategy.md)).

## Scalability

- **More services**: `api`, `frontend` and later `worker` join the same file; development-only tools (e.g. a database UI) go under a Compose profile so they never start by default.
- **Extensions**: `pg_trgm` and `unaccent` (useful for text filters) are included in the official image and enabled by migrations. An extension outside the image would need a custom Dockerfile for `db`; only `PostgresImage` and the `db` service change.
- **Production**: published images (built by CI) instead of local builds, required variables, no database port published.

## To do / open questions

- `api` and `frontend` Dockerfiles and nginx configuration.
- Production setup: separate file or profile, HTTPS, reverse proxy, backups of the `db-data` volume.
- Upgrade path to PostgreSQL 19 when useful (not required before 2030).

---
_Last updated: 2026-11-09_
