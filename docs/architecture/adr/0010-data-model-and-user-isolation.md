# ADR-0010: Data model, keys and per-user isolation

- **Status**: Accepted
- **Date**: 2026-11-10
- **Deciders**: Paul

## Context

- The MVP needs accounts, categories, transactions, transfers between accounts, monthly budgets and tags.
- Errors on balances, transfers and budgets, or data visible to the wrong user, are the most serious failures of the app ([ADR-0006](0006-testing-strategy.md)).
- Every user-owned entity has a `UserId` and every query is filtered by the current user ([ADR-0001](0001-authentication.md)). Multi-user is on the roadmap.
- Business rules live in `Domain`, persistence in `Infrastructure`, slices use the `DbContext` directly ([ADR-0002](0002-backend-structure.md)).
- The repository is public and self-hosted with `docker compose up` ([ADR-0008](0008-local-development-environment.md)).

## Decision

### Keys and naming

1. **`Guid` keys everywhere**, Identity included (`IdentityUser<Guid>`). Values are UUIDv7, generated client-side by the Npgsql provider: known before saving and ordered in time.
2. **snake_case names** through `EFCore.NamingConventions`: plural tables (`accounts`, `transactions`), snake_case columns; Identity tables renamed (`users`, `roles`, `user_roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims`). One schema (`public`).

### Isolation between users

3. **Named query filter `UserScope`** (EF Core 10) on every user-owned entity, applied automatically to every entity that implements the owned-by-user marker. Without a current user the filter matches nothing.
4. **Composite foreign keys `(user_id, id)`**: every reference between user-owned tables includes `user_id`, so the database rejects a row that points to another user's data.
5. **`UserId` is a constructor argument of every domain entity**; a `SaveChanges` interceptor checks that it matches the current user on insert and that it never changes afterwards. The same interceptor sets `CreatedAt` and `UpdatedAt` from `TimeProvider`.

### Money and movements

6. **Signed amounts** in the domain and in the database: income positive, expense negative, transfer legs negative (source) and positive (destination). `CHECK` constraints tie the sign to the kind. The API may expose absolute values (decided in `[Backend]`).
7. **Transfers**: a `transfers` row plus **two rows in `transactions`** linked by `transfer_id`, one per account; the database allows at most one outgoing and one incoming leg per transfer, the domain always creates both. MVP: only between accounts with the same currency.
8. **Balances are computed**: initial balance plus the sum of the account's movements. No stored balance.
9. **Budgets with `effective_from`**: a category's monthly limit applies from a month until a later entry changes it (or ends it); changing it adds a row and keeps history. Budgets apply to expense categories and include their subcategories.

### Lifecycle, tags, errors

10. **Archive, don't delete**, accounts and categories that have history (`is_archived`); transactions, transfers, budgets and tags are deleted physically. Foreign keys between domain tables never cascade, except a transfer to its own legs, tags to their links, and a user to all their data.
11. **Tags** in a `tags` table with a `transaction_tags` link table.
12. **Domain errors are exceptions** (`DomainException` with a stable code), mapped to ProblemDetails by the global handler; request validation stays in the API ([`[Backend]`](../backend-structure.md)).

### Initial data and migrations

13. **Default categories are created per user** when the user is created, from a list in code (Italian names); no global categories, no `HasData`.
14. **Migrations are applied at API startup** (`MigrateAsync`, which takes a database lock against concurrent starts). `dotnet-ef` is pinned as a local tool. Migrations are append-only once merged on `main`.

### Smaller conventions

| Topic | Decision |
|---|---|
| Rounding | Amounts with more than 2 decimals are rejected, never rounded silently; zero amounts are rejected |
| Enumerations | `text` columns with a `CHECK` constraint (no native PostgreSQL enums, no integers) |
| Case-insensitive unique names | A `normalized_name` column computed by the domain, like Identity does |
| Concurrent edits | Optimistic concurrency on PostgreSQL `xmin` for editable entities |
| Currency | ISO 4217 `char(3)` on the account; transactions inherit it |
| Negative balances | Allowed (cards, overdraft) |
| Subcategories | One level; same kind as the parent; a transaction may use a parent or a child |
| Text search | `ILIKE` without index in the MVP; `pg_trgm` when data grows |
| Tags in commits | Part of the `transactions` module |

Details: [database/README.md](../../database/README.md).

## Consequences

**Positive**
- Cross-user leaks need two independent failures: a missing query filter **and** a database that accepts a foreign reference, which composite keys forbid.
- Balances are always consistent with the movements; sums are plain `SUM(amount)` with no sign logic in queries.
- A transfer cannot lose one of its sides silently; changing or deleting it touches both legs together.
- Budgets are set once and keep their history.
- History is never lost by deleting an account or a category.
- Identifiers are known before saving, unguessable in URLs and portable between instances (useful for import/export).

**Negative**
- More keys and indexes: every owned table has an extra unique key `(user_id, id)`, and EF configuration of overlapping composite foreign keys is more delicate (covered by integration tests).
- `UserId` must be passed explicitly when creating entities.
- A community package (`EFCore.NamingConventions`) is part of the persistence setup.
- Balances are recomputed on every read; snapshots are needed only at volumes far above personal use.
- Signed amounts must be converted to absolute values in exactly one place for display.
- The API needs DDL rights on its database to apply migrations at startup.

## Alternatives considered

**Keys**: Identity `string` keys with `Guid` domain keys (two id types, a 36-character text `user_id` in every row and index); `bigint` domain keys (enumerable ids, known only after insert).

**Naming**: EF defaults (quoted PascalCase identifiers in every hand-written query); a custom snake_case convention (code to maintain and test).

**Isolation**: query filters and checks in slices only (a bug can link data of two users and the database accepts it); PostgreSQL Row Level Security on top of composite keys (per-request connection state, harder pooling and migrations) — kept as a future option.

**`UserId` set by the interceptor**: shorter slices, but entities are complete only after saving and domain tests do not see the owner.

**Unsigned amounts with a kind**: natural for input, but every sum needs a `CASE`; a wrong sign in one query is exactly the error to avoid.

**Transfers**: two linked rows without a `transfers` table (the pair exists only by convention); one row with source and destination accounts (every balance and list query reads two account columns, no room for different currencies).

**Stored balances**: fast reads, but a duplicated value that can diverge and needs concurrency control. Monthly snapshots on top of computed balances remain the scaling path.

**Budgets**: one row per category and month (recreated every month); per-month overrides on top of `effective_from` (postponed).

**Deletion**: soft delete everywhere (partial unique indexes, phantom rows if a filter is missing); cascading deletes (deleting an account would delete years of movements).

**Tags**: a `text[]` column with a GIN index (renaming a tag rewrites every row, no integrity).

**Errors**: a `Result` type returned by the domain (more code in every slice).

**Default categories**: `HasData` (the owner does not exist when migrations run); global categories with a null owner (an exception to isolation, no composite keys).

**Migrations**: a migration bundle run as a separate step (kept as the path for public instances with restricted rights); manual `dotnet ef database update` (self-hosting no longer one command).
