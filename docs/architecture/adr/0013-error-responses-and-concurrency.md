# ADR-0013: Error responses, optimistic concurrency over HTTP and access to other users' data

- **Status**: Accepted
- **Date**: 2026-11-12
- **Deciders**: Paul

## Context

- The domain raises `DomainException` with a stable code; its HTTP mapping was left to `[Backend]` ([ADR-0010](0010-data-model-and-user-isolation.md)).
- Every owned row has a version (`xmin`, concurrency token): a stale write raises `DbUpdateConcurrencyException` ([database/conventions.md](../../database/conventions.md#concurrency)).
- Names are unique per user through unique indexes; two devices can create the same name at the same time.
- The query filter `UserScope` hides other users' rows; `IgnoreQueryFilters` is banned in `Api` ([ADR-0012](0012-api-foundation.md)).
- The roadmap has a PWA used offline from the phone, and the computer edits the same data: lost updates must be refused.
- The Angular app needs one predictable error shape to show messages and map errors to form fields.

## Decision

1. **One global `IExceptionHandler`** (`ApiExceptionHandler`) with `AddProblemDetails`, `UseExceptionHandler` and `UseStatusCodePages`: every error, a `401` included, is a ProblemDetails (RFC 9457) with a stable **`code`** and a **`traceId`**. Responses without a body get the code of their status.
2. **`DomainException` codes by family**, in one table (`DomainErrorStatuses`): invalid values → `400`, conflicts with the current state → `409`, invariants the slices must guarantee → `500` (logged, no details). A test fails if a code of `DomainErrors` is missing.
3. **Duplicates through the database**: a unique violation (`23505`) is mapped from the index name to a code (`account.name_taken`, …) with `409`. A test compares the mapping with the unique indexes in PostgreSQL. No check-then-insert in slices.
4. **Concurrency with `ETag` / `If-Match`**: single resources and write responses carry `ETag: "<xmin>"` (strong tag); every `PUT`, `PATCH` and `DELETE` requires `If-Match` — missing → `428`, malformed → `400`, stale → `412`, also when another write lands between the check and `SaveChanges`. Lists expose `version` on each item. A test fails if an update or delete endpoint does not take the version.
5. **Other users' data is `404`**, identical to an id that does not exist. An id in the request body that the user does not own gives `400` with code `{resource}.not_found` on that field.

Details: [backend/validation-and-errors.md](../../backend/validation-and-errors.md), [backend/api-endpoints.md](../../backend/api-endpoints.md#versions), [backend/security.md](../../backend/security.md#another-users-data).

## Consequences

**Positive**
- Slices stay linear: they throw (domain, `EnsureVersion`, `GetReferenceAsync`) or return `TypedResults.NotFound()`, and the response shape is always the same.
- The frontend switches on `code`, never on messages; messages can stay in English and the UI translates codes.
- No code can be forgotten (domain codes, unique indexes, versions on updates): each list is checked by a test.
- Duplicates are refused even for simultaneous requests, without a race window.
- Lost updates between phone and computer are refused; the standard headers fit an offline queue and allow `304` responses later.
- `404` never reveals that an id exists, and needs no code: the query filter already returns nothing.

**Negative**
- The status of a domain code lives away from the code itself (one table, protected by its test).
- The frontend must read `ETag` and send `If-Match` (one Angular interceptor or a helper per resource service).
- Index names appear in the API project: renaming an index in a migration requires updating the table (the test fails until then).
- Requests that match no endpoint still answer `401` to anonymous callers (ADR-0012), now with a body.

## Alternatives considered

**Endpoint filter per module** — Rejected. Lives in the endpoint pipeline, but cannot see errors outside endpoints; a global handler is still needed for `500`.

**Result pattern instead of exceptions** — Rejected. Explicit errors in signatures, but against ADR-0010 and boilerplate in every slice.

**All domain errors `422`, or all `400`** — Rejected. Simpler, but the status no longer tells a wrong value from a state that forbids the change.

**Check for duplicates in each slice** — Rejected. Readable, but two simultaneous requests still create the duplicate, and the check is repeated in every create and rename.

**Version in the body and `409`** — Rejected. Simple for forms, but not the HTTP standard and no help for caching or the offline queue.

**`403` for other users' data** — Rejected. Reveals that the id exists, and telling "exists but not yours" apart requires reading without the filter, which is banned in `Api`.
