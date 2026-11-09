# DevOps

## Scope

Everything needed to build, run, test and ship the project outside the application code: the local development environment, Docker and Docker Compose, configuration through environment variables, the repository workflow (branches, commits, dependency updates) and the CI/CD pipeline.

## Micro categories

| Document | Covers | Status |
|---|---|---|
| [local-development.md](local-development.md) | Prerequisites, how to run database, API and frontend, ports, test commands | Decided — ADR-0008 |
| [docker.md](docker.md) | `compose.yaml`, the `db` service, PostgreSQL version pinning shared with Testcontainers | Decided — ADR-0008 |
| [configuration.md](configuration.md) | Environment variables, `.env.example`, development defaults | Decided — ADR-0008 |
| [repository-workflow.md](repository-workflow.md) | Repository name, branch strategy, commit format, dependency updates, formatting files | Decided — ADR-0009 |
| `ci-cd.md` | GitHub Actions: jobs, test execution, coverage, releases | Not started (part 2) |

## Current state

- Repository skeleton created: `backend/` (.NET 10 solution with `Api`, `Domain`, `Infrastructure` and three test projects), `frontend/` (Angular 22 workspace with pnpm and Vitest), `docs/`, `.github/` (Dependabot, pull request template), root files (`compose.yaml`, `.env.example`, `.editorconfig`, `.gitattributes`, `.gitignore`, `LICENSE`, `CONTRIBUTING.md`, `README.md`).
- Development environment: PostgreSQL in Docker, API and frontend run natively ([local-development.md](local-development.md)).
- Tests run with one command per area: `dotnet test` in `backend/`, `pnpm test` in `frontend/`.
- Not yet: the GitHub repository itself, its ruleset, the CI pipeline, the `api` and `frontend` container images.

## Decisions

| Date | Decision | Reference |
|---|---|---|
| 2026-11-09 | Database in Docker, applications on the host; one `compose.yaml` that later also runs the whole app | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| 2026-11-09 | Development defaults versioned in Compose and `appsettings.Development.json`; `.env` for overrides and real instances | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| 2026-11-09 | PostgreSQL 18, Debian image pinned to `major.minor`, Compose as the single source read by Testcontainers | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| 2026-11-09 | Toolchain pinned: .NET 10 SDK (`global.json`), Node.js 26 LTS (`.nvmrc`), pnpm 11 (`packageManager`) | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| 2026-11-09 | Repository `personal-finance`, projects `PersonalFinance.*` | [ADR-0009](../architecture/adr/0009-repository-workflow.md) |
| 2026-11-09 | GitHub Flow with protected `main`, pull requests for everyone, squash merge, tags for releases | [ADR-0009](../architecture/adr/0009-repository-workflow.md) |
| 2026-11-09 | Conventional Commits `type(area[/module]): description` | [ADR-0009](../architecture/adr/0009-repository-workflow.md) |
| 2026-11-09 | Dependabot for NuGet, pnpm, Docker Compose, GitHub Actions; majors of Angular, TypeScript, pnpm and PostgreSQL excluded | [ADR-0009](../architecture/adr/0009-repository-workflow.md) |

## Tests

- The test setup itself is verified by running `dotnet test` and `pnpm test --watch=false` on a fresh clone ([local-development.md](local-development.md#tests)).
- `PostgresImageTests` (in `Api.Tests`) fails if the PostgreSQL image in `compose.yaml` is not pinned to `major.minor` or is Alpine-based.
- `docker compose config` validates `compose.yaml`; it runs in CI from part 2.

## Scalability

- **More services**: a `Worker` runs natively in development and becomes a Compose service; the `api` and `frontend` services join when their images exist.
- **More contributors**: a Dev Container on top of `compose.yaml`; `CODEOWNERS`, issue templates and CI path filters in `.github/`.
- **Public instances**: a production Compose setup with required variables, HTTPS and a reverse proxy (to define, see To do).
- **Releases**: container images built and published by CI from version tags.

## To do / open questions

- Part 2: `ci-cd.md` and the GitHub Actions workflow (jobs on pull requests vs `main`, coverage, Docker for Testcontainers, E2E timing, commitlint, `docker compose config`).
- `api` and `frontend` Dockerfiles, nginx configuration, services in `compose.yaml` with required production variables.
- HTTPS and reverse proxy; how the app is reached from the phone (LAN/VPN vs public exposure) — from [authentication.md](../architecture/authentication.md).
- Storage of Data Protection keys (with `[Backend]`).
- Create the GitHub repository and configure the ruleset on `main` ([repository-workflow.md](repository-workflow.md#protecting-main)).

---
_Last updated: 2026-11-09_
