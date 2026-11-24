# Repository workflow

## Scope

How the GitHub repository is used: name, branches and pull requests, commit messages, dependency updates, and the shared formatting files (`.editorconfig`, `.gitattributes`, `.gitignore`).

## Current state

### Name

Repository [`FloriiPaul09/personal-finance`](https://github.com/FloriiPaul09/personal-finance). .NET solution `PersonalFinance.slnx` with `PersonalFinance.Api`, `PersonalFinance.Domain`, `PersonalFinance.Infrastructure` and `PersonalFinance.{Domain,Infrastructure,Api,Architecture}.Tests` plus the test support library `PersonalFinance.Testing`; folders keep short names (`src/Api`, `tests/Api.Tests`). The Angular project is also named `personal-finance`.

### Branches and pull requests (GitHub Flow)

```
main ──●────────────●────────────●──── (tag v0.1.0)
        \          / squash      /
         feat/backend-…        fix/frontend-…
```

- `main` always works and is the only long-lived branch.
- Work happens on short-lived branches named `<type>/<area>-<topic>`, e.g. `feat/backend-create-account`.
- Every change, the maintainer's included, is merged through a pull request that fills in the [template](../../.github/pull_request_template.md).
- **Squash merge** only: one commit per pull request on `main`, with the pull request title as message.
- Releases are tags `vMAJOR.MINOR.PATCH` on `main` (images published from tags in part 2).

### Protecting `main`

Ruleset `Main` (Settings → Rules → Rulesets, target `main`):

- restrict deletions and block force pushes;
- require a pull request before merging (0 approvals while there is one maintainer, 1 when contributors arrive);
- require status checks to pass: `ci` and `pr-title`, both from GitHub Actions; branches need not be up to date before merging ([ci-cd.md](ci-cd.md#required-checks), [ADR-0030](../architecture/adr/0030-continuous-integration.md));
- require linear history; allow only squash merge (Settings → General → Pull Requests) and delete head branches automatically.

### Commit messages

`type(area[/module]): description` — full rules and examples in [CONTRIBUTING.md](../../CONTRIBUTING.md#commit-messages).

- **area**: `backend`, `frontend`, `devops`, `docs`.
- **module**: the backend modules (`accounts`, `auth`, `budgets`, `categories`, `dashboard`, `transactions`, `transfers`), for `backend` and `frontend` only; omitted for cross-cutting changes.
- Pull request titles are checked by commitlint in CI (`pr-title`, [ci-cd.md](ci-cd.md#pull-request-title)); the list of modules lives in `.github/commitlint/commitlint.config.mjs`.

### Dependency updates (Dependabot)

Configured in [`.github/dependabot.yml`](../../.github/dependabot.yml):

| Ecosystem | Folder | Grouping | Not updated by the bot |
|---|---|---|---|
| NuGet | `backend/` | minor + patch together | — |
| pnpm (`npm` ecosystem) | `frontend/` | Angular packages together; other minor + patch together | Angular, Angular DevKit, TypeScript and pnpm majors |
| Docker Compose | `/` | — | PostgreSQL majors |
| commitlint (`npm` ecosystem) | `.github/commitlint/` | all together | pnpm majors |
| GitHub Actions | `/` and `.github/actions/*` (composite actions) | all together; SHA and version comment updated together | — |

- Weekly on Monday morning (Europe/Rome), commit prefix `chore(<area>)`.
- npm updates wait 3 days after a release (cooldown): pnpm 11 refuses packages younger than a day, and fresh releases are the riskiest.
- **pnpm and Dependabot**: Dependabot supports pnpm 11 and 12, but with a pnpm-12 lockfile written as two YAML documents it loses alerts and security updates. The project stays on pnpm 11, whose lockfile is a single document, until that limitation is gone.

### Security fixes in transitive packages

Dependabot version updates only bump packages listed in `Directory.Packages.props`. A vulnerability in a package that comes **transitively** (restore warnings `NU1901`–`NU1904`, which become errors in CI) is fixed by hand until the direct dependency ships the fix:

1. Add the fixed version to `Directory.Packages.props` under `Security pins`.
2. Reference the package directly in the lowest project that pulls it in (today `Infrastructure`), with a comment naming the advisories: projects that reference it get the fixed version too.
3. Remove the pin when the direct dependency depends on the fixed version (Dependabot keeps the pin itself up to date meanwhile).

| Package | Fixed version | Advisories | Pulled in by | Since |
|---|---|---|---|---|
| `System.Security.Cryptography.Pkcs` | 10.0.13 | CVE-2026-84008, CVE-2026-70162 | ASP.NET Core Data Protection 10.0.11 (`Infrastructure`) | 2026-11-12 |
| `System.Security.Cryptography.Xml` | 10.0.13 | CVE-2026-96958, CVE-2026-96959, CVE-2026-96960 | ASP.NET Core Data Protection 10.0.11 (`Infrastructure`) | 2026-11-12 |

Dependabot alerts and security updates are not enabled on the repository yet (see To do): with them, GitHub opens these pull requests by itself.

### Formatting files

| File | Content |
|---|---|
| `.editorconfig` | UTF-8, LF, 2 spaces by default, 4 for C#; C# style and naming rules, enforced in build (warnings fail the build in CI only) |
| `.gitattributes` | LF for every text file (CRLF for `.cmd`/`.bat`), binary types, lockfile marked as generated |
| `.gitignore` | .NET outputs, coverage, editors, `.env*` except `.env.example`; Angular rules in `frontend/.gitignore` |
| `frontend/.prettierrc` | Prettier settings generated by Angular |

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Name `personal-finance`, projects `PersonalFinance.*` | Clear to anyone searching for the project | [ADR-0009](../architecture/adr/0009-repository-workflow.md) |
| Strict GitHub Flow, squash merge, tags | Every change reviewed and tested, readable history | [ADR-0009](../architecture/adr/0009-repository-workflow.md) |
| Scope with area and module | Precise history and changelogs | [ADR-0009](../architecture/adr/0009-repository-workflow.md) |
| Dependabot, grouped weekly updates, majors excluded where a migration is needed | Integrated in GitHub, little noise | [ADR-0009](../architecture/adr/0009-repository-workflow.md) |
| LF everywhere | Same files on Windows, macOS, Linux and in containers | — |

## Tests

- Pull requests cannot be merged unless `ci` and `pr-title` pass ([ci-cd.md](ci-cd.md#required-checks)).
- commitlint on pull request titles, checked against every title of `main` when it was introduced ([ci-cd.md](ci-cd.md#tests)).
- Dependabot pull requests go through the same checks as any other.

## Scalability

- **Contributors**: one required approval, `CODEOWNERS` per folder, issue templates, a `SECURITY.md` with how to report vulnerabilities.
- **Faster CI**: path filters so a backend-only change does not run frontend jobs.
- **Changelog**: generated from Conventional Commits at release time (e.g. release-please), grouped by area.

## To do / open questions

- ✅ `FloriiPaul09/personal-finance` created, ruleset and merge settings configured.
- ✅ Required checks and commitlint — [ADR-0030](../architecture/adr/0030-continuous-integration.md). Release workflow: block 5 of `[DevOps]`.
- Move to pnpm 12 once Dependabot fully supports its lockfile.
- Enable **Dependabot alerts** and **Dependabot security updates** in the repository settings (Settings → Security), so vulnerable transitive packages arrive as pull requests.
- Docker images (part 2): base them on a .NET 10 runtime with the October 2026 security fixes (10.0.13 or later).

---
_Last updated: 2026-11-24_
