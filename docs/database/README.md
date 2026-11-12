# Database & domain

## Scope

The domain model of the application and its persistence: entities and business rules in the `Domain` project, PostgreSQL schema, EF Core configuration, isolation between users, migrations, default data, and the tests that verify them.

## Micro categories

| Document | Covers | Status |
|---|---|---|
| [domain-model.md](domain-model.md) | Entities, relationships, ER diagram, business rules (balance, transfers, budgets) | Implemented — ADR-0010 |
| [conventions.md](conventions.md) | Naming, types, keys, constraints, indexes, timestamps, concurrency, EF configuration | Implemented — ADR-0010 |
| [user-isolation.md](user-isolation.md) | Ownership marker, current user, query filter, composite foreign keys, interceptor | Implemented — ADR-0001, ADR-0010 |
| [migrations.md](migrations.md) | Where migrations live, how they are created, reviewed and applied | Implemented — ADR-0010 |
| [default-data.md](default-data.md) | Default categories created for every new user | Implemented — ADR-0010 |
| [testing.md](testing.md) | Domain unit tests, persistence tests, Testcontainers fixture, database per test class | Implemented — ADR-0011 |

## Current state

**Implemented**: the data model of the MVP is complete on `main`, one pull request per block:

| Block | Branch | Status |
|---|---|---|
| Design and documentation | `docs/database-design` | Merged (#4) |
| Test infrastructure (fixture, `Testing`, `Infrastructure.Tests`) | `feat/backend-test-infrastructure` | Merged (#5) |
| Domain entities and rules + unit tests | `feat/backend-domain-model` | Merged (#6) |
| `AppDbContext`, Identity, configurations, filter, interceptor, `InitialCreate` + persistence tests | `feat/backend-persistence` | Merged (#7) |
| Default categories + tests | `feat/backend-default-categories` | Merged (#8) |

In short:

- `Guid` keys (UUIDv7) everywhere, Identity included; snake_case tables (`accounts`, `transactions`, `users`).
- Signed amounts (`numeric(18,2)`); balance computed from the initial balance and the movements.
- Transfer = one `transfers` row and two legs in `transactions`.
- Monthly budgets as a history of entries per expense category (`effective_from`).
- Accounts and categories archived, never deleted when they have history.
- Isolation in three layers: named query filter `UserScope`, `SaveChanges` interceptor, composite foreign keys `(user_id, id)`.
- `data_protection_keys` (migration `AddDataProtectionKeys`, `[Backend]`, ADR-0014): keys of the session and antiforgery cookies; not user data.

## Decisions

| Date | Decision | Reference |
|---|---|---|
| 2026-11-10 | `Guid` keys (UUIDv7) everywhere, `IdentityUser<Guid>` | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| 2026-11-10 | snake_case naming with `EFCore.NamingConventions`, Identity tables renamed, one schema | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| 2026-11-10 | Named query filter `UserScope` + composite foreign keys `(user_id, id)` | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| 2026-11-10 | `UserId` in domain constructors, checked by the interceptor, which also sets timestamps | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| 2026-11-10 | Signed amounts; transfers as `transfers` + two legs; computed balances; budgets with `effective_from` | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| 2026-11-10 | Archive accounts and categories; tags with a link table; domain errors as exceptions | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| 2026-11-10 | Default categories per user; migrations applied at API startup, `dotnet-ef` as local tool | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| 2026-11-10 | One container per test run, migrated template, one database per test class; `Infrastructure.Tests` and `Testing` projects | [ADR-0011](../architecture/adr/0011-persistence-testing.md) |

## Tests

- Business rules: unit tests in `Domain.Tests` ([domain-model.md](domain-model.md#tests)).
- Schema, constraints, isolation, migrations, default data: integration tests on real PostgreSQL in `Infrastructure.Tests` ([testing.md](testing.md)).

## Scalability

- **Multi-user**: built in (every row has an owner, three isolation layers).
- **More data**: computed balances with indexes first, monthly snapshots later; `pg_trgm` for text search.
- **New features** (recurring transactions, CSV import, attachments): new tables with `user_id`, `(user_id, id)` keys and composite foreign keys; no change to existing rules.
- **Background processing**: the `Worker` uses the same `DbContext` with a fixed current user per scope.

## To do / open questions

- Refunds on expense categories ([domain-model.md](domain-model.md#scalability)).
- Questions for other areas are listed in each document (`[Backend]`: exposure of signed amounts — default categories for new users, concurrency and cross-user status decided in [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md); `[DevOps]`: CI with Docker, backups before migrations).

---
_Last updated: 2026-11-12_
