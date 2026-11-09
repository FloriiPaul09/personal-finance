# Repository structure

## Scope

Layout of the monorepo: top-level folders and files, what each one owns, and the rules for keeping the repository clean as it grows. The internal structure of each part is documented in its own area (`backend-structure.md`, `frontend/app-structure.md`, `devops/`).

## Current state

Skeleton **created**, not yet published on GitHub. Repository name: **`personal-finance`** ([ADR-0009](adr/0009-repository-workflow.md)).

```
personal-finance/
├── .github/
│   ├── dependabot.yml          # weekly grouped updates → devops/repository-workflow.md
│   ├── pull_request_template.md # Definition of Done checklist
│   └── workflows/              # CI with GitHub Actions (part 2) → devops/ci-cd.md
├── backend/
│   ├── PersonalFinance.slnx
│   ├── global.json             # .NET SDK + Microsoft Testing Platform
│   ├── Directory.Build.props   # shared build settings
│   ├── Directory.Packages.props # NuGet versions (Central Package Management)
│   ├── src/
│   │   ├── Api/                # PersonalFinance.Api — vertical slices by module (ADR-0002, ADR-0003)
│   │   ├── Domain/             # PersonalFinance.Domain
│   │   └── Infrastructure/     # PersonalFinance.Infrastructure
│   └── tests/
│       ├── Domain.Tests/       # PersonalFinance.Domain.Tests
│       ├── Api.Tests/          # PersonalFinance.Api.Tests
│       └── Architecture.Tests/ # PersonalFinance.Architecture.Tests
├── frontend/                   # Angular 22 workspace, pnpm, Vitest → frontend/app-structure.md
├── docs/                       # documentation by macro/micro category
├── compose.yaml                # db today; api and frontend (nginx) later → devops/docker.md
├── .env.example                # every variable, with placeholder values → devops/configuration.md
├── .editorconfig               # formatting shared by .NET and Angular
├── .gitattributes              # LF line endings, binary files
├── .gitignore                  # includes .env
├── LICENSE                     # MIT (ADR-0005)
├── CONTRIBUTING.md             # workflow, commits, tests; contributions under MIT (ADR-0005)
└── README.md                   # what it is, quick start, tests, links to docs/
```

### Rules

- **No secrets in the repository**: `.env` is ignored; `.env.example` is versioned and lists every variable with placeholder values. The only versioned values are documented development-only defaults ([devops/configuration.md](../devops/configuration.md)).
- **Each top-level area owns its tooling**: .NET in `backend/` (SDK pinned in `global.json`), pnpm in `frontend/` (Node.js in `.nvmrc`, pnpm in `packageManager`). The root only holds orchestration (Compose, CI) and project-wide files.
- **Quick start stays one command**: today `docker compose up -d db` plus the native API and frontend; `docker compose up` for the whole app once the images exist ([ADR-0008](adr/0008-local-development-environment.md)).
- **Documentation lives with the code**: `docs/` mirrors the conversation macros; a change that alters behavior updates its docs in the same pull request.
- **Commits** follow Conventional Commits with `type(area[/module])` ([devops/repository-workflow.md](../devops/repository-workflow.md)).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Monorepo with `backend/`, `frontend/`, `docs/` | One place to clone, one version of the truth, end-to-end features in one pull request | Project context (stack), [ADR-0007](adr/0007-tech-stack.md) |
| Backend split into `src/` and `tests/` with three source projects | Structure of the .NET solution | [ADR-0002](adr/0002-backend-structure.md), [ADR-0006](adr/0006-testing-strategy.md) |
| `LICENSE` (MIT) and `CONTRIBUTING.md` at the root | Public repository open to contributions | [ADR-0005](adr/0005-license.md) |
| CI in `.github/workflows/` | GitHub Actions runs all tests | [ADR-0006](adr/0006-testing-strategy.md) |
| Name `personal-finance`, projects `PersonalFinance.*`, short folder names | Descriptive name; folders stay short in paths and docs | [ADR-0009](adr/0009-repository-workflow.md) |
| `compose.yaml` instead of `docker-compose.yml` | File name preferred by the Compose specification | [devops/docker.md](../devops/docker.md) |
| pnpm instead of npm | Faster, strict dependency resolution | [ADR-0008](adr/0008-local-development-environment.md) |

## Tests

- `dotnet test` in `backend/` and `pnpm test` in `frontend/` run on the skeleton ([devops/local-development.md](../devops/local-development.md#tests)).
- `Architecture.Tests` checks project dependencies; `PostgresImageTests` checks the PostgreSQL pin in `compose.yaml`.

## Scalability

- **Worker** (roadmap): `backend/src/Worker/` plus a new service in `compose.yaml`; no change at the top level.
- **More clients** (e.g. a native app): a new top-level folder next to `frontend/`, with its own tooling.
- **Shared API contract**: the frontend client can be generated from the backend OpenAPI document instead of being written by hand.
- **More contributors**: `CODEOWNERS` per folder; CI path filters so a backend-only change does not run frontend jobs; issue templates in `.github/`; a Dev Container.
- **Releases**: tags and container images published from CI (`devops/ci-cd.md`).

## To do / open questions

- `[DevOps]` Publish the repository on GitHub and configure the ruleset on `main` ([devops/repository-workflow.md](../devops/repository-workflow.md#protecting-main)).
- `[DevOps]` Production Compose setup (required variables, HTTPS) when the `api` and `frontend` images exist.
- `[Frontend]` Location of Playwright end-to-end tests; generation of the API client from OpenAPI.

---
_Last updated: 2026-11-09_
