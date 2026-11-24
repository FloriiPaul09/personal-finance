# CI/CD

## Scope

The GitHub Actions workflows: which jobs run, when, with which tools; how coverage is reported; the pull request title check; the checks required to merge into `main`. Releases and published images are added in a later block ([README.md](README.md#to-do--open-questions)).

## Current state

### Workflows

| Workflow | File | Runs on | Jobs |
|---|---|---|---|
| CI | [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) | every pull request, every push to `main`, by hand (`workflow_dispatch`) | `backend`, `frontend`, `api-contract`, `e2e`, `compose`, `coverage`, `ci` |
| PR title | [`.github/workflows/pr-title.yml`](../../.github/workflows/pr-title.yml) | pull requests: opened, edited, reopened, new commits | `pr-title` |

```
pull request / push to main
 ├─ backend ─────┐
 ├─ frontend ────┴─► coverage ─┐
 ├─ api-contract ──────────────┤
 ├─ e2e ───────────────────────┼─► ci   (required)
 └─ compose ───────────────────┘
pull request (title)
 └─ pr-title                         (required)
```

Every job runs on `ubuntu-24.04`, which has Docker. A new push to a pull request cancels the run still going for the previous one; runs on `main` always finish.

### Jobs

| Job | What it does | Commands (same as on a developer machine) | Time |
|---|---|---|---|
| `backend` | Build with warnings as errors (`CI=true`), every test project, coverage | in `backend/`: `dotnet build PersonalFinance.slnx`, `dotnet test --no-build --coverage …` | ~5 min |
| `frontend` | Lint with module boundaries, unit tests with coverage, production build (bundle budget) | in `frontend/`: `pnpm lint`, `pnpm test:coverage`, `pnpm build` | ~3 min |
| `api-contract` | The committed OpenAPI snapshot and `schema.ts` match the API | in `backend/`: `dotnet build src/Api -p:OpenApiGenerateDocuments=true`; in `frontend/`: `pnpm api:types --file ../backend/artifacts/openapi/openapi.json`; `git diff --exit-code` on both files | ~2 min |
| `e2e` | Playwright on the real stack, phone and desktop | `docker compose up --detach --wait db`, `dotnet build src/Api`, `pnpm exec playwright install --with-deps chromium`, `pnpm e2e` | ~8 min |
| `compose` | `compose.yaml` is valid without `.env` and with every variable of `.env.example` | `docker compose config --quiet` twice | < 1 min |
| `coverage` | One report for backend and frontend in the run summary | ReportGenerator on the artifacts of `backend` and `frontend` | < 1 min |
| `ci` | Fails when any job above failed or was cancelled | `needs` of every job, results checked with `jq` | seconds |
| `pr-title` | The title follows `type(area[/module]): description` | `pnpm lint-title` in `.github/commitlint/` | < 1 min |

Times are estimates for a run with warm caches.

- **Toolchain** from the repository, never repeated in the workflows: .NET SDK from `backend/global.json` (`actions/setup-dotnet`), Node.js from `frontend/.nvmrc`, pnpm from `packageManager` in `frontend/package.json` (`pnpm/action-setup`). Two local composite actions set them up: [`.github/actions/setup-backend`](../../.github/actions/setup-backend/action.yml) (SDK + NuGet cache keyed on `Directory.Packages.props`, the project files and `dotnet-tools.json`) and [`.github/actions/setup-frontend`](../../.github/actions/setup-frontend/action.yml) (pnpm, Node.js with the pnpm store cache, `pnpm install --frozen-lockfile`).
- **.NET commands run in `backend/`**: `global.json` is found only from there. From the repository root `dotnet` picks the newest SDK installed and `dotnet test` falls back to VSTest, which does not understand the Microsoft Testing Platform options.
- **Integration tests** (`Infrastructure.Tests`, `Api.Tests`) start PostgreSQL with Testcontainers on the runner's Docker, with the image read from `compose.yaml` ([ADR-0011](../architecture/adr/0011-persistence-testing.md)): nothing else to configure.
- **End-to-end**: the database comes from `compose.yaml` (same image and development defaults as locally), the API is built first so that Playwright's `dotnet run` only starts it; `playwright.config.ts` starts the API and the dev server, never reuses running ones, and retries a failed test once when `CI` is set ([frontend/testing.md](../frontend/testing.md#end-to-end-playwright)). On failure the HTML report and the traces are uploaded as the `playwright-report` artifact (7 days).

### Coverage

Measured, shown, never enforced ([ADR-0006](../architecture/adr/0006-testing-strategy.md)).

- **Backend**: every test project references `Microsoft.Testing.Extensions.CodeCoverage` (in `backend/tests/Directory.Build.props`); `dotnet test --coverage --coverage-output-format cobertura --coverage-settings tests/coverage.config.xml` writes one Cobertura file per test project. The settings turn off *static* instrumentation: it rewrites the assemblies on disk, and the architecture tests (ArchUnitNET) then read the rewritten ones and fail on dependencies that are not in the code. Dynamic (in-memory) instrumentation measures the same lines.
- **Frontend**: `pnpm test:coverage` writes `frontend/coverage/personal-finance/lcov.info` (`v8` provider).
- **Report**: the `coverage` job merges both with ReportGenerator and appends a Markdown table to the **summary of the run** (Actions → the run → Summary); the HTML report is the `coverage-report` artifact (14 days). Test projects, `PersonalFinance.Testing`, generated migrations and source-generated files are left out. The frontend appears as the assembly **`Default`** (the name ReportGenerator gives to lcov files).

Locally, the same backend report: `dotnet test --coverage --coverage-output-format cobertura --coverage-settings "$PWD/tests/coverage.config.xml" --results-directory TestResults` in `backend/` (the settings path must be absolute: each test application runs from its own output folder).

### Pull request title

With squash merge the title becomes the commit on `main` ([ADR-0009](../architecture/adr/0009-repository-workflow.md)). [`.github/commitlint/`](../../.github/commitlint/) is a small pnpm package (`@commitlint/cli` and `@commitlint/config-conventional`, versions in its lockfile, updated by Dependabot) with [`commitlint.config.mjs`](../../.github/commitlint/commitlint.config.mjs):

- types: `feat`, `fix`, `test`, `refactor`, `docs`, `chore`, `perf`, `build`, `ci`;
- scope required: an area (`backend`, `frontend`, `devops`, `docs`) or `backend/<module>` / `frontend/<module>` with a backend module (`accounts`, `auth`, `budgets`, `categories`, `dashboard`, `transactions`, `transfers`). A custom rule matches the whole scope, because commitlint's `scope-enum` splits scopes at `/` and would accept `accounts/backend`;
- the other rules of Conventional Commits (lower-case description, at most 100 characters).

Try a title locally, from `.github/commitlint/` after `pnpm install`:

```powershell
"feat(backend/accounts): add create endpoint" | pnpm lint-title
```

The title reaches the script through an environment variable, never inside the script text, so a crafted title cannot run commands. The check is its own workflow so that editing the title does not rerun the whole CI; Dependabot titles (`chore(backend): bump …`) pass.

### Required checks

The ruleset `Main` (Settings → Rules → Rulesets) requires **two checks**, both from GitHub Actions:

| Check | Workflow | Why |
|---|---|---|
| `ci` | CI | Summarizes every CI job: adding, renaming or filtering jobs never touches the ruleset |
| `pr-title` | PR title | Separate workflow, so it cannot be a `need` of `ci` |

- `ci` runs with `if: always()` and fails unless every job is `success` (or `skipped`, kept for path filters later). A job that never ran because the run was cancelled makes it fail, instead of leaving a *skipped* check, which GitHub counts as passing.
- **Branches need not be up to date** before merging: after a merge the other pull requests are not rebased and tested again; the CI on the push to `main` catches the rare break caused by two pull requests together.
- The other rules of the ruleset are unchanged ([repository-workflow.md](repository-workflow.md#protecting-main)).

### Security of the workflows

- `permissions: contents: read` on both workflows: the token can read the code and nothing else; no job comments, labels or pushes.
- Only `pull_request` triggers, never `pull_request_target`: pull requests from forks run without secrets and with a read-only token. The workflows use no secrets.
- `actions/checkout` with `persist-credentials: false`: the token is not left in `.git/config` for later steps.
- **Actions pinned to a full commit SHA**, with the version as a comment (`actions/checkout@3d3c42e… # v7.0.1`): a moved tag cannot change the code that runs. Dependabot updates the SHA and the comment together, weekly, grouped ([repository-workflow.md](repository-workflow.md#dependency-updates-dependabot)), for the workflows and the composite actions.
- Runner image pinned (`ubuntu-24.04`): an image change is a deliberate edit.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Every job on every pull request and push to `main`, E2E included | A broken critical flow never reaches `main`; Actions minutes are free on a public repository | [ADR-0030](../architecture/adr/0030-continuous-integration.md) |
| One summarizing `ci` job plus `pr-title` as required checks | The ruleset does not change when jobs change; a cancelled job never counts as passing | [ADR-0030](../architecture/adr/0030-continuous-integration.md) |
| Branches need not be up to date before merging | No rebase chains on Dependabot Mondays; `main` is tested after each merge | [ADR-0030](../architecture/adr/0030-continuous-integration.md) |
| Coverage in the run summary and as an artifact, no external service | No token, works the same for Dependabot and forks | [ADR-0030](../architecture/adr/0030-continuous-integration.md) |
| commitlint with its configuration in `.github/commitlint` | Rules readable and versioned, usable locally, versions updated by Dependabot | [ADR-0030](../architecture/adr/0030-continuous-integration.md) |
| Actions pinned to commit SHAs | Immutable; recommended by GitHub and OpenSSF Scorecard | [ADR-0030](../architecture/adr/0030-continuous-integration.md) |
| Dynamic coverage instrumentation only | Static instrumentation breaks the architecture tests | — |

## Tests

- The workflows are checked with [actionlint](https://github.com/rhysd/actionlint) before each change (`pip install actionlint-py`, then `actionlint .github/workflows/*.yml`).
- The commitlint configuration was run on every title of `main` before this check existed: all pass except one written before the convention (`Docs/database design`); wrong scopes (`accounts/backend`, `devops/accounts`), a missing scope, unknown types and capitalized types fail.
- The pipeline checks itself: the pull request that added it had to pass all its jobs. Building the backend as the CI does (`CI=true`) found a real problem while writing it: `TestAuthenticationHandler.Scheme` hid a member of the base class (`CS0108`), a warning locally and an error in CI; the constant is now `SchemeName`.
- Coverage with static instrumentation made `Architecture.Tests` fail (the Infrastructure assembly, rewritten on disk, appeared to depend on the Api assembly); with `tests/coverage.config.xml` the architecture tests pass with coverage on.
- The `api-contract` commands were run locally on `main` and leave no diff.

## Scalability

- **Slower suite**: path filters (`dorny/paths-filter` or `on.pull_request.paths` in a job condition) skip jobs a change cannot affect; `ci` already accepts `skipped`. E2E can be sharded (`--shard`) across a matrix.
- **More contributors**: one required approval and `CODEOWNERS`; pull requests from forks already run safely (no secrets, read-only token).
- **Trend of coverage**: a service like Codecov can be added later without changing the tests; the Cobertura and lcov files are already produced.
- **Windows**: the CI runs on Linux only (Testcontainers needs Linux containers, which Windows runners do not offer); Windows-specific problems are documented in [local-development.md](local-development.md).

## To do / open questions

- Next DevOps blocks: dependency security (Dependabot alerts and security updates), images, exposure and backups, release workflow from tags ([README.md](README.md#to-do--open-questions)).

---
_Last updated: 2026-11-24_
