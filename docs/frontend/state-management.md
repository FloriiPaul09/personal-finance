# State management

## Scope

Where the state of the application lives: data read from the API (server state), state of a page (filters, selection), state shared by several features, and how data is refreshed after a write. The HTTP details (versions, errors, paging) are in block 2.

## Current state

Decided in block 1 ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)); the HTTP pieces the stores use (errors, versions, paging, ids) are in `core` since block 2 ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)). The first stores arrive with block 3 (session) and block 5 (accounts, categories). No state library is used.

### Kinds of state

| Kind | Where | Example |
|---|---|---|
| Server state of a feature | Store service of the feature (`features/<f>/data/<f>-store.ts`) | Accounts list, budget progress of a month |
| Reference data used by several features | Store in `core` | Active accounts and categories for selectors (transactions, budgets, dashboard) |
| Session | Store in `core` (block 3) | Current user from `GET /api/auth/me` |
| Page and form state | Signals in the component, Signal Forms | Open dialog, filter values, form fields |
| URL state | Query string, read as inputs | `?month=2026-11`, list filters |

### The store pattern

A store is a `@Service()` per feature (or per page when it holds page data) that is the **only** way the feature's components reach the API:

```ts
// features/accounts/data/accounts-api.ts — the only place that knows the URLs
@Service()
export class AccountsApi {
  private readonly http = inject(HttpClient);

  update(account: AccountResponse, body: AccountRequest): Observable<AccountResponse> {
    return this.http.put<AccountResponse>(`/api/accounts/${account.id}`, body, {
      headers: ifMatch(account.version),             // If-Match: "81723"
    });
  }
}

// features/accounts/data/accounts-store.ts
@Service()
export class AccountsStore {
  private readonly api = inject(AccountsApi);
  readonly includeArchived = signal(false);

  // Reads: httpResource driven by signals; its error() is an ApiError
  readonly accounts = httpResource<ListResponse<AccountResponse>>(() => ({
    url: '/api/accounts',
    params: { includeArchived: this.includeArchived() },
  }));

  // Writes: call the API, then reload what changed; the page catches the codes it expects
  async rename(account: AccountResponse, body: AccountRequest): Promise<void> {
    try {
      await firstValueFrom(this.api.update(account, body));
    } finally {
      this.accounts.reload();                        // also after a 412: show the current data
    }
  }
}
```

Rules:

- **Components never inject `HttpClient`** or an API service: they read signals of a store and call its methods.
- **Reads** use `httpResource()` (or `resource()` around an API service) so loading, error and value are signals; a template shows them with the shared empty and error states (block 4).
- **Writes** go through the API service, which sends `If-Match` with `ifMatch(version)`; the store then **reloads** the affected resources. No optimistic update in the MVP: the server computes balances and totals.
- **Errors** are always `ApiError` (see below): the page shows the codes it expects next to the right field and lets the rest go to the global handler.
- **Creates that may be sent again** (expenses, income, transfers) carry `id: newId()` (UUIDv7) from the moment the form opens: sending the same request twice returns the same resource.
- **Amounts are never computed** in a store: totals, balances, budget progress come from the API (`signedAmount`, `balance`, `spent`, …).
- The store is the feature's **facade**: its inside can move to NgRx SignalStore without changing components.

### Refresh across features

Features cannot import each other, but a new expense changes the dashboard, the balances and the budgets. A `core` service will publish **data changes by resource** (e.g. a version signal per resource: `transactions`, `accounts`, …). Stores that depend on a resource read that signal in their `httpResource` request, so a write in one feature reloads the others when they are next shown. To be built with the first write (block 5).

### Errors

Every failed `/api` request fails with an `ApiError` ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)):

| Property | Content |
|---|---|
| `status` | HTTP status; `0` when there was no answer |
| `code` | Stable code of the API ([backend/validation-and-errors.md](../backend/validation-and-errors.md#codes)) or of the app: `network.unavailable` (no answer), `response.unexpected` (no ProblemDetails) |
| `fieldErrors`, `fields` | Messages by field, keys in camelCase like the request (`Email` from the API → `email`) |
| `traceId` | Id of the request in the API logs |

- `errorMessage(code)` / `messageOf(error)`: the Italian message (one `$localize` map in `core/api/error-messages.ts`; unknown codes get a generic message).
- A page handles what it expects, e.g. `account.name_taken` on the name field, `concurrency.version_mismatch` with a reload.
- Anything not handled reaches `AppErrorHandler`, which logs it and shows it with `ErrorNotifier` (snackbar with the message and "Copia riferimento" for the `traceId`). `auth.required` is left to the `401` handling of block 3.
- A failed `httpResource` exposes the `ApiError` in `error()`; the page shows it with the shared error state (block 4).

### Versions

`ifMatch(version)` builds `If-Match: "<version>"`. The version is the `version` of the last body read, list items included, so an item can be changed from a list. A `412` means it changed elsewhere: reload, then the user decides again. The PWA queue will store the version with each pending change.

### Paging

`cursorPager(fetchPage, destroyRef?)` in `core`:

```ts
readonly page = cursorPager((cursor) => this.api.list(this.filters(), cursor), inject(DestroyRef));
// template: @for (t of page.items(); …) · page.loading() · page.hasMore() → page.loadMore()
// filters changed → page.reset()
```

It passes `nextCursor` back unchanged, ignores `loadMore()` while loading or after the last page, retries the same cursor after an error, and `reset()` cancels the running request so a page of old filters never lands in the new list. The movement list (block 6) uses it for infinite scroll.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Store services with signals and `httpResource`, no state library | Stable Angular APIs, no dependency; enough for a single-user app | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Components talk only to stores | One place for cache and refresh; a library can replace the inside later | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Reload after writes, no optimistic updates | The server is the source of truth for money | [ADR-0021](../architecture/adr/0021-frontend-foundation.md), [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md) |

## Tests

- Each store with logic (reload after a write, refresh across features) gets Vitest tests with `provideHttpClientTesting`: the request sent (URL, headers, body) and the signals after the response.
- Block 2: `cursor-pager.spec.ts` (pages in order with the cursor unchanged, `loadMore` ignored while loading and at the end, `reset` never shows a late page of old filters, retry after an error, cancel on destroy); `versions.spec.ts`; `api-error.spec.ts` and `error-messages.spec.ts` (every documented code has an Italian message); `app-error-handler.spec.ts`, `error-notifier.spec.ts` ([testing.md](testing.md)).

## Scalability

- **More features**: one store per feature; shared data moves to `core` instead of being read across features.
- **Complex screens**: a store can move to NgRx SignalStore (entities, devtools) behind the same API.
- **PWA offline**: writes of expenses and transfers go through a `core` queue that stores the request with its UUIDv7 id and version and replays it; stores do not change (the API accepts the same request again, [ADR-0017](../architecture/adr/0017-transactions-and-tags.md)).
- **Multi-user**: the session store resets every store on logout (they are root singletons): to be handled with logout in block 3.

## To do / open questions

- ✅ Block 2: typed errors, versions, paging, client ids ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)).
- Block 3: session store, reset of the stores on logout; first hand-written API service (auth).
- Block 5: data changes service in `core` for refresh across features.

---
_Last updated: 2026-11-16_
