# ADR-0008: Local development environment

- **Status**: Accepted
- **Date**: 2026-11-09
- **Deciders**: Paul

## Context

- The repository is public: anyone must be able to clone it and run it, and contributors must get a working environment quickly ([ADR-0007](0007-tech-stack.md)).
- Today there is one developer; tomorrow there may be contributors and more services (a `Worker` host is on the roadmap, [ADR-0002](0002-backend-structure.md)).
- Integration tests run against a real PostgreSQL through Testcontainers, which must use the same version as Docker Compose ([ADR-0006](0006-testing-strategy.md)).
- Fast feedback matters: hot reload and a debugger for both API and frontend.

## Decision

1. **Database in Docker, applications on the host.** One `compose.yaml` at the repository root. In development only the `db` service runs (`docker compose up -d db`); the API runs with `dotnet run` / `dotnet watch`, the frontend with `pnpm start`, which proxies `/api` to the API. The `api` and `frontend` services join the same file when their images exist, so `docker compose up` remains the one-command quick start.
2. **Versioned development defaults.** Compose and `appsettings.Development.json` share development-only values (database credentials, initial user), so a fresh clone runs without configuration. `.env` (from `.env.example`) overrides them and is required for any instance reachable by other people.
3. **PostgreSQL 18**, Debian-based official image, pinned to an explicit minor version (`postgres:18.6`).
4. **Compose is the single source of the PostgreSQL version.** Integration tests read the image of the `db` service from `compose.yaml`; a test fails if the tag is not pinned to `major.minor` or is Alpine-based. Dependabot updates the minor version; a major upgrade is a planned change.
5. **Toolchain pinned in the repository**: .NET SDK through `backend/global.json` (10.0, roll forward to the latest feature band), Node.js through `frontend/.nvmrc` (26 LTS; `engines` also accepts 24), **pnpm 11** through `packageManager` in `frontend/package.json`.

## Consequences

**Positive**
- Native hot reload and debugging for API and frontend; no development Dockerfiles to maintain.
- A new service (the `Worker`) runs natively like the API and becomes one more service in Compose.
- Clone and run: no secrets to create for local development.
- Local development, integration tests and CI always use the same PostgreSQL; a version bump is one line.
- PostgreSQL 18 is supported until November 2030: no forced major upgrade for years.

**Negative**
- Contributors need .NET SDK, Node.js, pnpm and Docker installed, not only Docker.
- Development values are in the repository: they must never reach a public instance (database bound to `127.0.0.1`, production variables required once the `api` service is added).
- Tests depend on the location of `compose.yaml` and on a YAML parser.
- pnpm is not bundled with Node.js 25+ (Corepack was removed): it must be installed explicitly.

## Alternatives considered

**Full stack in containers with `docker compose watch`** — Rejected. Only Docker needed, but slower hot reload (especially on Windows), harder debugging, and one development Dockerfile per service.

**Dev Container (`.devcontainer/`)** — Not adopted now. Zero setup for contributors and Codespaces support, but an extra layer while there is a single developer. It can be added later on top of the same `compose.yaml` without changing this decision.

**One `.env` loaded by the API too** / **`dotnet user-secrets`** — Rejected. The first needs extra code to load `.env` in .NET; the second keeps the database password in two places to align by hand.

**PostgreSQL 19** — Rejected for now. General availability on 19 November 2026: starting on a fresh `.0` brings no benefit to this project.

**PostgreSQL version in a Dockerfile, or a constant in tests checked against Compose** — Rejected. The Dockerfile becomes useful only if an extension outside the official image is needed (the switch stays local to the test helper and the `db` service); the constant means two places to update.

**npm** — Rejected in favor of pnpm: faster installs, strict dependency resolution (no access to undeclared packages), supported by Dependabot.
