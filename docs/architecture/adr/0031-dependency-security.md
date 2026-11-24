# ADR-0031: Dependency security and .NET version upgrades

- **Status**: Accepted
- **Date**: 2026-11-24
- **Deciders**: Paul

## Context

- Dependabot opens weekly version updates ([ADR-0009](0009-repository-workflow.md)); its alerts and security updates were not enabled. In November two transitive packages of ASP.NET Core Data Protection (`System.Security.Cryptography.Pkcs` and `.Xml` 10.0.11) were vulnerable: NuGet Audit turned the restore warnings into CI errors, and the fixed versions were pinned by hand in `Directory.Packages.props` (`Security pins`) with direct references in `Infrastructure`.
- ASP.NET Core 10.0.15 depends on `System.Security.Cryptography.Xml` 10.0.15, which depends on `.Pkcs` 10.0.15: with the direct packages up to date, the pins add nothing (`dotnet list package --vulnerable --include-transitive` reports nothing without them).
- .NET 11 was released in November 2026. Dependabot proposed `11.0.0` for single packages (`Npgsql`, `dotnet-ef`, `Microsoft.Extensions.ApiDescription.Server`, `System.Security.Cryptography.Pkcs`), closed by hand: a package of .NET 11 on the .NET 10 runtime, or half the packages on 11, is not a supported combination. The same rule already applies to Angular (`ng update`) and PostgreSQL (data migration).
- The CI ([ADR-0030](0030-continuous-integration.md)) fails on vulnerable NuGet packages (NuGet Audit, warnings as errors) but not on npm packages.
- The repository is public: alerts, security updates and the dependency review are free.

## Decision

1. **Dependabot alerts, security updates and grouped security updates** are on (Settings → Advanced Security). A vulnerability, also in a transitive package, opens a pull request at once, grouped per ecosystem, and goes through the whole CI like any other.
2. **The `Security pins` are removed** once ASP.NET Core 10.0.15 is on `main`. The procedure for pinning a transitive package by hand stays documented for a fix that security updates cannot make (e.g. no fixed version of the direct dependency yet).
3. **Dependency review on pull requests**: `actions/dependency-review-action` in the CI, a pull request job required through `ci`. It fails when the pull request adds or changes a dependency (npm, NuGet, GitHub Actions; runtime, development and unknown scopes) with a known vulnerability of moderate severity or more. Vulnerabilities published later for dependencies already on `main` are handled by alerts and security updates, so an unrelated pull request never turns red because of them.
4. **Majors that follow .NET are not updated by Dependabot**: `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, `Microsoft.EntityFrameworkCore.*`, `Npgsql*`, `EFCore.NamingConventions` and `dotnet-ef`. Moving to a new .NET major (SDK in `global.json`, these packages, runtime images, CI) is **one planned change with its own ADR**, decided when the new version is worth it: .NET 10 is the LTS the project is built on.

## Consequences

**Positive**
- Security fixes arrive by themselves, also for transitive packages: no more hand-made pins in the usual case.
- `Directory.Packages.props` lists only what the code uses.
- A pull request cannot bring a vulnerable npm package unnoticed, Dependabot's included.
- No more single-package `11.0.0` pull requests to close by hand; minor and patch updates of the same packages still come every week.

**Negative**
- A grouped security update that fails the CI holds back the other fixes of its group until it is fixed.
- The dependency review needs the dependency graph of GitHub (on by default for public repositories) and runs on pull requests only.
- A new .NET major is never proposed: it must be remembered (the `.NET` upgrade stays an open item in [devops/repository-workflow.md](../../devops/repository-workflow.md)).

## Alternatives considered

- **Alerts and ungrouped security updates**: one pull request per package; related packages (Data Protection, `Pkcs`, `Xml`) could arrive separately and fail to build alone.
- **Alerts only**: every fix by hand, as in November.
- **Keeping the pins as a floor**: harmless today, but direct references to packages the code does not use, to remember at the next .NET major.
- **`pnpm audit` and NuGet Audit on every run**: catches new vulnerabilities on unchanged dependencies too, but a CVE published overnight would turn every pull request red, documentation included; it duplicates the alerts.
- **No extra check**: a pull request could bring a vulnerable npm package without the CI saying so.
- **Letting Dependabot propose .NET majors and closing them**: noise every release, and a merged single-package major breaks the runtime combination.
