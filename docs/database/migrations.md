# Migrations

## Scope

How the schema evolves: where EF Core migrations live, how they are created and reviewed, when they are applied, and the rules that keep them safe for every self-hosted instance.

## Current state

Decided ([ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md)), **not implemented yet**: the first migration, `InitialCreate`, arrives with the branch `feat/backend-persistence`.

### Where they live

- Code-first migrations in `backend/src/Infrastructure/Persistence/Migrations`.
- `dotnet-ef` is pinned as a **local tool** (`backend/.config/dotnet-tools.json`): `dotnet tool restore` installs the same version for everyone.
- A design-time factory (`IDesignTimeDbContextFactory`) in `Infrastructure` builds the `DbContext` for the tools with the development connection string, overridable with `ConnectionStrings__Default`.

### Commands (from `backend/`)

```powershell
dotnet tool restore
# create a migration after changing the model
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Infrastructure
# SQL script, to review what a migration does
dotnet ef migrations script <From> <To> --project src/Infrastructure --startup-project src/Infrastructure
# apply to the development database (the API also does it at startup)
dotnet ef database update --project src/Infrastructure --startup-project src/Infrastructure
```

Names are PascalCase and describe the change: `InitialCreate`, `AddTransactionNotes`.

### When they are applied

- **At API startup**, before serving requests: `Database.MigrateAsync()`. EF Core takes a database lock while migrating, so two instances starting together do not apply the same migration twice.
- In development: `dotnet watch` on the API applies new migrations on restart.
- In tests: once per test run, on the template database ([testing.md](testing.md#fixture)).
- If a migration fails, the API does not start; the error is in the logs.

### Rules

- **Append-only once merged on `main`**: a merged migration is never edited or deleted; a fix is a new migration. Self-hosted instances may already have applied it.
- **One migration per pull request** when possible, with the model change that needs it.
- **Review the SQL** of every migration (script command above) before merging, especially renames: EF may generate drop + add instead of a rename, losing data.
- **Data changes** (backfills) go in the migration with `migrationBuilder.Sql(...)`, written to be safe on an empty database too.
- **No user data in migrations**: default categories belong to users and are created by code ([default-data.md](default-data.md)), not by `HasData`.
- `CHECK` constraints are declared in the EF configuration (`ToTable(t => t.HasCheckConstraint(...))`) so they are part of the model and of the snapshot.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Applied at API startup | `docker compose up` stays the whole installation | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| `dotnet-ef` as local tool | Same tool version for every contributor and CI | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| Append-only after merge | Instances in the wild may have applied any merged migration | This document |

## Tests

- **No pending model changes**: a test fails if the model differs from the last migration snapshot (someone forgot `migrations add`).
- **Migrations apply on an empty database**: every test run builds its template database by applying all migrations ([testing.md](testing.md#fixture)).
- Constraints created by migrations are verified by the persistence tests ([conventions.md](conventions.md#tests)).
- CI (`[DevOps]` part 2) runs the same tests.

## Scalability

- **Public instances with restricted rights**: produce a migration bundle (`dotnet ef migrations bundle`) and run it as a separate step or container before the API, which then needs no DDL rights. New ADR if adopted.
- **Large tables**: migrations that rewrite big tables (new non-null column, new index) need care — `CREATE INDEX CONCURRENTLY` through raw SQL, columns added nullable then backfilled.
- **Rollbacks**: not automated; a broken migration is fixed by a new one. Backups before upgrading are part of the production setup (`[DevOps]`).
- **Squashing**: if the history grows long, a new baseline can be introduced with a new ADR, keeping old instances upgradable.

## To do / open questions

- `[DevOps]` Database backup before upgrades in the production setup.
- `[DevOps]` CI check that `dotnet ef migrations has-pending-model-changes` is clean (in addition to the test).

---
_Last updated: 2026-11-10_
