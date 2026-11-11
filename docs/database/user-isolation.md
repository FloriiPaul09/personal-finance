# User isolation

## Scope

How the data of one user is kept invisible and unchangeable for every other user, at the level of EF Core and of the database: ownership marker, current user, named query filter, composite foreign keys and the `SaveChanges` interceptor. Authentication itself is in [architecture/authentication.md](../architecture/authentication.md).

## Current state

Implemented ([ADR-0001](../architecture/adr/0001-authentication.md), [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md)) in `backend/src/Infrastructure/Persistence`: `AppDbContext` (filter), `OwnershipInterceptor`, `ICurrentUser` / `FixedCurrentUser`, `UserIsolationException`, composite keys in `Configurations/`. The HTTP current user arrives with `[Backend]`; until then the API acts as nobody (`FixedCurrentUser.None`).

### Three independent layers

```
Request of user B
   │
   ▼
1. Query filter "UserScope"      reads never return rows of user A
   │
   ▼
2. SaveChanges interceptor       inserts always belong to B; user_id never changes
   │
   ▼
3. Composite foreign keys        the database rejects a row of B pointing to data of A
```

A leak needs all three layers to fail at the same time.

### Ownership marker and current user

- Every user-owned domain entity implements `IOwnedByUser` (in `Domain/Common`): a read-only `Guid UserId`, set by the constructor.
- `ICurrentUser` (in `Infrastructure`) exposes the id of the user the code is acting for, or none:

| Implementation | Where | User |
|---|---|---|
| HTTP current user | `Api` | The authenticated user of the request; none for anonymous requests |
| Fixed current user (`FixedCurrentUser`) | `Infrastructure` | Set explicitly: creation of default data for a new user, tests, the future `Worker` (one scope per user) |

`AppDbContext` receives `ICurrentUser` through dependency injection; the filter reads it at query time, not when the model is built.

### Query filter

- One **named filter, `UserScope`**, added in a loop to every entity type that implements `IOwnedByUser`:

  ```csharp
  // Infrastructure/Persistence/AppDbContext.cs — built with expression trees for each owned type
  builder.Entity(clrType).HasQueryFilter(QueryFilters.UserScope, entity => entity.UserId == this.CurrentUserId);
  ```

- With no current user, `CurrentUserId` is a value that matches nothing: anonymous code reads nothing.
- Filters with a name can be disabled one by one (`IgnoreQueryFilters([...])`), so a future filter (e.g. archived rows) never forces disabling `UserScope`.
- **Rule**: slices in `Api` never call `IgnoreQueryFilters`. Only `Infrastructure` code that acts for every user (e.g. deleting a user, migrations of data) may do it, and it is reviewed explicitly.
- Identity tables are not filtered: they are read only by Identity.

### Composite foreign keys

Every reference between owned tables includes `user_id`:

```sql
ALTER TABLE accounts     ADD CONSTRAINT ak_accounts_user_id_id UNIQUE (user_id, id);
ALTER TABLE transactions ADD CONSTRAINT fk_transactions_accounts_user_id_account_id
    FOREIGN KEY (user_id, account_id) REFERENCES accounts (user_id, id);
```

| Child | References |
|---|---|
| `transactions` | `accounts`, `categories`, `transfers` |
| `categories` | `categories` (parent) |
| `budgets` | `categories` |
| `transaction_tags` | `transactions`, `tags` |

- A row of user B pointing to an account of user A is rejected by PostgreSQL (`23503`), whatever the code does.
- Optional references (`category_id`, `transfer_id`, `parent_id`) are nullable: with PostgreSQL's default `MATCH SIMPLE`, a null column skips the check, as it should.
- In EF Core, each relationship uses `HasForeignKey(e => new { e.UserId, e.AccountId })` and `HasPrincipalKey(a => new { a.UserId, a.Id })`; `UserId` takes part in several foreign keys of the same table.

### Interceptor

A `SaveChangesInterceptor` in `Infrastructure/Persistence`, for entities implementing `IOwnedByUser`:

| State | Check or action |
|---|---|
| Added | `UserId` must equal the current user, otherwise the save fails; sets `CreatedAt` and `UpdatedAt` |
| Modified | `UserId` must not change, otherwise the save fails; sets `UpdatedAt` |
| Any, without a current user | The save fails |

Time comes from `TimeProvider`, so tests use a fixed clock.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| `UserId` on every owned entity | Multi-user without data migration | [ADR-0001](../architecture/adr/0001-authentication.md) |
| Named query filter `UserScope` on every owned entity | Isolation in reads without code in slices; disabling another filter keeps it on | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| Composite foreign keys `(user_id, id)` | The database itself refuses references between users | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| `UserId` in constructors, checked by the interceptor | Explicit invariant; a save for the wrong user is impossible | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| No Row Level Security for now | Per-request connection state, harder pooling and migrations | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |

## Tests

In `Infrastructure.Tests` ([testing.md](testing.md#isolation-tests)), with two users A and B for every owned entity:

- B's context reads none of A's rows (lists, `Find`, navigation properties, aggregates such as sums);
- B cannot update or delete A's rows (they are not found);
- B cannot insert a transaction on A's account or with A's category, a budget on A's category, a link to A's tag — even bypassing the filter, the database rejects it (`23503`);
- inserting an entity whose `UserId` differs from the current user fails; changing `UserId` fails; saving without a current user fails;
- a context without current user reads nothing;
- a test enumerates every entity type of the model and fails if an owned entity has no `UserScope` filter or no `(user_id, id)` key: new tables cannot forget isolation.

Endpoint-level isolation tests stay in `Api.Tests` ([testing-strategy.md](../architecture/testing-strategy.md#minimum-required)).

## Scalability

- **Multi-user**: already the model; registration only adds users.
- **Admin features** (user management, statistics): dedicated `Infrastructure` queries that disable `UserScope` explicitly, behind an admin policy, with their own tests.
- **Worker** (recurring transactions, import): one scope per user with a fixed current user, so the same filter and interceptor apply.
- **Defense in depth**: PostgreSQL Row Level Security on top, if the database is ever accessed by other tools; it needs a new ADR.

## To do / open questions

- `[Backend]` HTTP current user implementation and the expected status for cross-user access (`404` recommended, so existence is not revealed).
- An architecture test that forbids `IgnoreQueryFilters` in `Api` (with the library chosen in `[Backend]`).
- ✅ EF Core maps the optional composite keys (`category_id`, `parent_id`) sharing `user_id` with other keys: verified by the isolation tests (`Database_RejectsATransactionWithAnotherUsersCategory`, `Database_RejectsASubcategoryOfAnotherUsersCategory`); the fallback of ADR-0010 is not needed.

---
_Last updated: 2026-11-10_
