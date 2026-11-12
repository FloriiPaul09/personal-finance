# Database conventions

## Scope

How the schema is named and typed, how keys, constraints and indexes are defined, how timestamps and concurrency work, and how EF Core is configured in `Infrastructure` to produce that schema. Isolation between users is in [user-isolation.md](user-isolation.md).

## Current state

Implemented ([ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md)): `AppDbContext` and one configuration per entity in `backend/src/Infrastructure/Persistence`, first migration `InitialCreate`.

| What | Where |
|---|---|
| Provider and naming (`UseNpgsql` + `UseSnakeCaseNamingConvention`), used by API, tools and tests | `PersistenceServiceCollectionExtensions.UsePersonalFinanceDatabase` |
| Key, `(user_id, id)` key, foreign key to `users`, `xmin` token shared by every owned entity | `Configurations/OwnedEntityMapping.cs` |
| Enums as snake_case text and their `CHECK` | `Configurations/SnakeCaseEnumConverter.cs` |
| One file per entity | `Configurations/*Configuration.cs` |

Entities with a constructor whose parameters all match properties (`Account`, `Tag`) are materialized through it; the others have a private parameterless constructor for EF Core only.

### Naming

| Object | Convention | Example |
|---|---|---|
| Tables | snake_case, plural | `accounts`, `transaction_tags` |
| Columns | snake_case | `initial_balance`, `effective_from` |
| Primary keys | `pk_<table>` | `pk_accounts` |
| Foreign keys | `fk_<table>_<principal>_<columns>` (generated) | `fk_transactions_accounts_user_id_account_id` |
| Indexes | `ix_<table>_<columns>` (generated) | `ix_transactions_user_id_date` |
| Unique keys | `ak_<table>_<columns>` or `ix_…` with `IsUnique` | `ak_accounts_user_id_id` |
| Check constraints | `ck_<table>_<rule>` (named by hand) | `ck_transactions_amount_sign` |
| Identity tables | renamed | `users`, `roles`, `user_roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims` |

- Applied by `UseSnakeCaseNamingConvention()` (`EFCore.NamingConventions`) on the `DbContext` options; Identity tables are renamed in `OnModelCreating`.
- One schema: `public`.
- C# names stay PascalCase; only the database is snake_case.

### Types

| Data | C# | PostgreSQL |
|---|---|---|
| Identifiers | `Guid` (UUIDv7, generated client-side by Npgsql) | `uuid` |
| Amounts | `decimal` | `numeric(18,2)` |
| Transaction date, budget month | `DateOnly` | `date` |
| Technical timestamps | `DateTimeOffset` (UTC) | `timestamptz` |
| Currency | `string` (3 upper-case letters) | `char(3)` with `CHECK (currency ~ '^[A-Z]{3}$')` |
| Enumerations | C# `enum` stored as text | `text` with `CHECK (… IN (…))` |
| Names | `string` | `varchar(100)` (`varchar(50)` for tags) |
| Descriptions | `string?` | `varchar(200)` |
| Flags | `bool` | `boolean` |

- No `float`, `double` or `real` anywhere.
- Enum values are stored in snake_case (`credit_card`): a value converter maps them; adding a value means updating the `CHECK` in a migration.
- `DateOnly` maps natively to `date`: no time and no time zone conversion.

### Keys

