# ADR-0017: Transactions, tags, paging and offline-safe creation

- **Status**: Accepted
- **Date**: 2026-11-12
- **Deciders**: Paul

## Context

- Transactions are the resource used every day: quick entry from the phone, list with filters, edits. The conventions of [ADR-0015](0015-resource-conventions-and-accounts.md) apply.
- Amounts are stored signed ([ADR-0010](0010-data-model-and-user-isolation.md)). The domain receives positive amounts and applies the sign itself. How the API exposes them was left open for this block.
- The roadmap has a PWA that queues expenses offline. A request whose response is lost is sent again, and must not save the expense twice. The same queue may hold an edit of an expense not yet synchronized.
- Other roadmap items: recurring transactions created by the `Worker`, CSV import, export, charts per category, attachments.
- Transfer legs are rows of `transactions` (kind `transfer`) and change only through their transfer (block 7).
- Indexes:
  - `(user_id, date DESC, id)` serves the list by period;
  - `(user_id, account_id, date)` and `(user_id, category_id, date)` serve filters and balances;
  - text search uses `ILIKE` without an index in the MVP ([database/conventions.md](../../database/conventions.md)).

## Decision

1. **Kind + positive amount in requests.** Responses carry `amount` (positive, what the form sends back) and `signedAmount` (the effect on the balance). Only the domain chooses a sign.
2. **Optional client-chosen id** on create: a UUID version 7, otherwise `400` on `id`. A `POST` with an id that already exists for the user returns that transaction with `200` and creates nothing. Two identical requests at the same instant also end with one transaction: the loser of the primary-key race reads the winner. An id used by another user gives `409` `resource.conflict` on `id`, and nothing about that user's data is revealed. The domain accepts an optional id in `OwnedEntity`, so transfers, the `Worker` and the CSV import can do the same.
3. **Keyset paging** on `(date, id)`, newest first. The response is `{ "items": [...], "nextCursor": "…" }` (`PageResponse<T>`); `nextCursor` is opaque and `null` on the last page. The limit defaults to 50, at most 200. There is no total count; period totals come from the dashboard.
4. **Filters with one value each**:
   - `from` / `to` on the date;
   - `accountId`;
   - `categoryId` (a top-level category includes its subcategories);
   - `kind`;
   - `tag` (name, any case);
   - `q` (text in the description, `ILIKE` with `%` and `_` matched literally, at most 100 characters).

   A filter on another user's account or category simply finds nothing.
5. **Tags by name in the transaction** (`"tags": ["vacanza-2026"]`, at most 20): existing tags are reused without regard to case, and missing ones are created. `PUT` replaces the whole list. `/api/tags` lists tags with their usage count, renames them and deletes them (removing them from their transactions). Tags stay part of the transactions module in commits and docs.
6. **Transfer legs in the list**, in read-only form (`kind: transfer`, `transferId`). `PUT` and `DELETE` on them give `409` `transaction.is_transfer_leg`.
7. **A tag-only change still changes the version**: the update always rewrites the transaction row, so a stale client gets `412` even when it only changed tags.

Details: [backend/api-endpoints.md](../../backend/api-endpoints.md#transactions).

## Consequences

**Positive**
- A client bug cannot turn an expense into an income; the CSV import maps a column of positive amounts and a kind.
- The offline queue is safe to replay without extra tables, and it knows each id before the server answers. It can attach a photo or queue an edit to an expense still waiting to be sent.
- Paging stays fast at any depth and never skips or repeats an item while new expenses arrive.
- Quick entry with tags needs one request; typos are fixed by renaming the tag.
- The list of an account explains its whole balance, transfers included.

**Negative**
- Two amount fields in responses.
- No page numbers and no total count in the list.
- The PWA must generate UUIDv7 ids: `crypto.randomUUID()` gives version 4, which is refused, so a small function or library is needed.
- A typo in a tag name creates a new tag (fixed with rename; merging two tags is not supported yet).
- Two requests creating the same new tag at the same instant: one gets `409` `tag.name_taken` and is sent again.
- The list orders within a day by id descending, while the index orders by id ascending inside the date: PostgreSQL sorts within each date (incremental sort), cheap for the few transactions of a day.

## Alternatives considered

**Signed amounts everywhere** — Rejected. Same shape as the database, but the client chooses the sign: one bug and an expense becomes an income.

**Kind + positive amount without `signedAmount`** — Rejected. One field, but transfer legs need another flag to tell in from out, and running balances need sign logic in the client.

**`Idempotency-Key` header with stored responses** — Rejected. Generic for every `POST`, but a migration, cleanup of old keys, more code, and the queue does not know the id until the server answers.

**No protection** — Rejected. A dropped connection during the request saves the expense twice.

**Offset paging with total count** — Rejected. Page numbers, but slow deep pages, skipped or repeated rows while inserting, and a `COUNT` on every request.

**Multi-value filters now** — Rejected for now. More flexible, but more cases to test for a need not yet there; repeated parameters can be added without breaking clients.

**Tags by id only** — Rejected. No implicit creation, but one more step for every expense with a new tag.

**Transfer legs excluded from the list** — Rejected. Simpler list, but an account's list would not explain its balance.
