# API endpoints

## Scope

Conventions shared by every endpoint and the contract of each endpoint: route, authentication, request, responses. One section per resource. How endpoints are written is in [README.md](README.md#endpoints); errors in [validation-and-errors.md](validation-and-errors.md); authentication in [security.md](security.md).

## Current state

The foundation is implemented: health, development tools, authentication, the mechanism that maps every module ([ADR-0012](../architecture/adr/0012-api-foundation.md)), error responses and versions ([ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)). The conventions shared by resources and the first resource, accounts, are in [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md); categories in [ADR-0016](../architecture/adr/0016-categories-module.md); transactions and tags in [ADR-0017](../architecture/adr/0017-transactions-and-tags.md); transfers in [ADR-0018](../architecture/adr/0018-transfers-module.md); budgets in [ADR-0019](../architecture/adr/0019-budgets-module.md). Resource endpoints arrive one block at a time; their contract is written here in the same pull request as the code.

### Conventions

| Topic | Convention |
|---|---|
| Base path | `/api`; each module adds its prefix: `/api/accounts`, `/api/categories`, … The Angular dev server and nginx proxy `/api` ([devops/local-development.md](../devops/local-development.md)) |
| Format | JSON, `camelCase` properties (ASP.NET Core default); **enumerations as `camelCase` strings** (`"creditCard"`) — numbers and unknown values give `400` `request.invalid` ([ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md)). Options in `JsonConventions` |
| Authentication | Session cookie on every endpoint unless marked **public** ([security.md](security.md)); without a session `401` |
| Identifiers | `Guid` (UUIDv7) in routes: `/api/accounts/{id:guid}`. Creates that must be safe to send again (offline queue) accept an `id` chosen by the client, a UUIDv7 ([ADR-0017](../architecture/adr/0017-transactions-and-tags.md)) |
| Amounts | **JSON numbers** with at most 2 decimals (`1234.56`), `decimal` on the server; a third decimal is refused (`amount.too_many_decimals`), never rounded. Totals and balances are computed by the server: clients never add amounts in floating point. Movements: kind + positive `amount` in requests; responses add `signedAmount` ([ADR-0017](../architecture/adr/0017-transactions-and-tags.md)) |
| Dates | Transaction dates `YYYY-MM-DD` (`DateOnly`); months `YYYY-MM` (`YearMonth`, budgets); timestamps ISO 8601 in UTC |
| Methods | `GET` read, `POST` create (`201` + `Location` + `ETag`), `PUT` replace the whole resource (`200` + `ETag`), `DELETE` (`204`), `POST /{id}/{action}` for idempotent state changes such as `archive` (`200` + `ETag`). `PUT`, `PATCH` and `DELETE` require `If-Match` ([Versions](#versions)) |
| Errors | ProblemDetails (RFC 9457) with a stable `code` and a `traceId`; validation errors `400` with `errors` by field ([validation-and-errors.md](validation-and-errors.md)) |
| Not found | `404` `resource.not_found` for a route id that is not the user's — another user's id included ([security.md](security.md#another-users-data)) |
| Responses | One response type per resource (e.g. `AccountResponse`), the same for list items, reads and writes, with `version` in the body |
| Lists | **Envelope** `{ "items": [...] }` (`ListResponse<T>`) for every list, paged or not; filters in the query string (`?includeArchived=true`, `?kind=expense`) — enumerations in the query follow the JSON names (`QueryValues.ParseEnum`). Paged lists use a cursor: `{ "items": [...], "nextCursor": "…" }` (`PageResponse<T>`), `?limit=` and `?cursor=` ([ADR-0017](../architecture/adr/0017-transactions-and-tags.md)) |
| OpenAPI | Every endpoint has a name (`WithName`) and its module tag; responses come from `TypedResults`; schema names are prefixed with their slice (`CreateAccountRequest`) |

### Endpoint template

Each endpoint is documented with this table, then request and response examples:

| | |
|---|---|
| **Route** | `METHOD /api/...` |
| **Auth** | Session / Public |
| **Request** | Body or query parameters, with validation rules |
| **Responses** | Status codes, codes and bodies |
| **Versions** | `ETag` returned / `If-Match` required |
| **Isolation** | What another user gets (`404`, or `400` `{resource}.not_found` for ids in the body) |
| **Tests** | Test class |

### Versions

Every resource has a version: PostgreSQL's `xmin` of its row ([database/conventions.md](../database/conventions.md#concurrency), [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)). It prevents lost updates when the phone and the computer change the same item.

| Where | How |
|---|---|
| Single resource (`GET /api/x/{id}`) and write responses (`POST`, `PUT`, `PATCH`) | Header `ETag: "12345"` — a strong tag, the version in quotes |
| Lists | Each item has `version` (number) in the body, so an item can be edited from a list without reading it again |
| `PUT`, `PATCH`, `DELETE` | Header `If-Match: "12345"` with the last version read — **mandatory** |

| Situation | Response |
|---|---|
| `If-Match` missing | `428` `concurrency.version_required` |
| `If-Match` malformed: `*`, weak tag `W/"…"`, several tags, no quotes | `400` `concurrency.version_invalid` |
| Version different from the current one, or the row changed between the check and the save | `412` `concurrency.version_mismatch` — nothing is changed |
| Resource not the user's | `404` before any version check |

In a slice:

```csharp
internal static async Task<Results<Ok<Response>, NotFound>> Handle(
    Guid id, Request request, IfMatch ifMatch, AppDbContext db, HttpResponse response, CancellationToken ct)
{
    var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct);
    if (account is null) return TypedResults.NotFound();

    db.EnsureVersion(account, ifMatch);          // 412 if the client read an older version
    account.Rename(request.Name);
    await db.SaveChangesAsync(ct);               // 412 if another write landed in between

    var version = RowVersion.Of(db, account);
    response.SetVersion(version);                // ETag of the new version
    return TypedResults.Ok(new Response(account.Id, account.Name, version));
}
```

- Taking an `IfMatch` parameter is what makes the header mandatory; `ApiHostTests.EveryUpdateAndDelete_RequiresIfMatch` fails if an update or delete endpoint does not take it.
- Read projections get the version with `EF.Property<uint>(entity, RowVersion.PropertyName)`.
- `If-None-Match` / `304` for reads is not implemented yet (see Scalability).

### Health

| | |
|---|---|
| **Route** | `GET /api/health` |
| **Auth** | Public |
| **Request** | None |
| **Responses** | `200` `Healthy` (text) when the API runs; `503` `Unhealthy` when a registered health check fails (none yet) |
| **Isolation** | No user data |
| **Tests** | `ApiHostTests.Health_IsPublic` |

Used by container health checks later ([devops/docker.md](../devops/docker.md)).

### OpenAPI

Development only ([ADR-0012](../architecture/adr/0012-api-foundation.md)): in any other environment these routes do not exist.

| Route | Content |
|---|---|
| `GET /openapi/v1.json` | OpenAPI document generated by ASP.NET Core |
| `GET /scalar/` | Scalar UI on that document (`/scalar` redirects here) |

Both are public on a development machine. Requests sent from Scalar carry the cookies of the API origin: call `GET /api/auth/antiforgery`, add the header `X-XSRF-TOKEN` with the value of the `XSRF-TOKEN` cookie, log in with `POST /api/auth/login`, then call the protected endpoints. The document is also the input for a generated Angular client, if `[Frontend]` chooses one.

### Authentication

Module `auth` ([ADR-0014](../architecture/adr/0014-authentication-endpoints-and-sessions.md), [security.md](security.md)). Every unsafe request — these included — needs the antiforgery header `X-XSRF-TOKEN` ([security.md](security.md#csrf)); a missing or wrong token gives `400` `auth.antiforgery_invalid`.

#### Get the antiforgery token

| | |
|---|---|
| **Route** | `GET /api/auth/antiforgery` |
| **Auth** | Public |
| **Request** | None |
| **Responses** | `204`, cookie `XSRF-TOKEN` (readable) + `pf_antiforgery` |
| **Tests** | `AuthTests.AntiforgeryToken_IsAReadableStrictCookie` |

Called by the app at startup, before its first unsafe request.

#### Login

| | |
|---|---|
| **Route** | `POST /api/auth/login` |
| **Auth** | Public, rate limited (10 / minute / address) |
| **Request** | `{ "email": "me@example.com", "password": "…", "rememberMe": false }` — `email` required, valid, ≤ 256; `password` required, ≤ 128 |
| **Responses** | `200` `{ "id": "…", "email": "…" }`, cookies `pf_session` and a new `XSRF-TOKEN` · `400` `validation.failed` · `401` `auth.invalid_credentials` (wrong email, wrong password or locked — same answer) · `429` `request.too_many` |
| **Tests** | `AuthTests` |

`rememberMe: false` gives a cookie that ends with the browser; `true` keeps it 14 days (sliding).

#### Logout

| | |
|---|---|
| **Route** | `POST /api/auth/logout` |
| **Auth** | Session |
| **Request** | None |
| **Responses** | `204`, session cookie removed, new anonymous `XSRF-TOKEN` · `401` `auth.required` |
| **Tests** | `AuthTests.Logout_EndsTheSession` |

#### Current user

| | |
|---|---|
| **Route** | `GET /api/auth/me` |
| **Auth** | Session |
| **Responses** | `200` `{ "id": "…", "email": "…" }` · `401` `auth.required` |
| **Isolation** | Always the user of the session |
| **Tests** | `AuthTests.Me_*` |

The app calls it at startup to decide whether to show the login.

#### Change password

| | |
|---|---|
| **Route** | `POST /api/auth/change-password` |
| **Auth** | Session |
| **Request** | `{ "currentPassword": "…", "newPassword": "…" }` — both required, ≤ 128 |
| **Responses** | `204`, session renewed · `400` `auth.password_incorrect` on `currentPassword` · `400` `auth.password_rejected` on `newPassword` (e.g. shorter than 12) · `401` `auth.required` |
| **Tests** | `AuthTests.ChangePassword_*` |

Other sessions of the user end within 5 minutes (security stamp).

#### Register

| | |
|---|---|
| **Route** | `POST /api/auth/register` — **exists only with `Auth__AllowRegistration=true`** |
| **Auth** | Public, rate limited |
| **Request** | `{ "email": "…", "password": "…" }` — `email` required, valid, ≤ 256; `password` required, ≤ 128 |
| **Responses** | `201` `{ "id": "…", "email": "…" }` with the default categories, session started, new `XSRF-TOKEN` · `400` `auth.password_rejected` on `password` · `400` `validation.failed` · `409` `auth.email_taken` on `email` · `429` `request.too_many` |
| **Tests** | `RegistrationTests`, `AuthTests.Register_DoesNotExist_WhileRegistrationIsOff` |

### Accounts

Module `accounts` ([ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md)). Every account endpoint returns the same `AccountResponse`:

```json
{
  "id": "0193a5c4-7e1f-7c3b-9a52-3f0e6d2b8a41",
  "name": "Conto corrente",
  "type": "checking",
  "currency": "EUR",
  "initialBalance": 100.00,
  "balance": 1339.85,
  "isArchived": false,
  "hasMovements": true,
  "version": 81723
}
```

| Field | Content |
|---|---|
| `type` | `cash`, `checking`, `creditCard`, `savings`, `other` |
| `currency` | ISO 4217, upper case |
| `initialBalance` | Any sign: a card or an overdraft can start below zero |
| `balance` | `initialBalance` + every movement of the account (income, expenses, transfer legs), computed by the database at each read, never stored |
| `hasMovements` | `true` when a transaction uses the account: the currency is fixed and the account can only be archived, not deleted |
| `version` | Value to send in `If-Match` |

Request body of create and update (all fields required):

```json
{ "name": "Conto corrente", "type": "checking", "currency": "EUR", "initialBalance": 100.00 }
```

| Field | Rules |
|---|---|
| `name` | 1–100 characters, trimmed; unique per user without regard to case, archived accounts included → `409` `account.name_taken` |
| `type` | One of the values above; anything else `400` `request.invalid` |
| `currency` | 3 letters, any case (`eur` → `EUR`); not letters → `400` `currency.invalid`. Cannot change once the account has movements → `409` `account.currency_locked` |
| `initialBalance` | At most 2 decimals → otherwise `400` `amount.too_many_decimals` |

#### List accounts

| | |
|---|---|
| **Route** | `GET /api/accounts` |
| **Auth** | Session |
| **Request** | Query `includeArchived` (`true` / `false`, default `false`) |
| **Responses** | `200` `{ "items": [AccountResponse, …] }`: active accounts by name; with `includeArchived=true` the archived ones follow, by name · `400` `request.invalid` (e.g. `includeArchived=maybe`) · `401` `auth.required` |
| **Versions** | `version` on each item |
| **Isolation** | Only the user's accounts |
| **Tests** | `AccountsTests.List_*` |

Not paged: a person has a handful of accounts.

#### Get an account

| | |
|---|---|
| **Route** | `GET /api/accounts/{id}` |
| **Auth** | Session |
| **Responses** | `200` `AccountResponse`, archived or not · `401` `auth.required` · `404` `resource.not_found` |
| **Versions** | `ETag` |
| **Isolation** | Another user's account: `404`, like a nonexistent id |
| **Tests** | `AccountsTests.Get_*` |

#### Create an account

| | |
|---|---|
| **Route** | `POST /api/accounts` |
| **Auth** | Session |
| **Request** | Body above |
| **Responses** | `201` `AccountResponse`, `Location: /api/accounts/{id}` · `400` `validation.failed` (missing field, name too long, currency not 3 characters), `request.invalid` (unreadable JSON, unknown type), `currency.invalid`, `amount.too_many_decimals`, `amount.out_of_range` · `401` `auth.required` · `409` `account.name_taken` |
| **Versions** | `ETag` |
| **Isolation** | Created for the user of the session; another user's account with the same name is no conflict |
| **Tests** | `AccountsTests.Create_*` |

#### Update an account

| | |
|---|---|
| **Route** | `PUT /api/accounts/{id}` |
| **Auth** | Session |
| **Request** | Body above, header `If-Match` |
| **Responses** | `200` `AccountResponse` · `400` as create · `401` `auth.required` · `404` `resource.not_found` · `409` `account.name_taken`, `account.currency_locked` · `412` `concurrency.version_mismatch` · `428` `concurrency.version_required` |
| **Versions** | `If-Match` required; new `ETag` |
| **Isolation** | Another user's account: `404` before any version check |
| **Tests** | `AccountsTests.Update_*` |

Archived accounts can be edited: archiving only stops new movements. Changing the initial balance changes the balance by the same amount.

#### Archive and unarchive

| | |
|---|---|
| **Route** | `POST /api/accounts/{id}/archive` · `POST /api/accounts/{id}/unarchive` |
| **Auth** | Session |
| **Request** | No body, no `If-Match` |
| **Responses** | `200` `AccountResponse` · `401` `auth.required` · `404` `resource.not_found` |
| **Versions** | New `ETag` (unchanged when the account was already in that state) |
| **Isolation** | Another user's account: `404`, nothing changes |
| **Tests** | `AccountsTests.Archive_*`, `Unarchive_*`, `ArchiveActions_*` |

Idempotent: archiving an archived account returns it unchanged. No version is required because the action changes one flag and cannot overwrite an edit made elsewhere; a form opened before archiving gets `412` on save and reloads. An archived account keeps its history and balance and is hidden from the default list; new movements on it are refused with `409` `account.archived` (blocks 6–7).

#### Delete an account

| | |
|---|---|
| **Route** | `DELETE /api/accounts/{id}` |
| **Auth** | Session |
| **Request** | Header `If-Match` |
| **Responses** | `204` · `401` `auth.required` · `404` `resource.not_found` · `409` `account.has_movements` · `412` `concurrency.version_mismatch` · `428` `concurrency.version_required` |
| **Versions** | `If-Match` required |
| **Isolation** | Another user's account: `404`, nothing deleted |
| **Tests** | `AccountsTests.Delete_*` |

Only for an account without movements, e.g. one created by mistake. With movements the app offers to archive it (`hasMovements` tells it in advance).

### Categories

Module `categories` ([ADR-0016](../architecture/adr/0016-categories-module.md)). Every category endpoint returns the same `CategoryResponse`:

```json
{
  "id": "0193a5c4-8a02-7d11-b1e0-5c7d0e9f2a10",
  "name": "Carburante",
  "kind": "expense",
  "parentId": "0193a5c4-89f7-7a3e-8c41-2b6e1d0c9f33",
  "isArchived": false,
  "isInUse": true,
  "hasSubcategories": false,
  "version": 90211
}
```

| Field | Content |
|---|---|
| `kind` | `expense` or `income`; a subcategory has the kind of its parent |
| `parentId` | The parent of a subcategory (one level only); `null` for a top-level category |
| `isInUse` | `true` when transactions or budgets use the category: its kind is fixed and it can only be archived |
| `hasSubcategories` | `true` when other categories, archived included, have it as parent: its kind is fixed, it cannot become a subcategory or be deleted |
| `version` | Value to send in `If-Match` |

Request body of create and update:

```json
{ "name": "Carburante", "kind": "expense", "parentId": "0193a5c4-89f7-7a3e-8c41-2b6e1d0c9f33" }
```

| Field | Rules |
|---|---|
| `name` | Required, 1–100 characters, trimmed; unique per user, kind and parent without regard to case, archived categories included → `409` `category.name_taken` |
| `kind` | Required, `expense` or `income`; anything else `400` `request.invalid`. Must be the parent's kind → otherwise `400` `category.kind_mismatch`. Can change only while `isInUse` and `hasSubcategories` are `false` → otherwise `409` `category.kind_locked` |
| `parentId` | Optional. One of the user's top-level categories → otherwise `400` `category.not_found` on `parentId` (another user's or nonexistent), `400` `category.too_deep` (a subcategory), `400` `category.parent_itself`; a newly chosen archived parent `409` `category.archived` |

#### List categories

| | |
|---|---|
| **Route** | `GET /api/categories` |
| **Auth** | Session |
| **Request** | Query `kind` (`expense` / `income`, optional), `includeArchived` (`true` / `false`, default `false`) |
| **Responses** | `200` `{ "items": [CategoryResponse, …] }` · `400` `request.invalid` (unknown `kind`, on `kind`; malformed `includeArchived`) · `401` `auth.required` |
| **Versions** | `version` on each item |
| **Isolation** | Only the user's categories |
| **Tests** | `CategoriesTests.List_*` |

Order: expense categories, then income; top-level categories by name, each followed by its subcategories by name. With `includeArchived=true` archived categories appear in their place. Not paged. The app builds a grouped menu from consecutive items with the same `parentId`.

#### Get a category

| | |
|---|---|
| **Route** | `GET /api/categories/{id}` |
| **Auth** | Session |
| **Responses** | `200` `CategoryResponse`, archived or not · `401` `auth.required` · `404` `resource.not_found` |
| **Versions** | `ETag` |
| **Isolation** | Another user's category: `404`, like a nonexistent id |
| **Tests** | `CategoriesTests.Get_*` |

#### Create a category

| | |
|---|---|
| **Route** | `POST /api/categories` |
| **Auth** | Session |
| **Request** | Body above |
| **Responses** | `201` `CategoryResponse`, `Location: /api/categories/{id}` · `400` `validation.failed`, `request.invalid`, `category.not_found` (on `parentId`), `category.too_deep`, `category.kind_mismatch` · `401` `auth.required` · `409` `category.name_taken`, `category.archived` (archived parent) |
| **Versions** | `ETag` |
| **Isolation** | Created for the user of the session; another user's category as parent is `400` `category.not_found` |
| **Tests** | `CategoriesTests.Create_*` |

#### Update a category

| | |
|---|---|
| **Route** | `PUT /api/categories/{id}` |
| **Auth** | Session |
| **Request** | Body above, header `If-Match` |
| **Responses** | `200` `CategoryResponse` · `400` as create, plus `category.parent_itself` · `401` `auth.required` · `404` `resource.not_found` · `409` `category.name_taken`, `category.kind_locked`, `category.has_subcategories` (a parent moved under another), `category.archived` (new archived parent), `budget.branch_conflict` (a budgeted category under a budgeted parent) · `412` · `428` |
| **Versions** | `If-Match` required; new `ETag` |
| **Isolation** | Another user's category: `404` before any version check; as parent: `400` `category.not_found` |
| **Tests** | `CategoriesTests.Update_*` |

Rename, move and change of kind in one request: moving a subcategory to the top level and changing its kind works in one `PUT` when the category is unused.

#### Archive and unarchive

| | |
|---|---|
| **Route** | `POST /api/categories/{id}/archive` · `POST /api/categories/{id}/unarchive` |
| **Auth** | Session |
| **Request** | No body, no `If-Match` |
| **Responses** | `200` `CategoryResponse` · `401` `auth.required` · `404` `resource.not_found` · `409` `category.archived` (unarchive of a subcategory whose parent is archived) |
| **Versions** | New `ETag` (unchanged when nothing changed) |
| **Isolation** | Another user's category: `404`, nothing changes |
| **Tests** | `CategoriesTests.Archive_*`, `Unarchive_*`, `ArchiveActions_*` |

Archiving a top-level category archives its subcategories too. `unarchive` restores only the category it is called on: subcategories are restored one by one, after their parent. Archived categories keep their transactions and budgets; new transactions on them are refused with `409` `category.archived` (block 6).

#### Delete a category

| | |
|---|---|
| **Route** | `DELETE /api/categories/{id}` |
| **Auth** | Session |
| **Request** | Header `If-Match` |
| **Responses** | `204` · `401` `auth.required` · `404` `resource.not_found` · `409` `category.has_subcategories`, `category.in_use` · `412` · `428` |
| **Versions** | `If-Match` required |
| **Isolation** | Another user's category: `404`, nothing deleted |
| **Tests** | `CategoriesTests.Delete_*` |

Only for an unused category without subcategories, e.g. one created by mistake; otherwise the app offers to archive it (`isInUse`, `hasSubcategories` tell it in advance).

### Transactions

Module `transactions` ([ADR-0017](../architecture/adr/0017-transactions-and-tags.md)). Every transaction endpoint returns the same `TransactionResponse`:

```json
{
  "id": "0193a5c4-9b31-7e02-a6d4-1f2e3d4c5b6a",
  "kind": "expense",
  "accountId": "0193a5c4-7e1f-7c3b-9a52-3f0e6d2b8a41",
  "categoryId": "0193a5c4-8a02-7d11-b1e0-5c7d0e9f2a10",
  "transferId": null,
  "date": "2026-11-12",
  "amount": 12.50,
  "signedAmount": -12.50,
  "description": "Pranzo",
  "tags": ["lavoro"],
  "version": 90411
}
```

| Field | Content |
|---|---|
| `kind` | `income`, `expense`, or `transfer` for a transfer leg (read-only here) |
| `categoryId` | Set for income and expenses; `null` for transfer legs |
| `transferId` | Set for transfer legs only: open the transfer to change or delete them |
| `amount` | Always positive, as the client sends it |
| `signedAmount` | Effect on the account balance: `+` income and incoming legs, `−` expenses and outgoing legs. Sum it for running balances |
| `tags` | Tag names, by name |

#### List transactions

| | |
|---|---|
| **Route** | `GET /api/transactions` |
| **Auth** | Session |
| **Request** | Query, all optional: `from`, `to` (`YYYY-MM-DD`, included), `accountId`, `categoryId` (a top-level category includes its subcategories), `kind` (`income` / `expense` / `transfer`), `tag` (name, any case), `q` (text in the description, any case, ≤ 100 characters; `%` and `_` are literal), `limit` (1–200, default 50), `cursor` |
| **Responses** | `200` `{ "items": [TransactionResponse, …], "nextCursor": "…" or null }` · `400` `validation.failed` on `limit`, `to` (before `from`), `q` (too long) · `400` `request.invalid` on `kind`, `cursor` (not issued by the API), or a malformed date or id · `401` `auth.required` |
| **Versions** | `version` on each item |
| **Isolation** | Only the user's transactions; a filter on another user's account or category finds nothing |
| **Tests** | `TransactionsTests.List_*` |

Newest first: by date, then by id (creation order for ids generated as UUIDv7). To read the next page, pass `nextCursor` back unchanged as `cursor` with the same filters; it is `null` on the last page. Items created after the first page never shift the next pages. No total count: period totals belong to the dashboard.

#### Get a transaction

| | |
|---|---|
| **Route** | `GET /api/transactions/{id}` |
| **Auth** | Session |
| **Responses** | `200` `TransactionResponse`, a transfer leg included · `401` `auth.required` · `404` `resource.not_found` |
| **Versions** | `ETag` |
| **Isolation** | Another user's transaction: `404` |
| **Tests** | `TransactionsTests.Get_*` |

#### Create a transaction

| | |
|---|---|
| **Route** | `POST /api/transactions` |
| **Auth** | Session |
| **Request** | `{ "id": "…", "kind": "expense", "accountId": "…", "categoryId": "…", "date": "2026-11-12", "amount": 12.50, "description": "Pranzo", "tags": ["lavoro"] }` |
| **Responses** | `201` `TransactionResponse`, `Location: /api/transactions/{id}` · `200` `TransactionResponse` when the `id` already exists for the user (request sent again) · `400` `validation.failed` (missing field, `kind: transfer`, `id` not UUIDv7, description > 200, more than 20 tags), `request.invalid`, `account.not_found` / `category.not_found` on the field, `amount.not_positive`, `amount.too_many_decimals`, `category.kind_mismatch`, `name.required` / `name.too_long` (a tag) · `401` `auth.required` · `409` `account.archived`, `category.archived`, `tag.name_taken` (rare race), `resource.conflict` on `id` (used by another user) |
| **Versions** | `ETag` |
| **Isolation** | Another user's account or category: `400` `{resource}.not_found`; another user's id: `409` without details |
| **Tests** | `TransactionsTests.Create_*` |

| Field | Rules |
|---|---|
| `id` | Optional UUID **version 7** chosen by the client. Makes the request safe to send again: the PWA sets it when it queues the expense |
| `kind` | `income` or `expense`; transfers through `/api/transfers` (block 7) |
| `accountId` | An active account of the user |
| `categoryId` | An active category of the user, of the same kind (an income needs an income category) |
| `date` | Required; future dates allowed (planned expenses) |
| `amount` | Greater than zero, at most 2 decimals; the sign follows `kind` |
| `description` | Optional, ≤ 200 characters, trimmed; empty becomes `null` |
| `tags` | Optional, ≤ 20 names; compared without regard to case; missing tags are created (≤ 50 characters each) |

#### Update a transaction

| | |
|---|---|
| **Route** | `PUT /api/transactions/{id}` |
| **Auth** | Session |
| **Request** | `{ "accountId": "…", "categoryId": "…", "date": "…", "amount": 12.50, "description": "…", "tags": [...] }` and header `If-Match`. No `kind`: it never changes (sent anyway, it is ignored). `tags` is the complete list: omitted or empty removes every tag |
| **Responses** | `200` `TransactionResponse` · `400` as create · `401` `auth.required` · `404` `resource.not_found` · `409` `transaction.is_transfer_leg`, `account.archived` / `category.archived` (only when newly chosen) · `412` · `428` |
| **Versions** | `If-Match` required; new `ETag`, also when only tags change |
| **Isolation** | Another user's transaction: `404`; another user's account or category in the body: `400` on the field |
| **Tests** | `TransactionsTests.Update_*` |

An expense on an account archived later can still be corrected; moving it to another archived account cannot.

#### Delete a transaction

| | |
|---|---|
| **Route** | `DELETE /api/transactions/{id}` |
| **Auth** | Session |
| **Request** | Header `If-Match` |
| **Responses** | `204` · `401` `auth.required` · `404` `resource.not_found` · `409` `transaction.is_transfer_leg` · `412` · `428` |
| **Versions** | `If-Match` required |
| **Isolation** | Another user's transaction: `404`, nothing deleted |
| **Tests** | `TransactionsTests.Delete_*` |

Deleted for good, with its tag links; the tags themselves stay.

### Tags

Part of the transactions module ([ADR-0017](../architecture/adr/0017-transactions-and-tags.md)); prefix `tags`. Tags are created by naming them in a transaction. `TagResponse`: `{ "id": "…", "name": "vacanza-2026", "transactionCount": 3, "version": 512 }`.

| Route | Request | Responses | Notes |
|---|---|---|---|
| `GET /api/tags` | — | `200` `{ "items": [TagResponse, …] }` by name · `401` | For autocompletion; `transactionCount` lets the app sort suggestions by use. Not paged |
| `PUT /api/tags/{id}` | `{ "name": "…" }` (required, ≤ 50) + `If-Match` | `200` `TagResponse` · `400` `validation.failed` · `401` · `404` · `409` `tag.name_taken` · `412` · `428` | Renames the tag on every transaction (e.g. a typo) |
| `DELETE /api/tags/{id}` | `If-Match` | `204` · `401` · `404` · `412` · `428` | Removes the tag from its transactions, which do not change |

Isolation: another user's tag is `404`. Tests: `TagsTests`.

### Transfers

Module `transfers` ([ADR-0018](../architecture/adr/0018-transfers-module.md)). Every transfer endpoint returns the same `TransferResponse`:

```json
{
  "id": "0193a5c4-a7c2-7f10-b3d1-6e5f4a3b2c1d",
  "fromAccountId": "0193a5c4-7e1f-7c3b-9a52-3f0e6d2b8a41",
  "toAccountId": "0193a5c4-7e33-7a90-8c11-0a9b8c7d6e5f",
  "date": "2026-11-12",
  "amount": 300.00,
  "description": "Risparmio",
  "outgoingTransactionId": "0193a5c4-a7c3-7b44-9d02-1c2b3a4d5e6f",
  "incomingTransactionId": "0193a5c4-a7c3-7b45-8e13-2d3c4b5a6f70",
  "version": 91022
}
```

`amount` is the amount moved, always positive. The two legs appear in `GET /api/transactions` with `kind: transfer`, `transferId` and `signedAmount` negative on the source, positive on the destination. There is no list of transfers: use `GET /api/transactions?kind=transfer` (with `accountId` for one account), then open a transfer by `transferId`.

Request body of create and update:

```json
{ "id": "…", "fromAccountId": "…", "toAccountId": "…", "date": "2026-11-12", "amount": 300.00, "description": "Risparmio" }
```

| Field | Rules |
|---|---|
| `id` | Create only, optional UUID **version 7** chosen by the client: sending the same request again returns the existing transfer with `200` |
| `fromAccountId`, `toAccountId` | Required, two different accounts of the user (`400` `transfer.same_account`) with the same currency (`400` `transfer.currency_mismatch`); another user's or nonexistent → `400` `account.not_found` on the field; a newly chosen archived account → `409` `account.archived` |
| `date` | Required; future dates allowed |
| `amount` | Greater than zero, at most 2 decimals |
| `description` | Optional, ≤ 200 characters, trimmed; on both legs |

| Route | Responses | Versions | Tests |
|---|---|---|---|
| `GET /api/transfers/{id}` | `200` `TransferResponse` · `401` · `404` | `ETag` | `TransfersTests.Get_*` |
| `POST /api/transfers` | `201` `TransferResponse`, `Location` · `200` (same `id` sent again) · `400` `validation.failed`, `account.not_found`, `transfer.same_account`, `transfer.currency_mismatch`, `amount.*` · `401` · `409` `account.archived`, `resource.conflict` on `id` | `ETag` | `TransfersTests.Create_*` |
| `PUT /api/transfers/{id}` | `200` `TransferResponse` · `400` as create · `401` · `404` · `409` `account.archived` (new account only) · `412` · `428` | `If-Match` required; new `ETag` | `TransfersTests.Update_*` |
| `DELETE /api/transfers/{id}` | `204` · `401` · `404` · `412` · `428` | `If-Match` required | `TransfersTests.Delete_*` |

- **Isolation**: another user's transfer is `404`; another user's account in the body is `400` on the field.
- **Update**: both legs change together and keep their ids. The version is the `transfers` row's, rewritten on every change, so a stale form gets `412` even though only the legs changed. A transfer on an account archived later can still be corrected.
- **Delete**: removes the transfer and both legs for good; both balances go back as before.
- Legs cannot be changed or deleted through `/api/transactions` (`409` `transaction.is_transfer_leg`).

### Budgets

Module `budgets` ([ADR-0019](../architecture/adr/0019-budgets-module.md)). A budget is a history of entries per expense category; each entry applies from its month until the next entry of the category. Months are written `"yyyy-MM"` everywhere (JSON and query).

`BudgetResponse`:

```json
{ "id": "…", "categoryId": "…", "from": "2026-06", "amount": 350.00, "version": 7781 }
```

`amount: null` means "no budget from this month".

| Route | Request | Responses | Versions | Tests |
|---|---|---|---|---|
| `GET /api/budgets` | Query `categoryId` (optional) | `200` `{ "items": [BudgetResponse, …] }` by category name, then month · `401` | `version` on each item | `BudgetsTests.List_*` |
| `GET /api/budgets/{id}` | — | `200` `BudgetResponse` · `401` · `404` | `ETag` | `BudgetsTests.Get_*` |
| `POST /api/budgets` | `{ "categoryId": "…", "from": "2026-06", "amount": 350.00 }`; `amount` optional (omitted or `null` ends the budget) | `201` `BudgetResponse`, `Location` · `400` `validation.failed` (missing `categoryId` or `from`), `request.invalid` (month not `yyyy-MM`), `category.not_found` on `categoryId`, `budget.category_not_expense`, `amount.not_positive`, `amount.too_many_decimals` · `401` · `409` `budget.month_taken`, `budget.branch_conflict`, `category.archived` (a new limit on an archived category) | `ETag` | `BudgetsTests.Create_*` |
| `PUT /api/budgets/{id}` | `{ "amount": 320.00 }` + `If-Match`; `null` or omitted ends the budget from that month | `200` `BudgetResponse` · `400` `amount.*` · `401` · `404` · `412` · `428` | `If-Match` required; new `ETag` | `BudgetsTests.Update_*` |
| `DELETE /api/budgets/{id}` | `If-Match` | `204` · `401` · `404` · `412` · `428` | `If-Match` required | `BudgetsTests.Delete_*` |

- **Isolation**: another user's entry is `404`; another user's category in the body is `400` `category.not_found`.
- **Changing the budget from a month**: if that month has an entry, `PUT` it; otherwise `POST` a new one (`409` `budget.month_taken` tells the client it already exists). Category and month of an entry never change: delete it and create another.
- **Stop or delete**: an entry without amount stops the budget and keeps the past; `DELETE` removes a wrong entry. Deleting every entry of a category makes it unbudgeted, so the budget can move to its subcategories (or to its parent).
- **One level per branch**: a budget on a subcategory of a budgeted parent, or on a parent with budgeted subcategories, is `409` `budget.branch_conflict`.

#### Budget progress

| | |
|---|---|
| **Route** | `GET /api/budgets/progress` |
| **Auth** | Session |
| **Request** | Query `month` (`yyyy-MM`, optional: the server's current month in UTC) |
| **Responses** | `200` `{ "items": [BudgetProgressResponse, …] }` by category name · `400` `request.invalid` on `month` · `401` |
| **Isolation** | Only the user's budgets and expenses |
| **Tests** | `BudgetsTests.Progress_*` |

```json
{ "categoryId": "…", "budgetId": "…", "month": "2026-06", "limit": 350.00, "spent": 120.25, "remaining": 229.75, "ratio": 0.3435714285714285714285714286, "isOverBudget": false }
```

- One item per category with a limit that month: the entry with the latest month not after it, unless it has no amount. Months before the first entry and after the end of a budget have no item.
- `spent`: expenses of the category **and its subcategories** dated in the month, positive. Income and transfers never count.
- `ratio` is exact (`spent / limit`, above 1 when over budget): round only when displaying. `remaining` is negative when over budget.
- The app should send `month` itself: near midnight on the last day of a month, the server's UTC month can differ from the user's.

### Resources

| Resource | Module prefix | Block | Status |
|---|---|---|---|
| Authentication (antiforgery, login, logout, me, change password, register) | `auth` | 3 | Implemented — [Authentication](#authentication) |
| Accounts | `accounts` | 4 | Implemented — [Accounts](#accounts) |
| Categories | `categories` | 5 | Implemented — [Categories](#categories) |
| Transactions (with tags, filters) | `transactions`, `tags` | 6 | Implemented — [Transactions](#transactions), [Tags](#tags) |
| Transfers | `transfers` | 7 | Implemented — [Transfers](#transfers) |
| Budgets | `budgets` | 8 | Implemented — [Budgets](#budgets) |
| Dashboard | `dashboard` | 9 | To do |

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| `/api/{module}` routes from `ApiModule.Prefix`, not from folder names | Renaming code never changes a public URL | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Development-only OpenAPI document and Scalar UI | Try endpoints locally without exposing the API surface on real instances | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Contract documented in the same pull request as the endpoint | The document never lags behind the code | [README.md](README.md) |
| Enumerations as `camelCase` strings, amounts as JSON numbers, every list in `{ "items": [...] }`, one response type per resource | Readable and stable contract; lists can gain paging without breaking clients | [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md) |
| Account balance computed in SQL at every read; `PUT` of the whole account; archive as idempotent actions; delete only without movements; archived accounts hidden by default | Balances always consistent; history never lost; simple forms | [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md) |
| Budgets: entries as a resource, months `yyyy-MM`, progress of a month from one query with the domain's arithmetic, stop with an entry without amount or delete a wrong one, no paging | Same rules as every resource; progress equal to the domain; history kept | [ADR-0019](../architecture/adr/0019-budgets-module.md) |
| Transfers: one response with both leg ids, no list of their own (legs via `/api/transactions?kind=transfer`), the `transfers` row version covers the legs, optional client id, no tags | One mechanism for listing movements; same headers as every resource; offline-safe | [ADR-0018](../architecture/adr/0018-transfers-module.md) |
| Transactions: kind + positive amount in, `amount` + `signedAmount` out; optional client UUIDv7 id makes create safe to repeat; cursor paging; single-value filters; tags by name created on the fly; transfer legs listed read-only | No sign bugs in clients; offline replay without duplicates; fast stable paging | [ADR-0017](../architecture/adr/0017-transactions-and-tags.md) |
| Categories as a flat list (parent then subcategories), filters `kind` and `includeArchived`, `PUT` of name + kind + parent, unarchive of one category at a time, delete only when unused and without subcategories, alphabetical order | One response type; one domain method for every edit; history and budgets keep their category | [ADR-0016](../architecture/adr/0016-categories-module.md) |

## Tests

- `ApiHostTests`: health is public; the OpenAPI document and Scalar exist in Development and not in Production; only the listed routes are public; every `PUT` / `PATCH` / `DELETE` requires `If-Match`.
- `ErrorHandlingTests`: `ETag` on create and read; `428` without `If-Match`; `400` on a malformed one; `412` on a stale version and on a write that lands between the check and the save, with nothing changed; new `ETag` after a successful update; `404` for another user's resource, even with a version.
- `EndpointMappingTests`: an endpoint of a module is served under `/api/{prefix}` with the module tag.
- `ApiHostTests.OpenApiDocument_NamesSchemasAfterTheirSlice_AndListsEnumerationValues`: distinct schema names for nested contracts, enum values as strings.
- `BudgetsTests`: every budget endpoint — success, `401`, validation, isolation, versions — plus history order, ending a budget, archived categories, one level per branch, moving a budget to subcategories after deleting the parent's entries; progress with the latest entry, subcategories, month boundaries, ended budgets, over budget, the default month, compared with `BudgetProgress.Calculate`.
- `TransfersTests`: every transfer endpoint — success, `401`, validation, isolation (route ids and both account fields), versions — plus balances of both accounts after create, update and delete, legs listed with `transferId`, repeated create with the same id, domain codes (same account, currencies, archived accounts).
- `TransactionsTests`: every transaction endpoint — success, `401`, validation, isolation (route ids, ids in the body, filters), versions — plus amounts and signs, cursor paging without gaps, every filter, repeated create with the same id, tags reused and created, tag-only changes bumping the version, transfer legs read-only, balance after create and delete.
- `TagsTests`: list with usage, rename (on every transaction), delete (links removed), with `401`, validation, isolation and versions.
- `CategoriesTests`: every category endpoint — success, `401`, validation, isolation (route ids and `parentId`), versions — plus display order, filters, usage flags, moves checked against budgets, archive and restore of a branch.
- `AccountsTests`: every account endpoint — success, `401`, validation, isolation, versions — plus the balance compared with `Account.BalanceAt`, enumerations as strings, malformed JSON in Development and Production.
- Each resource section names its test class; the minimum per endpoint is in [testing.md](testing.md#minimum-per-endpoint).

## Scalability

- **More resources**: one section per resource; the table above grows.
- **Versioning**: not needed while the only client ships with the API. With third-party or native clients, a version segment (`/api/v2`) or header with a new ADR; `ApiModule` groups make it a change in one place.
- **Generated clients**: the OpenAPI document can be exported at build time (`Microsoft.Extensions.ApiDescription.Server`) if `[Frontend]` generates its client.
- **PWA and caching**: the offline queue stores the version with each pending change and sends it as `If-Match`; a `412` means the item changed elsewhere. Reads can answer `304 Not Modified` to `If-None-Match` when needed.
- **OpenAPI**: `If-Match` and `ETag` are not described in the document yet; an operation transformer can add them for endpoints with `IfMatchRequiredMetadata`.
- **Balances with years of movements**: the account query sums every movement. When that shows in response times, the `Worker` writes monthly balance snapshots and the query adds only the movements after the last snapshot; the response does not change. A covering index with `amount` is a cheaper first step.
- **Many accounts** (shared households, multi-user): the list already uses the envelope, so paging can be added without breaking clients.
- **Text search**: `ILIKE` scans the user's transactions; with many years of data, a `pg_trgm` index on `description` (`[DB]`) keeps `q` fast without changing the API.
- **Exports**: the cursor walks the whole history page by page; a streaming export endpoint can reuse the same filters.
- **Attachments, recurring transactions, CSV import**: they reuse client or caller ids (`OwnedEntity` takes one), so a retried upload or import row never duplicates a transaction.
- **Category order**: a `position` field (user-defined order) or an order by usage for the PWA can be added to `CategoryResponse` without breaking clients; the in-memory ordering moves to SQL if a user ever has thousands of categories.
- **CSV import**: rows are matched to categories by normalized name, kind and parent — the same key as the unique index.
- **Currencies**: when transfers between currencies arrive, the balance stays per account in its own currency; totals across currencies belong to the dashboard with exchange rates.

## To do / open questions

- ✅ Status codes of errors, concurrency and cross-user access — [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md).
- Describe `If-Match` / `ETag` in the OpenAPI document (operation transformer), if the generated client needs it.
- ✅ Enum serialization, amounts in JSON, list envelope — [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md).
- ✅ Paging (cursor), signs of amounts, idempotent create — [ADR-0017](../architecture/adr/0017-transactions-and-tags.md).
- Contract of the dashboard (block 9).
- OpenAPI: describe months as `string` with pattern `^\d{4}-\d{2}$` (schema transformer for `YearMonth`).
- Progress over a range of months for charts, reusing `ProgressOf`, when charts arrive.
- Transfers: a list of their own, tags, transfers between currencies (`toAmount`) — when needed ([ADR-0018](../architecture/adr/0018-transfers-module.md)).
- `[Frontend]`: generate UUIDv7 ids for queued expenses; follow `nextCursor` for infinite scroll; never sum `amount`, use `signedAmount`; send the whole tag list on update.
- Tag merge (two tags into one) and multi-value filters, when needed.
- `[Frontend]`: build the grouped category menu from consecutive items; read `isInUse` / `hasSubcategories` to lock the kind and to offer archive instead of delete.
- `[Frontend]`: amounts are never added in floating point on the client; read `hasMovements` to lock the currency and to offer archive instead of delete.

---
_Last updated: 2026-11-12_
