# API endpoints

## Scope

Conventions shared by every endpoint and the contract of each endpoint: route, authentication, request, responses. One section per resource. How endpoints are written is in [README.md](README.md#endpoints); errors in [validation-and-errors.md](validation-and-errors.md); authentication in [security.md](security.md).

## Current state

The foundation is implemented: health, development tools, authentication, the mechanism that maps every module ([ADR-0012](../architecture/adr/0012-api-foundation.md)), error responses and versions ([ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)). The conventions shared by resources and the first resource, accounts, are in [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md); categories in [ADR-0016](../architecture/adr/0016-categories-module.md). Resource endpoints arrive one block at a time; their contract is written here in the same pull request as the code.

### Conventions

| Topic | Convention |
|---|---|
| Base path | `/api`; each module adds its prefix: `/api/accounts`, `/api/categories`, … The Angular dev server and nginx proxy `/api` ([devops/local-development.md](../devops/local-development.md)) |
| Format | JSON, `camelCase` properties (ASP.NET Core default); **enumerations as `camelCase` strings** (`"creditCard"`) — numbers and unknown values give `400` `request.invalid` ([ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md)). Options in `JsonConventions` |
| Authentication | Session cookie on every endpoint unless marked **public** ([security.md](security.md)); without a session `401` |
| Identifiers | `Guid` (UUIDv7) in routes: `/api/accounts/{id:guid}` |
| Amounts | **JSON numbers** with at most 2 decimals (`1234.56`), `decimal` on the server; a third decimal is refused (`amount.too_many_decimals`), never rounded. Totals and balances are computed by the server: clients never add amounts in floating point. Exposure of signs on movements decided in block 6 |
| Dates | Transaction dates `YYYY-MM-DD` (`DateOnly`); timestamps ISO 8601 in UTC |
| Methods | `GET` read, `POST` create (`201` + `Location` + `ETag`), `PUT` replace the whole resource (`200` + `ETag`), `DELETE` (`204`), `POST /{id}/{action}` for idempotent state changes such as `archive` (`200` + `ETag`). `PUT`, `PATCH` and `DELETE` require `If-Match` ([Versions](#versions)) |
| Errors | ProblemDetails (RFC 9457) with a stable `code` and a `traceId`; validation errors `400` with `errors` by field ([validation-and-errors.md](validation-and-errors.md)) |
| Not found | `404` `resource.not_found` for a route id that is not the user's — another user's id included ([security.md](security.md#another-users-data)) |
| Responses | One response type per resource (e.g. `AccountResponse`), the same for list items, reads and writes, with `version` in the body |
| Lists | **Envelope** `{ "items": [...] }` (`ListResponse<T>`) for every list, paged or not; filters in the query string (`?includeArchived=true`, `?kind=expense`) — enumerations in the query follow the JSON names (`QueryValues.ParseEnum`). Paging and sorting parameters decided with the first list that needs them (transactions) |
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

### Resources

| Resource | Module prefix | Block | Status |
|---|---|---|---|
| Authentication (antiforgery, login, logout, me, change password, register) | `auth` | 3 | Implemented — [Authentication](#authentication) |
| Accounts | `accounts` | 4 | Implemented — [Accounts](#accounts) |
| Categories | `categories` | 5 | Implemented — [Categories](#categories) |
| Transactions (with tags, filters) | `transactions` | 6 | To do |
| Transfers | `transfers` | 7 | To do |
| Budgets | `budgets` | 8 | To do |
| Dashboard | `dashboard` | 9 | To do |

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| `/api/{module}` routes from `ApiModule.Prefix`, not from folder names | Renaming code never changes a public URL | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Development-only OpenAPI document and Scalar UI | Try endpoints locally without exposing the API surface on real instances | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Contract documented in the same pull request as the endpoint | The document never lags behind the code | [README.md](README.md) |
| Enumerations as `camelCase` strings, amounts as JSON numbers, every list in `{ "items": [...] }`, one response type per resource | Readable and stable contract; lists can gain paging without breaking clients | [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md) |
| Account balance computed in SQL at every read; `PUT` of the whole account; archive as idempotent actions; delete only without movements; archived accounts hidden by default | Balances always consistent; history never lost; simple forms | [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md) |
| Categories as a flat list (parent then subcategories), filters `kind` and `includeArchived`, `PUT` of name + kind + parent, unarchive of one category at a time, delete only when unused and without subcategories, alphabetical order | One response type; one domain method for every edit; history and budgets keep their category | [ADR-0016](../architecture/adr/0016-categories-module.md) |

## Tests

- `ApiHostTests`: health is public; the OpenAPI document and Scalar exist in Development and not in Production; only the listed routes are public; every `PUT` / `PATCH` / `DELETE` requires `If-Match`.
- `ErrorHandlingTests`: `ETag` on create and read; `428` without `If-Match`; `400` on a malformed one; `412` on a stale version and on a write that lands between the check and the save, with nothing changed; new `ETag` after a successful update; `404` for another user's resource, even with a version.
- `EndpointMappingTests`: an endpoint of a module is served under `/api/{prefix}` with the module tag.
- `ApiHostTests.OpenApiDocument_NamesSchemasAfterTheirSlice_AndListsEnumerationValues`: distinct schema names for nested contracts, enum values as strings.
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
- **Category order**: a `position` field (user-defined order) or an order by usage for the PWA can be added to `CategoryResponse` without breaking clients; the in-memory ordering moves to SQL if a user ever has thousands of categories.
- **CSV import**: rows are matched to categories by normalized name, kind and parent — the same key as the unique index.
- **Currencies**: when transfers between currencies arrive, the balance stays per account in its own currency; totals across currencies belong to the dashboard with exchange rates.

## To do / open questions

- ✅ Status codes of errors, concurrency and cross-user access — [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md).
- Describe `If-Match` / `ETag` in the OpenAPI document (operation transformer), if the generated client needs it.
- ✅ Enum serialization, amounts in JSON, list envelope — [ADR-0015](../architecture/adr/0015-resource-conventions-and-accounts.md).
- Paging and sorting parameters (block 6, transactions).
- Contracts of the remaining resources (blocks 6–9).
- `[Frontend]`: build the grouped category menu from consecutive items; read `isInUse` / `hasSubcategories` to lock the kind and to offer archive instead of delete.
- `[Frontend]`: amounts are never added in floating point on the client; read `hasMovements` to lock the currency and to offer archive instead of delete.

---
_Last updated: 2026-11-12_
