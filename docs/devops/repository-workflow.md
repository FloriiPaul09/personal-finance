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
| NuGet | `backend/` | minor + patch together | Majors of the packages that follow .NET: `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, `Microsoft.EntityFrameworkCore.*`, `Npgsql*`, `EFCore.NamingConventions`, `dotnet-ef` |
| pnpm (`npm` ecosystem) | `frontend/` | Angular packages together; other minor + patch together | Angular, Angular DevKit, TypeScript and pnpm majors |
| Docker Compose | `/` | — | PostgreSQL majors |
| Docker (Dockerfiles) | `backend/`, `frontend/` | minor + patch together | .NET (`dotnet/sdk`, `dotnet/aspnet`) and Node.js majors |
| commitlint (`npm` ecosystem) | `.github/commitlint/` | all together | pnpm majors |
| GitHub Actions | `/` and `.github/actions/*` (composite actions) | all together; SHA and version comment updated together | — |

- Weekly on Monday morning (Europe/Rome), commit prefix `chore(<area>)`.
- **A new .NET major is one planned change** (SDK in `global.json`, the packages above, runtime images, CI), with its own ADR: Dependabot never proposes it, as for Angular and PostgreSQL ([ADR-0031](../architecture/adr/0031-dependency-security.md)).
- npm updates wait 3 days after a release (cooldown): pnpm 11 refuses packages younger than a day, and fresh releases are the riskiest.
- **pnpm and Dependabot**: Dependabot supports pnpm 11 and 12, but with a pnpm-12 lockfile written as two YAML documents it loses alerts and security updates. The project stays on pnpm 11, whose lockfile is a single document, until that limitation is gone.

### Security updates

On since 2026-11-24 (Settings → Advanced Security, [ADR-0031](../architecture/adr/0031-dependency-security.md)):

| Setting | State | Effect |
|---|---|---|
| Dependency graph | on (default for public repositories) | Needed by alerts and by the dependency review of the CI |
| Dependabot alerts | on | A known vulnerability in a dependency, also transitive, is listed under Security → Dependabot |
| Dependabot security updates | on | Each alert opens a pull request with the fixed version at once, not on Monday |
| Grouped security updates | on | One security pull request per ecosystem and folder, so related packages are fixed together |
| Malware alerts | on | A dependency that is a known malicious package (mostly npm supply-chain attacks) is listed under Security → Dependabot |

Security pull requests use the same `commit-message` prefix and go through the whole CI. On pull requests the CI also runs the **dependency review**, which fails when a dependency added or changed by the pull request has a known vulnerability ([ci-cd.md](ci-cd.md#jobs)).

### Dismissed alerts

An alert that cannot be fixed yet and does not reach the app is dismissed in GitHub as *Risk is tolerable*, with the reason as a comment, and listed here; the fix arrives with the weekly version updates. Review the list when the parent package is updated.

| Alert | Package | Advisory | Why it is tolerable | Fixed when | Since |
|---|---|---|---|---|---|
| #1 | `minimatch` < 11.0.1 (npm, `frontend/`) | GHSA-g3hv-75wg-m249 / CVE-2026-118764, moderate: denial of service through amplified glob parts | Lint tooling only (`angular-eslint` → `typescript-eslint` → `minimatch`): it runs in `pnpm lint` on our own ESLint patterns, never in the app bundle and never on input from users | `typescript-eslint` depends on `minimatch` 11.0.1 or later | 2026-11-26 |

### Security fixes in transitive packages

Normally a security update fixes a vulnerable transitive package by itself. When it cannot (e.g. the direct dependency has no fixed version yet), the vulnerability shows as restore warnings `NU1901`–`NU1904`, which become errors in CI, and is fixed by hand:

1. Add the fixed version to `Directory.Packages.props` under `Security pins`.
2. Reference the package directly in the lowest project that pulls it in (today `Infrastructure`), with a comment naming the advisories: projects that reference it get the fixed version too.
3. Remove the pin when the direct dependency depends on the fixed version (Dependabot keeps the pin itself up to date meanwhile).

Each pin is listed here (package, fixed version, advisories, pulled in by, since) until it is removed. **Current pins: none.**

**History**: from 2026-11-12 the table held `System.Security.Cryptography.Pkcs` and `.Xml` 10.0.13 (CVE-2026-84008, CVE-2026-70162, CVE-2026-96958/96959/96960), brought at 10.0.11 by ASP.NET Core Data Protection. Removed on 2026-11-26, after Dependabot #48 brought ASP.NET Core 10.0.15, which depends on both at 10.0.15: `dotnet list package --vulnerable --include-transitive` reports nothing without the pins. The same change removed a direct `Npgsql` reference that #48 had added to `Infrastructure` (the EF Core provider already brings Npgsql 10.0.4). Today the `Security pins` section is empty and absent.

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
| Alerts and grouped security updates on, manual pins only as a fallback, .NET majors as one planned change | Fixes arrive by themselves; no half-upgraded runtime | [ADR-0031](../architecture/adr/0031-dependency-security.md) |
| LF everywhere | Same files on Windows, macOS, Linux and in containers | — |

## Tests

- Pull requests cannot be merged unless `ci` and `pr-title` pass ([ci-cd.md](ci-cd.md#required-checks)).
- commitlint on pull request titles, checked against every title of `main` when it was introduced ([ci-cd.md](ci-cd.md#tests)).
- Dependabot pull requests go through the same checks as any other, version and security updates alike.
- Removing the pins was checked with `dotnet list package --vulnerable --include-transitive` (no vulnerable package) and `dotnet list package --include-transitive` (`Pkcs` and `Xml` at 10.0.15 from Data Protection).

## Scalability

- **Contributors**: one required approval, `CODEOWNERS` per folder, issue templates, a `SECURITY.md` with how to report vulnerabilities.
- **Faster CI**: path filters so a backend-only change does not run frontend jobs.
- **Changelog**: generated from Conventional Commits at release time (e.g. release-please), grouped by area.

## To do / open questions

- ✅ `FloriiPaul09/personal-finance` created, ruleset and merge settings configured.
- ✅ Required checks and commitlint — [ADR-0030](../architecture/adr/0030-continuous-integration.md). Release workflow: block 5 of `[DevOps]`.
- Move to pnpm 12 once Dependabot fully supports its lockfile.
- ✅ Dependabot alerts, security updates and grouped security updates enabled; manual pins removed ([ADR-0031](../architecture/adr/0031-dependency-security.md)).
- Move to a new .NET major when worth it (today .NET 10 LTS; .NET 11 released in November 2026): one pull request with SDK, packages, images and CI, and its ADR.
- Docker images (part 2): base them on a .NET 10 runtime with the October 2026 security fixes (10.0.13 or later).

---
_Last updated: 2026-11-28_
