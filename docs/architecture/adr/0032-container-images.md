# ADR-0032: Container images and the Compose setup of a real instance

- **Status**: Accepted
- **Date**: 2026-11-28
- **Deciders**: Paul

## Context

- [ADR-0008](0008-local-development-environment.md) runs only PostgreSQL in Docker during development, with versioned development defaults and no `.env`; the `api` and `frontend` services were to join with their images, nginx serving the app and proxying `/api`, with the variables of a real instance required.
- Compose interpolates the whole file before running any command: a required variable (`${VAR:?}`) in `compose.yaml` makes even `docker compose up -d db` fail without `.env`, with or without profiles (checked with Compose 5.7). Testcontainers reads the PostgreSQL image from `compose.yaml` ([ADR-0011](0011-persistence-testing.md)).
- The API applies migrations and creates the initial user at startup ([ADR-0010](0010-data-model-and-user-isolation.md), [ADR-0014](0014-authentication-endpoints-and-sessions.md)); its session cookie is always `Secure`, and the app and the API must share one origin (`SameSite=Strict`, [ADR-0001](0001-authentication.md)).
- The backend uses ASP.NET Core 10.0.15; .NET majors are a planned change ([ADR-0031](0031-dependency-security.md)). Node.js 26 builds the frontend; its production build has content-hashed file names, but an inline script and inline critical styles in `index.html`.
- How the app is reached from other devices (HTTPS, reverse proxy, VPN) is decided in the next block.

## Decision

1. **Two Compose files.** `compose.yaml` keeps only `db`, unchanged for development and tests. `compose.app.yaml` adds `api` and `frontend` and makes the secrets required (`POSTGRES_PASSWORD`, `Auth__InitialUser__*`); it also stops publishing the database port. `.env.example` sets `COMPOSE_FILE=compose.yaml:compose.app.yaml`, so on an instance `cp .env.example .env`, edit, `docker compose up -d` starts everything, while a clone without `.env` still starts only the database. The connection string of the API is built from the `POSTGRES_*` variables.
2. **API image**: multi-stage build on `mcr.microsoft.com/dotnet/sdk:10.0.404-noble`, runtime `mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled-extra` — Ubuntu chiseled, without shell or package manager, running as the non-root user `app` on port 8080, with ICU and time zones so text behaves as in the tests. No Docker health check (no HTTP client in the image): nginx answers `502` until the API is up, which the app already handles.
3. **Frontend image**: Node.js 26 builds the app with pnpm at the version of `packageManager`; `nginx/nginx-unprivileged:1.30.8-alpine` serves it as a non-root user on port 8080, proxies `/api/` to `api:8080` (resolved at each request through Docker's DNS), falls back to `index.html` for routes, answers `404` for missing files, caches hashed files for a year and `index.html` never, and adds security headers. No Content-Security-Policy yet (inline script and styles of the build).
4. **Exact tags**, updated by Dependabot (`docker` ecosystem), majors of .NET and Node.js excluded; the CI checks that the images follow `.nvmrc`, the .NET major of `global.json` and that the runtime is not older than the ASP.NET Core packages.
5. **CI**: an `images` job builds both images (layers in the GitHub Actions cache), starts the stack with `.env.example` and test values, and runs a smoke test through nginx (`.github/scripts/smoke-test.sh`): health after migrations, app and route fallback, `404`, headers, `401` without session, no OpenAPI in Production, login of the initial user and its default categories. It is part of the required `ci` check ([ADR-0030](0030-continuous-integration.md)).
6. The frontend is published on `127.0.0.1` by default (`APP_BIND`, `APP_PORT`): browsers accept the `Secure` session cookie over plain HTTP only on `localhost`, so other devices wait for HTTPS (next block).

## Consequences

**Positive**
- Development, tests and the `compose` CI job keep working without `.env`; a real instance cannot start with missing secrets.
- Small images with a reduced attack surface; no process runs as root; the database is not reachable from the host.
- A broken Dockerfile, nginx rule or production startup is caught in the pull request, with the same script usable on a real instance.
- A new release of the app is picked up at the next visit (`index.html` never cached) while unchanged files stay cached.

**Negative**
- Two Compose files to know; without `.env`, `docker compose up` starts only the database.
- No shell in the API container: debugging goes through `docker debug` or the logs.
- The `api` service has no health check; `docker compose up --wait` waits for it to run, not to be ready.
- The CI job adds two image builds per pull request (cached after the first).
- Until the next block, the app is usable only from the machine that runs it.

## Alternatives considered

- **One file with profiles, checks in the API**: one file, but Compose would start without secrets and the error would show only in the logs; the profile must be repeated on every command.
- **One file with required variables**: breaks development without `.env` (ADR-0008), the `compose` CI job and Testcontainers.
- **Standard `aspnet` image**: a shell for debugging and `curl` for a health check, but more packages to patch and root by default.
- **Chiseled without `extra`**: smaller, but invariant globalization differs from the tests and there are no time zones.
- **Official `nginx` image**: the master process runs as root.
- **Caddy**: automatic HTTPS would help the next block, but changes the nginx choice of ADR-0008 and needs a public domain, not decided yet.
- **Build only, or end-to-end tests on the containers**: building alone misses startup errors; Playwright on the containers is closest to production but slower and needs a rework when HTTPS arrives.
