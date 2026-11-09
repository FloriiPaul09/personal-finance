# Local development

## Scope

How to set up a machine to work on the project, run each part locally and run the tests. Compose details are in [docker.md](docker.md), variables in [configuration.md](configuration.md).

## Current state

### Prerequisites

| Tool | Version | Pinned in |
|---|---|---|
| Docker (with Compose v2) | recent | — |
| .NET SDK | 10.0 (any feature band ≥ 10.0.100) | `backend/global.json` |
| Node.js | 26 LTS (24.15+ also supported) | `frontend/.nvmrc`, `engines` in `frontend/package.json` |
| pnpm | 11 | `packageManager` in `frontend/package.json` |

pnpm is installed with `npm install -g pnpm@11`. Corepack is not bundled with Node.js 25 and later, so `corepack enable` does not work out of the box.

Only the versions in the table are supported. Odd Node.js releases (23, 25) are not: pnpm 11 needs the built-in `node:sqlite` module and stops with `ERR_UNKNOWN_BUILTIN_MODULE` on older ones, and Angular 22 rejects them.

### Windows

The commands in this document work in PowerShell, with two differences from bash:

- **Windows PowerShell 5.1** (the one preinstalled) blocks `npm` and `pnpm` scripts and does not support `&&`. Allow local scripts once for your user, and run commands one per line or separated by `;`:

  ```powershell
  Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
  ```

  PowerShell 7 (`winget install Microsoft.PowerShell`) supports `&&` as well.
- **Node.js versions**: install the LTS with `winget install OpenJS.NodeJS.LTS`, or use [nvm-windows](https://github.com/coreybutler/nvm-windows) (`winget install CoreyButler.NVMforWindows`, then `nvm install 26` and `nvm use 26`) to switch between versions. Uninstall any Node.js installed separately before using nvm-windows, and reopen the terminal after installing either tool so the `PATH` is reloaded.
- **Line endings**: `.gitattributes` keeps every text file in LF, also on Windows; editors that respect `.editorconfig` (Visual Studio, Rider, VS Code) keep it that way.

### Running the app

```
┌────────────┐   /api proxy   ┌────────────┐                ┌────────────────────┐
│ ng serve   │ ─────────────► │ dotnet run │ ─────────────► │ PostgreSQL (Docker)│
│ :4200      │                │ :5080      │                │ 127.0.0.1:5432     │
└────────────┘                └────────────┘                └────────────────────┘
```

```bash
docker compose up -d db                      # database
dotnet watch --project backend/src/Api       # API with hot reload → http://localhost:5080
cd frontend && pnpm install && pnpm start    # frontend → http://localhost:4200
```

- The frontend calls the API on the same origin (`/api/...`): `frontend/proxy.conf.json` forwards those calls to `http://localhost:5080`. In containers, nginx plays the same role ([ADR-0001](../architecture/adr/0001-authentication.md)).
- The API listens on port 5080 (`backend/src/Api/Properties/launchSettings.json`); `GET /api/health` answers `Healthy` when it is up.
- No `.env` is needed: development defaults are shared by `compose.yaml` and `appsettings.Development.json` ([configuration.md](configuration.md)).
- Stop the database with `docker compose stop db`; `docker compose down -v` also deletes its data.

### Tests

| Area | Command (from the folder) | Runner |
|---|---|---|
| `backend/` | `dotnet test` | xUnit v3 on Microsoft Testing Platform, enabled in `global.json` |
| `frontend/` | `pnpm test` (watch) or `pnpm test --watch=false` (single run) | Vitest through the Angular `@angular/build:unit-test` builder, jsdom |

- `dotnet test` runs the three test projects of `PersonalFinance.slnx`. Integration tests that start PostgreSQL with Testcontainers need Docker running.
- Microsoft Testing Platform fails a run that executes zero tests: every test project keeps at least one test (`Domain.Tests` has a placeholder until the first business rule).

### Repository layout

```
backend/
├── PersonalFinance.slnx
├── global.json                 # SDK and test runner
├── Directory.Build.props       # shared settings (net10.0, nullable, warnings as errors in CI)
├── Directory.Packages.props    # NuGet versions (Central Package Management)
├── src/Api | Domain | Infrastructure
└── tests/Domain.Tests | Api.Tests | Architecture.Tests
frontend/                       # Angular 22 workspace, pnpm, Vitest
```

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Database in Docker, API and frontend on the host | Native hot reload and debugger, no development Dockerfiles | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| SDK, Node.js and pnpm pinned in the repository | Same toolchain for everyone, CI included | [ADR-0008](../architecture/adr/0008-local-development-environment.md) |
| xUnit v3 on Microsoft Testing Platform | Default runner of xUnit v3 4.x and of the .NET 10 SDK | [ADR-0006](../architecture/adr/0006-testing-strategy.md) |
| Vitest through the Angular builder | Default test runner of Angular | [ADR-0006](../architecture/adr/0006-testing-strategy.md) |

## Tests

This document is verified by following it on a fresh clone: both test commands must pass. CI (part 2) runs the same commands.

## Scalability

- **Worker**: `dotnet watch --project backend/src/Worker`, same database.
- **Contributors**: a Dev Container on top of `compose.yaml` removes the local prerequisites except Docker.
- **More test projects**: added to `PersonalFinance.slnx`, `dotnet test` picks them up; shared test helpers (such as `PostgresImage`) move to a test support project when a second project needs them.

## To do / open questions

- `[Frontend]` Playwright location and phone viewport; coverage provider for Vitest.
- `[Backend]` Testcontainers fixture using `PostgresImage.Name`, database reset between tests.
- Optional `pnpm` scripts at the repository root are not planned: each area owns its tooling.

---
_Last updated: 2026-11-09_
