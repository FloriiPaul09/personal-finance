# ADR-0015: Resource endpoint conventions and the accounts module

- **Status**: Accepted
- **Date**: 2026-11-12
- **Deciders**: Paul

## Context

- The API foundation, errors, versions and authentication are in place ([ADR-0012](0012-api-foundation.md), [ADR-0013](0013-error-responses-and-concurrency.md), [ADR-0014](0014-authentication-endpoints-and-sessions.md)). Accounts are the first real resource, so choices made here become conventions for categories, transactions, transfers, budgets and the dashboard.
- Open conventions: enum serialization, amounts in JSON, list shape, paging ([api-endpoints.md](../../backend/api-endpoints.md)).
- Account rules in `Domain` ([database/domain-model.md](../../database/domain-model.md#account-balance)):
  - the balance is computed, never stored;
  - the currency is fixed once the account has movements;
  - an archived account keeps history and balance but receives no new movements.
- The roadmap has a PWA used offline from the phone, a generated Angular client from OpenAPI (if `[Frontend]` chooses one), the `Worker` with monthly snapshots, CSV import and multi-user.

## Decision

**Conventions for every resource**

1. **Enumerations as `camelCase` strings** (`"creditCard"`), with one global `JsonStringEnumConverter`; numbers are refused.
2. **Amounts as JSON numbers** with at most 2 decimals (`decimal` on the server, checked by `Domain`). Totals and balances are always computed by the server.
3. **Every list is an envelope** `{ "items": [...] }` (`ListResponse<T>`), even when it is not paged. Paging, totals or a cursor are added next to `items` later.
4. **One response type per resource** (`AccountResponse`), returned by list, read and every write, with `version` in the body; writes re-read it from the database after saving.
5. **OpenAPI schema names are prefixed with the declaring slice** (`LoginRequest`, `CreateAccountRequest`), so nested contracts with the same name never collide.
6. **Unreadable requests are `400` `request.invalid` in every environment**: invalid JSON, an unknown enumeration value, a malformed query or route value.

**Accounts**

7. **Balance computed in SQL at every read**: initial balance plus the sum of movements, as correlated subqueries in the same query as the accounts. The response also has `hasMovements`.
8. **`PUT` replaces the whole account** (name, type, currency, initial balance) with a mandatory `If-Match`.
9. **Archive and unarchive are dedicated actions**: `POST /api/accounts/{id}/archive` and `/unarchive`. They return the account with its new `ETag` and are idempotent, so they do not require `If-Match`.
10. **`DELETE` only for accounts without movements**, with `If-Match`; with movements `409` `account.has_movements`. The rule lives in `Domain` (`Account.EnsureCanDelete`), and the app offers to archive instead.
11. **The list hides archived accounts by default**; `?includeArchived=true` adds them after the active ones. No paging: a person has a handful of accounts.

Details: [backend/api-endpoints.md](../../backend/api-endpoints.md#accounts), [backend/validation-and-errors.md](../../backend/validation-and-errors.md).

## Consequences

**Positive**
- The balance is always consistent with the movements: there is no stored value that can drift. The index `(user_id, account_id, date)` serves the subqueries, and an API test compares the result with the `Domain` calculation.
- The OpenAPI document lists the allowed values of every enumeration; adding or reordering enum members never breaks clients.
- Every list can gain paging without breaking clients; transactions will use the same envelope.
- A generated client gets one readable type per contract.
- History is never lost: an account with movements can only be archived. An account created by mistake can still be deleted.
- Archiving from one device never fails because another device renamed the account.

**Negative**
- The client must not add amounts in floating point (a rule for `[Frontend]`). Exact cents are guaranteed up to about 90 trillion.
- A write costs one more query, the re-read for the response.
- The balance query reads every movement of the account. With years of data it needs the monthly snapshots of the `Worker` (see Scalability in [api-endpoints.md](../../backend/api-endpoints.md#scalability)).
- An enum member renamed in `Domain` changes the API contract: enum names are part of the contract from now on.
- A transaction saved between the movement check and the delete fails on the foreign key and gives `500`. This is rare: it needs a delete and a new movement on the same account at the same instant.

## Alternatives considered

**Balance only in a dedicated endpoint or the dashboard** — Rejected. Lighter list, but the app shows the balance next to the account almost everywhere, so it would need two calls.

**Balance column updated on every write** — Rejected. Fast reads, but against [ADR-0010](0010-data-model-and-user-isolation.md); a missed update leaves a wrong balance.

**`PATCH` with JSON Merge Patch** — Rejected. Smaller requests, but "absent" and `null` must be told apart and validation becomes per field. The account form always sends every field.

**`isArchived` in `PUT`** — Rejected. No extra route, but archiving would need the whole account and a version.

**`DELETE` that archives when there are movements; no delete at all** — Rejected. The first hides what happened from the client; the second keeps accounts created by mistake forever.

**Archived accounts always in the list** — Rejected. Every screen would have to remember to filter them.

**Enumerations as numbers** — Rejected. Unreadable, and reordering the enum breaks clients.

**Amounts as strings** — Rejected. Exact at any size, but every client must convert them and forms get harder; `numeric(18,2)` amounts are exact as JSON numbers in the ranges of a personal budget.

**Plain arrays for small lists** — Rejected. Smaller JSON, but different shapes between resources, and adding paging later breaks clients.
