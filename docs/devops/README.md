# DevOps

## Scope

Everything needed to build, run, test and ship the project outside the application code: the local development environment, Docker and Docker Compose, configuration through environment variables, the repository workflow (branches, commits, dependency updates) and the CI/CD pipeline.

## Micro categories

| Document | Covers | Status |
|---|---|---|
| [local-development.md](local-development.md) | Prerequisites, how to run database, API and frontend, ports, test commands | Decided — ADR-0008 |
| [docker.md](docker.md) | `compose.yaml`, the `db` service, PostgreSQL version pinning shared with Testcontainers | Decided — ADR-0008 |
| [configuration.md](configuration.md) | Environment variables, `.env.example`, development defaults | Decided — ADR-0008 |
| [repository-workflow.md](repository-workflow.md) | Repository name, branch strategy, commit format, dependency updates, security updates, formatting files | Decided — ADR-0009, ADR-0031 |
| [ci-cd.md](ci-cd.md) | GitHub Actions: jobs, when they run, coverage report, pull request title, required checks, security of the workflows | Decided — ADR-0030; releases in a later block |

## Current state

- Repository skeleton created: `backend/` (.NET 10 solution with `Api`, `Domain`, `Infrastructure` and three test projects), `frontend/` (Angular 22 workspace with pnpm and Vitest), `docs/`, `.github/` (Dependabot, pull request template), root files (`compose.yaml`, `.env.example`, `.editorconfig`, `.gitattributes`, `.gitignore`, `LICENSE`, `CONTRIBUTING.md`, `README.md`).
- Development environment: PostgreSQL in Docker, API and frontend run natively ([local-development.md](local-development.md)).
- Tests run with one command per area: `dotnet test` in `backend/`, `pnpm test` in `frontend/`.
- **CI** (part 2, block 1): GitHub Actions on every pull request and push to `main` — backend build and tests with coverage, frontend lint, unit tests with coverage and build, OpenAPI contract check, end-to-end tests, `docker compose config`, one coverage report; pull request titles checked by commitlint; `ci` and `pr-title` required by the ruleset of `main` ([ci-cd.md](ci-cd.md), [ADR-0030](../architecture/adr/0030-continuous-integration.md)).
- **Dependency security** (block 2): Dependabot alerts and grouped security updates on, manual pins of transitive packages removed, dependency review on pull requests, .NET majors excluded from Dependabot ([repository-workflow.md](repository-workflow.md#security-updates), [ADR-0031](../architecture/adr/0031-dependency-security.md)).
- Repository public on GitHub with the ruleset `Main` ([repository-workflow.md](repository-workflow.md#protecting-main)).
- Not yet: the `api` and `frontend` container images, the production setup, releases.

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
| 2026-11-24 | CI with GitHub Actions: every job on every pull request and push to `main`, E2E included; summarizing `ci` job + `pr-title` as required checks, branches not required up to date; coverage in the run summary; commitlint in `.github/commitlint`; actions pinned to commit SHAs | [ADR-0030](../architecture/adr/0030-continuous-integration.md) |
| 2026-11-24 | Dependabot alerts and grouped security updates, `Security pins` removed, dependency review on pull requests, majors of the packages that follow .NET excluded (a new .NET is one planned change with its ADR) | [ADR-0031](../architecture/adr/0031-dependency-security.md) |

## Tests

- The test setup itself is verified by running `dotnet test` and `pnpm test --watch=false` on a fresh clone ([local-development.md](local-development.md#tests)).
- `PostgresImageTests` (in `Infrastructure.Tests`) fails if the PostgreSQL image in `compose.yaml` is not pinned to `major.minor` or is Alpine-based.
- `docker compose config` validates `compose.yaml` with and without `.env` in the `compose` job of the CI ([ci-cd.md](ci-cd.md)).
- The CI runs every test of the repository on each pull request; `ci` and `pr-title` must pass before a merge.

## Scalability

- **More services**: a `Worker` runs natively in development and becomes a Compose service; the `api` and `frontend` services join when their images exist.
- **More contributors**: a Dev Container on top of `compose.yaml`; `CODEOWNERS`, issue templates and CI path filters in `.github/`.
- **Public instances**: a production Compose setup with required variables, HTTPS and a reverse proxy (to define, see To do).
- **Releases**: container images built and published by CI from version tags.

## To do / open questions

- ✅ Part 2, block 1: `ci-cd.md` and the GitHub Actions workflows (jobs, coverage, Docker for Testcontainers, E2E on every pull request, commitlint, `docker compose config`, required checks) — [ADR-0030](../architecture/adr/0030-continuous-integration.md).
- ✅ Block 2: Dependabot alerts and grouped security updates, pins removed, dependency review, .NET majors excluded — [ADR-0031](../architecture/adr/0031-dependency-security.md).
- Block 5: release workflow from `vX.Y.Z` tags, published images, changelog, final README.
- `api` and `frontend` Dockerfiles, nginx configuration, services in `compose.yaml` with required production variables.
- HTTPS and reverse proxy; how the app is reached from the phone (LAN/VPN vs public exposure) — from [authentication.md](../architecture/authentication.md).
- ✅ Storage of Data Protection keys: in the database, no volume needed ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md)).
- From `[Backend]`: HTTPS is required for login outside `localhost` (`Secure` cookies); forwarded headers behind the reverse proxy, so rate limiting sees client addresses.
- ✅ GitHub repository created, ruleset `Main` configured; required checks `ci` and `pr-title` added with the CI ([repository-workflow.md](repository-workflow.md#protecting-main)).

---
_Last updated: 2026-11-24_
