# ADR-0022: Frontend integration with the API — antiforgery, errors, versions, paging and ids

- **Status**: Accepted
- **Date**: 2026-11-16
- **Deciders**: Paul

## Context

- The API expects from its client ([architecture/README.md](../README.md#frontend), handoffs from `[Backend]`): the antiforgery cookie requested at startup with Angular's default names, `If-Match` with the version on every update and delete, decisions based on the error `code`, lists as `{ items }` and cursor pages, UUIDv7 ids for creates that may be sent again ([ADR-0013](0013-error-responses-and-concurrency.md), [ADR-0014](0014-authentication-endpoints-and-sessions.md), [ADR-0015](0015-resource-conventions-and-accounts.md), [ADR-0017](0017-transactions-and-tags.md)).
- The roadmap adds a PWA that opens offline and replays queued expenses and transfers, CSV import and export, multi-user and other languages: the choices must hold for them without a rewrite.
- [ADR-0021](0021-frontend-foundation.md) chose types generated from OpenAPI and hand-written services, and stores built on signals and `httpResource`.
- The OpenAPI document of the API, read in this block, describes every number as `number | string` (ASP.NET Core's web defaults also read numbers written as text) and months (`YearMonth`) as an empty schema. Validation errors name fields after the C# properties (`Email`), not the JSON properties (`email`).

## Decision

1. **Antiforgery at startup, never blocking**: an app initializer calls `GET /api/auth/antiforgery` before the first page, waits at most 5 seconds and never fails, so the app still opens offline or with the API down. Angular's built-in XSRF support is configured explicitly with the API's names (`XSRF-TOKEN` cookie, `X-XSRF-TOKEN` header); it reads the cookie at every write, so the token renewed by login and logout needs no code. A write without token gets `auth.antiforgery_invalid`, whose message asks to reload.
2. **One error type**: an interceptor turns every failed `/api` request into an `ApiError` (`status`, `code`, `fieldErrors` with camelCase keys, `traceId`, `detail`); a request without answer becomes `network.unavailable`, an answer without ProblemDetails `response.unexpected`. Italian messages come from one map of codes in `core` (`errorMessage`, `messageOf`). Pages handle the codes they expect (e.g. a duplicate name on its field); everything else reaches a global `ErrorHandler` that shows a snackbar with the message and offers to copy the `traceId`. `auth.required` stays silent there: block 3 redirects to the login.
3. **Versions explicit**: update and delete methods take the `version` from the body of any response, list items included, and send it with `ifMatch(version)` as a strong tag (`If-Match: "123"`). A `412` (`concurrency.version_mismatch`) makes the store reload and the message says the item changed meanwhile.
4. **Cursor paging in one utility**: `cursorPager(fetchPage)` in `core` exposes items, loading, error, loaded and has-more as signals, with `loadMore()` and `reset()`; `reset()` cancels the running request, so a page of old filters never lands in a new list.
5. **Client ids with the `uuid` package** (`v7()`), wrapped by `newId()`: time-ordered also within one millisecond. `crypto.randomUUID()` gives version 4 ids, which the API refuses.
6. **API types from a committed snapshot**: `pnpm api:types` downloads the OpenAPI document from the API in Development, saves it as `core/api/openapi.json`, adjusts it (numbers as `number` / `integer` only, `YearMonth` as a `yyyy-MM` string) and generates `core/api/schema.ts`; `pnpm api:types --file <path>` regenerates from a saved document without the API. The app imports the aliases of `core/api/models.ts` (`AccountResponse`, `ListResponse<T>`, `PageResponse<T>`, `Version`, …). Services are written per feature on `HttpClient`.

## Consequences

**Positive**
- The PWA opens offline: startup never waits on the network for more than a few seconds and never fails because of it.
- Every error has one shape: forms, `httpResource` errors, stores and the global handler read the same `code`; another language only translates the message map.
- The offline queue of the PWA can store the request with its client id and version and replay it unchanged; the CSV import reuses `newId()` and the codes for a per-row report.
- Paging logic (cursor, filters changing mid-request) is written and tested once; the CSV export walks the same cursor.
- The types match the running API, and the snapshot shows in a pull request what changed in the contract.

**Negative**
- A first page may wait up to 5 seconds for the antiforgery request when the API hangs.
- The message map grows with every new code; an unknown code shows a generic message until it is added (a test lists the documented codes).
- The document adjustments are a workaround until `[Backend]` describes numbers and months precisely.
- One more dependency (`uuid`).

## Alternatives considered

**Antiforgery on demand (before the first write) or retry after `auth.antiforgery_invalid`** — Rejected: more code and races between simultaneous writes; repeating a `POST` without a client id could create duplicates.

**Result objects instead of exceptions, or only a global snackbar** — Rejected: results do not fit `httpResource`, which already exposes `error()`; a snackbar alone cannot show which form field is wrong.

**`ETag` cache in an interceptor, or the version passed through `HttpContext`** — Rejected: an item edited from a list was never read with an `ETag`, and a stale cache gives confusing `412`s; `HttpContext` adds indirection for the same result.

**Paging inside the transactions store** — Rejected: the same logic would be rewritten for the export and tested through one feature.

**Hand-written UUIDv7** — Rejected: format, version, variant and ordering would be ours to get right and test.

**Fixing the document by hand or keeping `number | string`** — Rejected: hand edits are lost at the next generation; `number | string` would force a conversion wherever an amount is read.
