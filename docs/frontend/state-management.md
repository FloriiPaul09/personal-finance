# State management

## Scope

Where the state of the application lives: data read from the API (server state), state of a page (filters, selection), state shared by several features, and how data is refreshed after a write. The HTTP details (versions, errors, paging) are in block 2.

## Current state

Decided in block 1 ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)); the HTTP pieces the stores use (errors, versions, paging, ids) are in `core` since block 2 ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)). The first store is the session (block 3, [ADR-0023](../architecture/adr/0023-frontend-authentication.md)); block 5 ([ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md)) added the reference data of accounts and categories in `core/data`, the refresh across features (`DataChanges`) and the first feature stores; block 6 ([ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)) the stores of movements and transfers, the movement list with its filters in the URL and the recent choices of the quick entry; block 7 ([ADR-0027](../architecture/adr/0027-frontend-budgets.md)) the budgets store (entries and progress of a month). No state library is used.

### Kinds of state

| Kind | Where | Example |
|---|---|---|
| Server state of a feature | Store service of the feature (`features/<f>/data/<f>-store.ts`) | Accounts list, budget progress of a month |
| Reference data used by several features | Directory in `core/data` | `AccountsDirectory`, `CategoriesDirectory`: pages, selectors, later movements, budgets, dashboard |
| Session | `SessionStore` in `core/auth` | Current user from `GET /api/auth/me` |
| Page and form state | Signals in the component, Signal Forms | Open filter panel, form fields, "Descrizione e tag" shown |
| URL state | Query string, read as inputs | `?month=2026-11`, movement list filters |
| Device state | Browser storage, per user | Recent categories and last account of the quick entry (`RecentChoices`) |

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
  private readonly directory = inject(AccountsDirectory);   // reads: the shared list in core
  private readonly changes = inject(DataChanges);

  readonly all = this.directory.all;                         // signals for the pages
  readonly error = this.directory.error;

  // Writes: call the API, then have the list read again; the page catches the codes it expects
  async update(account: AccountResponse, body: AccountRequest): Promise<AccountResponse> {
    try {
      return await firstValueFrom(this.api.update(account, body));
    } finally {
      this.changes.notify('accounts');                       // also after a 412: show the current data
    }
  }
}
```

Rules:

- **Components never inject `HttpClient`** or an API service: they read signals of a store and call its methods.
- **Reads** use `httpResource()` (or `resource()` around an API service) so loading, error and value are signals; a template shows them with `<app-empty-state>`, `<app-error-state>` and Material's progress bar ([ui-components.md](ui-components.md#shared-components)).
- **Writes** go through the API service, which sends `If-Match` with `ifMatch(version)`; the store then calls `DataChanges.notify()` for what it wrote, and every reader of it **reads again** ([Refresh across features](#refresh-across-features)). No optimistic update in the MVP: the server computes balances and totals.
- **Errors** are always `ApiError` (see below): the page shows the codes it expects next to the right field and lets the rest go to the global handler.
- **Creates that may be sent again** (expenses, income, transfers) carry `id: newId()` (UUIDv7) from the moment the form opens: sending the same request twice returns the same resource.
- **Amounts are never computed** in a store: totals, balances, budget progress come from the API (`signedAmount`, `balance`, `spent`, …).
- The store is the feature's **facade**: its inside can move to NgRx SignalStore without changing components.

### Session

`SessionStore` (`core/auth/session-store.ts`, block 3):

| Member | What it does |
|---|---|
| `status` | `unknown` (not asked yet), `authenticated`, `anonymous` (`me` answered `401`), `unavailable` (no answer: offline or API down) |
| `user` | `CurrentUser` from the API, or `null` |
| `ensureLoaded()` | Asks `GET /api/auth/me` once and remembers the answer; concurrent calls share the request; after `unavailable` the next call asks again. Called by the guards, not at startup |
| `login(request)` | `POST /api/auth/login`, then `authenticated` with the user of the answer |
| `markSignedOut()` | Forgets the user (used by the interceptor on `401 auth.required`) |
| `changePassword(request)` | `POST /api/auth/change-password` |
| `logout()` | `POST /api/auth/logout` (a `401` means already signed out), then a full page load of `/login` through `LOAD_PAGE` (replaced in tests) |

- The session cookie is `HttpOnly`: the store never sees it, it only knows whether there is a session.
- **Logout reloads the app** instead of resetting each store: every store is a root singleton, and a reload is the only reset that cannot forget one (multi-user: no data of one user is ever shown to the next).
- Forms are page state (Signal Forms in the component); a page turns the `ApiError` of a submit into form errors with `apiFormErrors()` from `core/forms` ([ui-components.md](ui-components.md#forms)).

### Reference data

Block 5, `core/data`. Accounts and categories are small lists (the API does not page them) that many features show: they are read **once for the whole app**, archived items included, and every page and selector reads the same signals.

| Directory | Reads | Read again after writes to | Members |
|---|---|---|---|
| `AccountsDirectory` | `GET /api/accounts?includeArchived=true` | `accounts`, `transactions`, `transfers` (balance, `hasMovements`) | `all`, `active`, `byId(id)`, `error`, `loading`, `reload()` |
| `CategoriesDirectory` | `GET /api/categories?includeArchived=true` | `categories`, `transactions`, `budgets` (`isInUse`) | `all`, `byId(id)`, `groups(kind, { includeArchived })`, `label(category)` ("Trasporti › Carburante"), `error`, `loading`, `reload()` |

- Both use `referenceList(url, dependsOn)`: an `httpResource` whose request reads `DataChanges.version(...dependsOn)`, so each write sends a new request and cancels a running one. The last list stays in `all()` while the new one loads (`linkedSignal`): `loading()` is true only before the first list, so pages do not flash a progress bar after each archive.
- Archived items are part of the list: the pages show them on request, and a selector shows an archived item when it is the current value (an old movement on an archived account).
- Feature stores do not read the API for these lists: `AccountsStore` and `CategoriesStore` expose the directory's signals and add the writes.

### Refresh across features

Features cannot import each other, but a new expense changes the balances, the categories in use, the budgets and the dashboard. `DataChanges` (`core/data`, block 5) counts the **writes by resource** (`accounts`, `categories`, `transactions`, `transfers`, `budgets`):

```ts
// writer: a store, after every write (failed ones too: a 412 means newer data)
this.changes.notify('accounts');

