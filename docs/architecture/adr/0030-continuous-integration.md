# ADR-0030: Continuous integration with GitHub Actions

- **Status**: Accepted
- **Date**: 2026-11-24
- **Deciders**: Paul

## Context

- The MVP is complete in backend and frontend; tests exist at every level of [ADR-0006](0006-testing-strategy.md): unit and architecture tests, integration tests on PostgreSQL with Testcontainers ([ADR-0011](0011-persistence-testing.md)), Vitest with `v8` coverage, Playwright on the real stack with phone and desktop projects ([ADR-0023](0023-frontend-authentication.md)).
- [ADR-0009](0009-repository-workflow.md) protects `main` with pull requests, squash merge and required checks, and asks for commitlint on pull request titles; the checks did not exist yet. Dependabot opens grouped pull requests every Monday.
- [ADR-0029](0029-precise-openapi-contract.md) makes the API write its OpenAPI document at build time, so CI can check that the committed snapshot and the generated types match the API.
- The repository is public: Actions minutes on standard runners are free, pull requests can come from forks, and the workflows are a supply-chain target.
- One maintainer today; contributors possible later.

## Decision

1. **One CI workflow, every job on every pull request and push to `main`**: `backend` (build with warnings as errors, all tests with coverage), `frontend` (lint, unit tests with coverage, production build), `api-contract` (export, regenerate, `git diff --exit-code`), `e2e` (Playwright on the real stack), `compose` (`docker compose config` with and without `.env`), `coverage` (report). **End-to-end tests run on every pull request**, not only on `main`: a broken login, quick entry, transfer or dashboard is stopped before the merge, Dependabot pull requests included.
2. **Required checks**: a summarizing job `ci` that needs every CI job and fails unless all succeeded (or were skipped on purpose), plus `pr-title` from its own workflow. `ci` runs with `if: always()`, so a failed or cancelled run never leaves a *skipped* required check, which GitHub counts as passing. **Branches need not be up to date** before merging.
3. **Coverage without an external service**: backend (Microsoft code coverage, Cobertura) and frontend (`v8`, lcov) merged by ReportGenerator into a table in the summary of the run and an HTML artifact. No threshold (ADR-0006).
4. **Pull request titles checked by commitlint**, configured in `.github/commitlint/` (own `package.json`, lockfile, `commitlint.config.mjs`, updated by Dependabot): types of `CONTRIBUTING.md`, scope required as `area` or `backend|frontend/<module>`, matched as a whole. In a separate workflow, so editing a title does not rerun the CI.
5. **Hardened workflows**: actions pinned to full commit SHAs with the version as a comment (Dependabot updates both); read-only `GITHUB_TOKEN`; only `pull_request` triggers; no secrets; checkout without persisted credentials; the title passed through an environment variable.
6. **Toolchain from the repository**: `backend/global.json`, `frontend/.nvmrc`, `packageManager`; two local composite actions set up backend and frontend once for every job.

## Consequences

**Positive**
- `main` stays green by construction: nothing is merged without build, tests, contract, end-to-end flows and a well-formed title.
- Changing the jobs never requires editing the ruleset; path filters can be added later without blocking pull requests.
- No accounts, tokens or third-party services: forks and Dependabot run the same pipeline.
- A tag moved by a compromised action cannot change what runs.
- The same commands as on a developer machine: a red job is reproduced locally with the line in [ci-cd.md](../../devops/ci-cd.md).

**Negative**
- Every pull request waits for the end-to-end tests (about 8 minutes); a flaky test blocks the merge (one retry on CI mitigates it).
- Coverage is read in the run summary, not as a pull request comment, and has no history.
- One more small npm package (`.github/commitlint`) and one more Dependabot entry.
- SHA-pinned lines are harder to read; patches of actions arrive only through Dependabot.
- Two pull requests green on their own can break `main` together; the run on `main` reports it, and a fix follows.

## Alternatives considered

- **End-to-end tests only on `main` (plus nightly)**: faster pull requests, but regressions reach `main` and Dependabot pull requests would be merged untested on the real stack.
- **End-to-end tests only when code paths change**: saves time on documentation-only pull requests, which are rare; a wrong filter (e.g. `compose.yaml`, `global.json`) would skip tests that matter.
- **Codecov**: pull request comments and history, but a third-party app and a token that Dependabot and forks cannot read.
- **Coverage as a pull request comment**: needs a writable token, unavailable to forks and Dependabot without `pull_request_target`, which is unsafe on a public repository.
- **`amannn/action-semantic-pull-request`**: no files in the repository, but the scope rule becomes a regex in the workflow and cannot be run locally.
- **commitlint through `npx` with a version in the workflow**: Dependabot does not see that version; no lockfile.
- **Every job as a required check**: explicit, but each new or renamed job means editing the ruleset, or pull requests wait forever for a missing check.
- **Branches up to date before merging**: safest, but each merge restarts the CI of every other pull request.
- **Actions pinned by major tag (`@v7`)**: readable, but tags are mutable (the 2025 `tj-actions/changed-files` compromise rewrote them).
