# ADR-0009: Repository workflow — branches, commits and dependency updates

- **Status**: Accepted
- **Date**: 2026-11-09
- **Deciders**: Paul

## Context

- Public monorepo with backend, frontend, documentation and orchestration in one place ([repository-structure.md](../repository-structure.md)).
- Pull requests are merged only when all tests are green ([ADR-0006](0006-testing-strategy.md)); the CI pipeline arrives in a second step.
- Two ecosystems (NuGet and pnpm) plus Docker images and GitHub Actions must stay up to date without daily manual work.
- One developer today, external contributors later: the history must stay readable and the rules must be the same for everyone.

## Decision

1. **Repository name**: `personal-finance`. .NET solution `PersonalFinance.slnx`, projects `PersonalFinance.Api`, `PersonalFinance.Domain`, `PersonalFinance.Infrastructure` and `PersonalFinance.<Name>.Tests`.
2. **GitHub Flow, strict**: `main` is protected by a ruleset; every change, the maintainer's included, goes through a pull request from a short-lived branch; required status checks must pass; **squash merge** only; releases are tags `vMAJOR.MINOR.PATCH` on `main`.
3. **Conventional Commits with area and module**: `type(area[/module]): description`, e.g. `feat(backend/accounts): …`. Areas: `backend`, `frontend`, `devops`, `docs`; modules follow the backend modules. The pull request title follows the same format, since it becomes the squash commit. Automatic checking (commitlint) arrives with the CI.
4. **Dependabot** for NuGet, pnpm, Docker Compose and GitHub Actions: weekly, grouped minor and patch updates, commit prefix with the area. Excluded from the bot: Angular and TypeScript majors (`ng update`), pnpm majors and PostgreSQL majors (planned changes).

## Consequences

**Positive**
- Every change on `main` has passed review and CI; the history has one readable commit per change.
- Scopes make it easy to filter history and to generate changelogs per area or module.
- Dependencies stay current with small, grouped pull requests; no external app to install.

**Negative**
- Even a typo fix needs a pull request.
- Squash merge hides the individual commits of a branch (they stay visible in the pull request).
- Dependabot is less flexible than Renovate, and its support for new pnpm majors can lag: pnpm majors are upgraded only once Dependabot supports them fully (alerts and security updates included).

**Follow-ups**
- Configure the ruleset on `main` when the repository is created ([repository-workflow.md](../../devops/repository-workflow.md#protecting-main)).
- Required status checks and commitlint with the CI (`[DevOps]` part 2).

## Alternatives considered

**GitHub Flow with direct pushes allowed for the maintainer** — Rejected. Faster alone, but problems are found by CI after they reach `main`, and the rules differ between maintainer and contributors.

**Git Flow** (`develop`, `release/*`, `hotfix/*`) — Rejected. Built for scheduled releases of versioned products; too heavy for a self-hosted app with one developer.

**Scope by area only** / **by module only** / **optional scope** — Rejected in favor of area plus module, which is more precise: the area alone says little about the feature, the module alone is ambiguous for infrastructure changes.

**Renovate** — Rejected. More powerful grouping and automerge, but an external app with a more complex configuration. **Manual updates with security alerts only** — Rejected: dependencies age and upgrades come in large jumps.

**Other repository names** (a short English brand, an Italian name) — Rejected in favor of a descriptive name that is clear to anyone searching for the project.
