# State management

## Scope

Where the state of the application lives: data read from the API (server state), state of a page (filters, selection), state shared by several features, and how data is refreshed after a write. The HTTP details (versions, errors, paging) are in block 2.

## Current state

Decided in block 1 ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)); the first stores arrive with block 2 (session) and block 5 (accounts, categories). No state library is used.

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
@Service()
export class AccountsStore {
  private readonly api = inject(AccountsApi);           // hand-written HttpClient service
  readonly includeArchived = signal(false);

  // Reads: httpResource / resource, driven by signals
  readonly accounts = httpResource<ListResponse<AccountResponse>>(() => ({
    url: '/api/accounts',
    params: { includeArchived: this.includeArchived() },
  }));

  // Writes: call the API, then reload what changed
  async archive(account: AccountResponse): Promise<void> {
    await this.api.archive(account.id);
    this.accounts.reload();
  }
}
```

Rules:

- **Components never inject `HttpClient`** or an API service: they read signals of a store and call its methods.
- **Reads** use `httpResource()` (or `resource()` around an API service) so loading, error and value are signals; a template shows them with the shared empty and error states (block 4).
- **Writes** go through the API service, which sends `If-Match` with the version and turns errors into typed `code`s (block 2), then the store **reloads** the affected resources. No optimistic update in the MVP: the server computes balances and totals.
- **Amounts are never computed** in a store: totals, balances, budget progress come from the API (`signedAmount`, `balance`, `spent`, …).
- The store is the feature's **facade**: its inside can move to NgRx SignalStore without changing components.

### Refresh across features

Features cannot import each other, but a new expense changes the dashboard, the balances and the budgets. A `core` service will publish **data changes by resource** (e.g. a version signal per resource: `transactions`, `accounts`, …). Stores that depend on a resource read that signal in their `httpResource` request, so a write in one feature reloads the others when they are next shown. To be built with the first write (block 5).

### Paging

The movement list follows `nextCursor` (infinite scroll): the store keeps the loaded pages and the cursor, and starts again when the filters change (block 6).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Store services with signals and `httpResource`, no state library | Stable Angular APIs, no dependency; enough for a single-user app | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Components talk only to stores | One place for cache and refresh; a library can replace the inside later | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Reload after writes, no optimistic updates | The server is the source of truth for money | [ADR-0021](../architecture/adr/0021-frontend-foundation.md), [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md) |

## Tests

- Each store with logic (paging, reload after a write, refresh across features) gets Vitest tests with `provideHttpClientTesting`: the request sent (URL, headers, body) and the signals after the response.
- Nothing to test yet: block 1 adds no store.

## Scalability

- **More features**: one store per feature; shared data moves to `core` instead of being read across features.
- **Complex screens**: a store can move to NgRx SignalStore (entities, devtools) behind the same API.
- **PWA offline**: writes of expenses and transfers go through a `core` queue that stores the request with its UUIDv7 id and version and replays it; stores do not change (the API accepts the same request again, [ADR-0017](../architecture/adr/0017-transactions-and-tags.md)).
- **Multi-user**: the session store resets every store on logout (they are root singletons): to be handled with logout in block 3.

## To do / open questions

- Block 2: hand-written API services, typed errors, versions; first `httpResource` with the generated types.
- Block 3: session store, reset of the stores on logout.
- Block 5: data changes service in `core` for refresh across features.

---
_Last updated: 2026-11-13_
