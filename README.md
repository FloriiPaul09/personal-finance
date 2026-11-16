# Personal Finance

Self-hosted web app to track personal finances: accounts, income and expenses, categories, transfers and monthly budgets. Built to make adding an expense from a phone take a few seconds.

> **Status: early development.** The repository skeleton and development environment are in place; features are not implemented yet. The roadmap is in [docs/](docs/README.md).

## Stack

Angular · ASP.NET Core (.NET 10 LTS) · Entity Framework Core · PostgreSQL · Docker Compose — rationale in [ADR-0007](docs/architecture/adr/0007-tech-stack.md).

## Quick start (development)

Prerequisites: [Docker](https://docs.docker.com/get-docker/), [.NET 10 SDK](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org/) 26 LTS (or 24) and pnpm 11 (`npm install -g pnpm@11`).

```bash
git clone https://github.com/FloriiPaul09/personal-finance.git
cd personal-finance

# 1. Database (PostgreSQL in Docker)
docker compose up -d db

# 2. API → http://localhost:5080
dotnet run --project backend/src/Api

# 3. Frontend → http://localhost:4200 (calls to /api are proxied to the API)
cd frontend
pnpm install
pnpm start
```

No configuration is needed for local development: Compose and `appsettings.Development.json` share development-only defaults. To change them, copy `.env.example` to `.env`. Details in [docs/devops/local-development.md](docs/devops/local-development.md).

A one-command start of the whole application (`docker compose up`) arrives together with the API and frontend images.

## Tests

| Area | Command | Notes |
|---|---|---|
| Backend | `dotnet test` (from `backend/`) | Unit, integration and architecture tests. Integration tests need Docker running. |
| Frontend | `pnpm test` (from `frontend/`) | Vitest; watch mode in a terminal, single run with `pnpm test --watch=false`, coverage with `pnpm test:coverage`. Lint with `pnpm lint`. |

Testing strategy: [docs/architecture/testing-strategy.md](docs/architecture/testing-strategy.md).

## Documentation

All design decisions and technical documentation live in [docs/](docs/README.md), one folder per area, with Architecture Decision Records in [docs/architecture/adr/](docs/architecture/README.md#architecture-decision-records).

## Contributing

Contributions are welcome: read [CONTRIBUTING.md](CONTRIBUTING.md) first.

## License

[MIT](LICENSE). Contributions are accepted under the same license.