- Every table has a `uuid` primary key named `id`, except link tables (`transaction_tags`: composite primary key).
- Every user-owned table also has a unique key `(user_id, id)`, the target of composite foreign keys ([user-isolation.md](user-isolation.md#composite-foreign-keys)).

### Constraints

| Table | Constraint |
|---|---|
| `accounts` | currency format; `type` in its list; unique `(user_id, normalized_name)` |
| `categories` | `kind` in its list; unique `(user_id, kind, parent_id, normalized_name)` with `NULLS NOT DISTINCT` (two top-level categories of the same kind with the same name collide) |
| `transactions` | `amount <> 0`; `kind` in its list; sign by kind (`income` > 0, `expense` < 0); `(kind = 'transfer') = (transfer_id IS NOT NULL)`; `(kind = 'transfer') = (category_id IS NULL)` |
| `transactions` | at most one negative leg per transfer (unique index on `transfer_id` where `amount < 0`) and one positive (where `amount > 0`) |
| `budgets` | `amount IS NULL OR amount > 0`; `effective_from` is the first day of a month (`EXTRACT(DAY FROM effective_from) = 1`); unique `(user_id, category_id, effective_from)` |
| `tags` | unique `(user_id, normalized_name)` |

Rules that need more than one row (both legs present, one level of categories, kinds matching) are enforced by the domain and covered by tests.

### Delete behavior

| Foreign key | On delete | Why |
|---|---|---|
| Any owned table → `users` | `CASCADE` | Deleting a user removes all their data |
| `transactions` → `transfers` | `CASCADE` | A transfer is deleted with its legs |
| `transaction_tags` → `transactions`, `tags` | `CASCADE` | Links have no meaning alone |
| `transactions` → `accounts`, `categories`; `budgets` → `categories`; `categories` → parent | `NO ACTION` | History is archived, never deleted by accident |

`NO ACTION` instead of `RESTRICT`: it is checked at the end of the statement, so deleting a user (which cascades to accounts and transactions in the same statement) works, while deleting an account that still has transactions fails.

### Normalized names

Names that must be unique without regard to case are stored twice: `name` as typed, `normalized_name` computed by the domain (trimmed, upper-case invariant culture), like Identity's `NormalizedEmail`. The unique index is on `normalized_name`.

### Timestamps

- `created_at` and `updated_at` on every owned table except the link table `transaction_tags`, set by the `SaveChanges` interceptor from `TimeProvider` ([user-isolation.md](user-isolation.md#interceptor)). Never set by slices, never by database defaults.
- Tests use a fixed clock.

### Concurrency

- Editable entities (`accounts`, `categories`, `transactions`, `transfers`, `budgets`, `tags`) map PostgreSQL's system column `xmin` as concurrency token: a shadow property `Version` (`uint`, column `xmin`, type `xid`), so the domain does not see it and no column is added.
- A conflicting update raises `DbUpdateConcurrencyException`. The API exposes the version as `ETag`, requires it in `If-Match` and answers `412` on a conflict ([ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md), [backend/api-endpoints.md](../backend/api-endpoints.md#versions)); `RowVersion` (in `Infrastructure/Persistence`) reads it.

### Indexes

| Index | Serves |
|---|---|
| `transactions (user_id, date DESC, id)` | Transaction list by period, newest first |
| `transactions (user_id, account_id, date)` | Balances, list filtered by account; also backs the composite foreign key |
| `transactions (user_id, category_id, date)` | Spending per category, budget progress |
| `transactions (user_id, transfer_id)` | Loading a transfer's legs |
| `transaction_tags (user_id, tag_id)` | Filter by tag |
| `budgets (user_id, category_id, effective_from)` | Limit of a month (unique) |
| `categories (user_id, parent_id)` | Subcategories |

Text search on descriptions uses `ILIKE` without a dedicated index in the MVP.

### EF Core configuration

- One `IEntityTypeConfiguration<T>` per entity in `Infrastructure/Persistence/Configurations`, loaded with `ApplyConfigurationsFromAssembly`.
- Domain entities use private setters and constructors; EF maps them through fields and properties, without changing the domain for persistence.
- `AppDbContext` derives from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| snake_case through `EFCore.NamingConventions` | SQL without quotes; standard PostgreSQL style | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| `uuid` keys, UUIDv7 generated client-side | Known before saving, index-friendly, unguessable | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| `text` + `CHECK` for enumerations | Readable, easy to extend, no `ALTER TYPE` | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| `normalized_name` for case-insensitive uniqueness | No extension, rule owned and tested by the domain | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| `xmin` as concurrency token | Phone and computer editing the same row; no extra column | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| `NO ACTION` between domain tables | Archive instead of delete, while user deletion still cascades | This document |

## Tests

In `Infrastructure.Tests` ([testing.md](testing.md#persistence-tests)):

- each `CHECK` constraint rejects a violating row (PostgreSQL error `23514`);
- each unique key rejects a duplicate (`23505`), including case-only differences on names;
- deleting an account or a category with transactions fails (`23503`); deleting a transfer deletes its legs; deleting a user deletes all their data;
- a stale `xmin` raises `DbUpdateConcurrencyException`;
- the model has no pending changes compared with the last migration.

## Scalability

- **More data**: partial or covering indexes guided by real query plans (`EXPLAIN ANALYZE` on realistic volumes); `pg_trgm` index for text search.
- **Multi-user**: no change; every index starts with `user_id`.
- **New modules**: same conventions; owned tables always get `user_id`, the `(user_id, id)` key and composite foreign keys.
- **Per-module schemas** (modular monolith): possible later with a new ADR; naming stays the same.

## To do / open questions

- ✅ `[Backend]` HTTP status for a concurrency conflict: `412` with `ETag` / `If-Match` ([ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)).
- Enable `pg_trgm` and `unaccent` when text search needs it.

---
_Last updated: 2026-11-12_