// reader: inside the request of an httpResource, the resources its data comes from
httpResource(() => {
  this.changes.version('accounts', 'transactions', 'transfers');
  return { url: '/api/accounts', params: { includeArchived: true } };
});
```

- The **writer** names only what it wrote; the **reader** lists what its data is computed from. A new reader (the dashboard) adds its own list without touching the writers.
- Root stores (the directories) read again right away; page stores are created with their page, so they read fresh data anyway and, while shown, follow the changes.

| Writer (block 6) | Notifies | Readers that follow |
|---|---|---|
| `TransactionsStore` (create, update, delete, and the delete of "Annulla") | `transactions` | Accounts (balances, `hasMovements`), categories (`isInUse`), the movement list, the tag suggestions |
| `TransfersStore` | `transfers` | Accounts, the movement list (legs) |
| `BudgetsStore` (block 7: set from a month, stop, "Annulla", delete an entry) | `budgets` | Budget entries and progress, categories (`isInUse`) |

The progress of the budgets also follows `transactions` and `categories`: an expense, or a category moved under another, changes what is spent.

### Movements

Block 6, `features/transactions/data` and `features/transfers/data`:

- **The list** (`transactions-page`) is page state: its period and filters come from the URL as inputs, `movementView(query)` turns them into the filters of the API, and a `cursorPager` loads 50 movements at a time. An effect resets the pager when the view changes or when `TransactionsStore.listVersion()` (`version('transactions', 'transfers')`) changes, so a movement saved, undone or deleted elsewhere shows at once. Movements are grouped by day in the order of the API; no totals are computed.
- **The forms** read the item with `resource()` around the API service (`GET /api/transactions/:id`, `/api/transfers/:id`), not from a list: a movement may be far from the pages loaded. The form model is a `linkedSignal` of a start value compared by id and version, so a `412` followed by a new read shows the current data, and "Salva e aggiungi un'altra" starts again from a new start value (new id, same account and date).
- **Ids** are chosen when the form opens (`newId()`), and kept until the save succeeds: a save that times out and is sent again creates one movement.
- **Undo** after a create is a delete of the movement just saved, through the store, so it notifies like any delete.
- **Tag suggestions**: `TransactionsStore.tagNames` reads `/api/tags` (read again after `transactions` writes), the most used first.

### Budgets

Block 7, `features/budgets/data` ([ADR-0027](../architecture/adr/0027-frontend-budgets.md)):

- **Entries**: the whole list of entries (`GET /api/budgets`), read with `referenceList` (now exported by `core`, with its query parameters) and again after each `budgets` write. `budgetedIds` (categories with at least one entry, stopped ones included, as the API counts them) gives the reasons of the category selector; `entriesOf(id)` the history of a category.
- **Progress of a month**: `progressOf(month)`, called by the page in its injection context, an `httpResource` on `/api/budgets/progress?month=…` (always sent: the API would use its own UTC month). After a write the same month is read again and its rows stay on screen; another month shows the progress bar until it arrives.
- **Setting a budget from a month**: `setFrom(categoryId, month, amount)` first reads the category's entries from the API, then changes that month's entry (`PUT` with its current version) or adds one (`POST`); `amount` `null` stops the budget. It returns the entry and an `undo()` for "Annulla": delete the entry it added, or put back the amount of the entry it changed.
- No client id and no offline queue for budgets: a duplicate month is refused by the API (`budget.month_taken`).

### Recent choices

`RecentChoices` (`core/data`, block 6) keeps the last categories used per kind (6 each) and the last account, for the quick entry. They live in the browser's `localStorage` under `pf:<userId>:recent`: per user, so two people on one device do not mix, and per device. A broken or missing storage (private mode) only means starting from nothing. Ids that no longer exist or are archived are skipped by the pages, not removed from the storage. `RECENT_STORAGE` is the token tests replace.

### Errors

Every failed `/api` request fails with an `ApiError` ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)):

| Property | Content |
|---|---|
| `status` | HTTP status; `0` when there was no answer |
| `code` | Stable code of the API ([backend/validation-and-errors.md](../backend/validation-and-errors.md#codes)) or of the app: `network.unavailable` (no answer: offline), `server.unavailable` (`502` / `503` / `504` from the proxy: the API is down), `response.unexpected` (any other answer without ProblemDetails) |
| `fieldErrors`, `fields` | Messages by field, keys in camelCase like the request (`Email` from the API → `email`) |
| `traceId` | Id of the request in the API logs |

- `errorMessage(code)` / `messageOf(error)`: the Italian message (one `$localize` map in `core/api/error-messages.ts`; unknown codes get a generic message).
- A page handles what it expects, e.g. `account.name_taken` on the name field, `concurrency.version_mismatch` with a reload.
- Anything not handled reaches `AppErrorHandler`, which logs it and shows it with `ErrorNotifier` (snackbar with the message and "Copia riferimento" for the `traceId`), through `Notifier`. `auth.required` is neither shown nor logged: `sessionInterceptor` has already opened the login page ([routing.md](routing.md#authentication)).
- Snackbars: every message goes through `Notifier` (`core/feedback`, block 5): `show(message, { action, onAction, duration })` and `undoable(message, undo)` ("Annulla", 8 s). It loads Material's snackbar with the first message (not at startup, [app-structure.md](app-structure.md#boundaries)) and gives it the `pf-snackbar` class; on phones `styles.scss` lifts it above the navigation bar and the new expense button.
- A failed `httpResource` exposes the `ApiError` in `error()`; the page shows it with `<app-error-state [error]="…" (retry)="resource.reload()">` from `core` ([ui-components.md](ui-components.md#shared-components)).

### Versions

`ifMatch(version)` builds `If-Match: "<version>"`. The version is the `version` of the last body read, list items included, so an item can be changed from a list. A `412` means it changed elsewhere: reload, then the user decides again. The PWA queue will store the version with each pending change.

Forms (block 5): the form model is a `linkedSignal` of the item read from the directory, compared by id and version. After a `412` the store has the list read again; the item comes back with a new version, the form shows the current data and a message ("modificato altrove … salva di nuovo"). A list read again with nothing changed resets nothing.

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
| Session asked on the first protected page; logout with a full reload | Startup never waits for the network; no data of a user left in memory | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |
| Accounts and categories read once in `core`, archived included; writes counted by resource in `DataChanges`, readers list their sources | One request per list for the whole app; features refresh each other without imports | [ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md) |
| After a `412` the form shows the current data | The person decides again on what is really there | [ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md) |
| Movement list as page state from the URL with `cursorPager`; recent choices in the browser storage per user; undo as a delete | A view that survives reloads; a convenience that needs no API; nothing new on the server | [ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md) |
| Budget entries read whole; progress per month in the page; entries read again before each save; "Annulla" as the inverse write | Few entries; the version sent is always the current one; nothing new on the server | [ADR-0027](../architecture/adr/0027-frontend-budgets.md) |

## Tests

- Each store with logic (reload after a write, refresh across features) gets Vitest tests with `provideHttpClientTesting`: the request sent (URL, headers, body) and the signals after the response.
- Block 7: `budgets-store.spec.ts` (entries read once and again after budget writes only, budgeted categories, progress of the month sent and read again after writes to budgets, movements and categories, rows kept for the same month and not for another, error and retry, change or add the month's entry with the current version, "Annulla" of each, refresh after a refused write).
- Block 6: `transactions-store.spec.ts` and `transfers-store.spec.ts` (requests, filters without empty values, `If-Match`, notify after failed writes too, tag names by usage), `recent-choices.spec.ts` (per user, most recent first, at most 6, broken or missing storage), `movement-query.spec.ts`, the list page (reset after a write elsewhere, more pages on scroll).
- Block 5: `data-changes.spec.ts`, `accounts-directory.spec.ts` (one read with archived items, read again after writes to its sources and not others, list kept while reading, running read cancelled, error and reload), `categories-directory.spec.ts` (groups by kind and parent, archived on request, labels), `accounts-store.spec.ts` and `categories-store.spec.ts` (requests, `If-Match`, refresh after failed writes too, subcategories restored one by one). Test data in `core/testing` (`anAccount`, `aCategory`, `someCategories`, `provideApiHttpTesting`, `problem`).
- Block 3: `session-store.spec.ts` (one shared `me` request, `401` → anonymous, no answer → unavailable then asked again, login, password change, logout with reload also when the session already expired), `session-interceptor.spec.ts`, `auth-guards.spec.ts`, `api-form-errors.spec.ts`.
- Block 2: `cursor-pager.spec.ts` (pages in order with the cursor unchanged, `loadMore` ignored while loading and at the end, `reset` never shows a late page of old filters, retry after an error, cancel on destroy); `versions.spec.ts`; `api-error.spec.ts` and `error-messages.spec.ts` (every documented code has an Italian message); `app-error-handler.spec.ts`, `error-notifier.spec.ts` ([testing.md](testing.md)).

## Scalability

- **More features**: one store per feature; shared data moves to `core` instead of being read across features.
- **Complex screens**: a store can move to NgRx SignalStore (entities, devtools) behind the same API.
- **PWA offline**: writes of expenses and transfers go through a `core` queue that stores the request with its UUIDv7 id and version and replays it; stores do not change (the API accepts the same request again, [ADR-0017](../architecture/adr/0017-transactions-and-tags.md)).
- **Multi-user**: logout reloads the app, so every store starts empty for the next user; roles, when they come, are read from `SessionStore.user`.
- **Many items**: the directories read whole lists, fine for tens of accounts and categories; with thousands they would move to paged reads and a search in the selectors.
- **Changes from other devices** (multi-user, PWA): `DataChanges` can also be notified by a push from the server or on focus, with the same readers.
- **PWA offline**: `SessionStore` can keep the last known user and answer from it when `me` gets no answer, instead of `unavailable`.

## To do / open questions

- ✅ Block 2: typed errors, versions, paging, client ids ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)).
- ✅ Block 3: session store, logout with reload ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)).
- ✅ Block 5: reference data and `DataChanges` for refresh across features ([ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md)).
- ✅ Block 6: movement and transfer stores notify `transactions` / `transfers`, so balances and categories in use follow ([ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)).
- ✅ Block 7: the budget store notifies `budgets`; the categories in use follow ([ADR-0027](../architecture/adr/0027-frontend-budgets.md)).
- Block 8: the dashboard reads `version('accounts', 'transactions', 'transfers', 'budgets', 'categories')`.
- Recent choices kept by the API, if people use more devices.

---
_Last updated: 2026-11-17_
