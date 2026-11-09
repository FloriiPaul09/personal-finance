# ADR-0007: Technology stack

- **Status**: Accepted
- **Date**: 2026-11-09 (recorded retroactively: the stack was chosen at project start, before ADR-0001)
- **Deciders**: Paul

## Context

- Web application for personal finance: daily use, also from a phone; single user today, multi-user on the roadmap.
- Public GitHub repository: anyone must be able to run it with `docker compose up`, with no secrets in the code.
- Money must be handled with exact decimal arithmetic.
- The project must stay maintainable for years by one developer and be approachable for contributors.

## Decision

| Layer | Choice |
|---|---|
| Frontend | Angular (latest stable), standalone components, signals, TypeScript strict |
| Backend | ASP.NET Core Web API on .NET (latest LTS), C# |
| Data access | Entity Framework Core with the Npgsql provider, code-first migrations |
| Database | PostgreSQL |
| Packaging | Docker images, orchestrated with Docker Compose (database, API, frontend served by nginx) |
| Repository | Monorepo on GitHub ([repository-structure.md](../repository-structure.md)) |

## Consequences

**Positive**
- Mature, actively maintained, open-source building blocks with permissive licenses (compatible with [ADR-0005](0005-license.md)).
- Exact money handling end to end: `decimal` in C#, `numeric` in PostgreSQL.
- Typed on both sides (C# and TypeScript strict); an API client can be generated from the OpenAPI document.
- Self-hosting is a single command; the same containers run locally, in CI and in production.
- .NET LTS releases give a predictable support window.

**Negative**
- Two ecosystems to keep updated (.NET and npm/Angular), each with regular major releases.
- Heavier than a single-language stack for a small app: contributors need to know both C# and TypeScript to work end to end.
- Docker is required to run the app and the integration tests ([ADR-0006](0006-testing-strategy.md)).

## Alternatives considered

Not formally evaluated: the stack was set as a starting constraint of the project. This record documents the choice and its consequences so that later decisions (ADR-0001 to ADR-0006) can refer to it. Replacing any layer requires a new ADR that supersedes the relevant part of this one.
