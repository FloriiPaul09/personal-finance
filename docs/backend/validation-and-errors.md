# Validation and errors

## Scope

How the API checks requests and how every failure becomes an HTTP response: validation of the request shape, business rule violations (`DomainException`), duplicates, concurrency conflicts, references to data that is not the user's, unexpected errors. Versions (`ETag` / `If-Match`) are described in [api-endpoints.md](api-endpoints.md#versions); access to other users' data in [security.md](security.md#another-users-data).

## Current state

Implemented ([ADR-0012](../architecture/adr/0012-api-foundation.md), [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md)) in `Api/Common/Errors` and `Api/Common/Concurrency`. Request validation is tested from the authentication endpoints on (`AuthTests.Login_WithAnInvalidEmail_Returns400OnTheField`).

### Two levels of checks

| Level | Checks | Where | Failure |
|---|---|---|---|
| Request shape | Required fields, lengths, ranges, formats, enum values, paging limits | Request records of the slice, built-in validation | `400`, `validation.failed`, errors by field |
| Business rules | Everything that protects money and data: amounts (zero, more than 2 decimals), archived accounts, category kinds, transfers, budgets | `Domain` entities and services ([database/domain-model.md](../database/domain-model.md)) | `DomainException` with a stable code → [Domain errors](#domain-errors) |

A rule is never written twice: if `Domain` already enforces it, the request record does not repeat it, except for checks that give a better message per field (e.g. a required name). The `Worker` and the CSV import go through the same `Domain` rules, so they get the same codes.

### Response shape

Every error is a ProblemDetails (RFC 9457, `application/problem+json`) with two extensions:

| Property | Content |
|---|---|
| `status`, `title`, `type` | Standard; `title` and `type` follow the status |
| `detail` | English message for developers and logs. Absent on `500`. Never shown as is to the user |
| `code` | **Stable code** the frontend switches on and translates — the contract |
| `traceId` | Id to quote when reporting a problem; the same id is in the logs |
| `errors` | Only on `400` about fields: field name → messages |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "An archived account accepts no new transactions.",
  "code": "account.archived",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

How it is produced (`AddApiErrors()` / `UseApiErrors()`, before authentication):

- `ApiExceptionHandler` (an `IExceptionHandler`) maps the known exceptions below.
- Anything else is logged by the exception handler middleware and answered `500` with code `server.error`, without details.
- `UseStatusCodePages` gives a ProblemDetails body to responses that have none: `TypedResults.NotFound()`, the `401` of the cookie, `405`, `415`.
- `CustomizeProblemDetails` adds `traceId`, and a `code` from the status when there is none.

### Codes

| Code | Status | When |
|---|---|---|
| `validation.failed` | 400 | Request shape invalid (built-in validation), `errors` by field |
| `request.invalid` | 400 | Malformed request (e.g. invalid JSON, wrong route value type) |
| `{resource}.not_found` | 400 | An id in the body is not one of the user's (`account.not_found`, `category.not_found`, …), `errors` on that field |
| `concurrency.version_invalid` | 400 | `If-Match` malformed (weak tag, `*`, several tags) |
| *domain code* | 400 / 409 | [Domain errors](#domain-errors) |
| `auth.antiforgery_invalid` | 400 | Unsafe request without a valid `X-XSRF-TOKEN` ([security.md](security.md#csrf)) |
| `auth.password_incorrect` | 400 | Change password: wrong current password, on `currentPassword` |
| `auth.password_rejected` | 400 | Password refused by the rules (e.g. shorter than 12), on the password field |
| `auth.required` | 401 | No session |
| `auth.invalid_credentials` | 401 | Login failed: wrong email, wrong password or locked account — one answer for all |
| `auth.forbidden` | 403 | Session without the permission (no roles yet) |
| `auth.email_taken` | 409 | Registration with an email already in use, on `email` |
| `resource.not_found` | 404 | No such resource **for this user** — including another user's ([security.md](security.md#another-users-data)) |
| `request.method_not_allowed` | 405 | Method not supported on the route |
| `account.name_taken`, `category.name_taken`, `tag.name_taken`, `budget.month_taken` | 409 | [Duplicates](#duplicates) |
| `concurrency.version_mismatch` | 412 | The resource changed since the client read it ([api-endpoints.md](api-endpoints.md#versions)) |
| `request.unsupported_media_type` | 415 | Body is not JSON |
| `concurrency.version_required` | 428 | `PUT` / `PATCH` / `DELETE` without `If-Match` |
| `request.too_many` | 429 | Rate limit on login and registration |
| `server.error` | 500 | Unexpected error, or a domain invariant broken by a bug |

API codes are constants of `ApiErrors`; domain codes of `DomainErrors`.

### Domain errors

`DomainErrorStatuses` gives each `DomainException` code a status by family:

| Family | Status | Codes |
|---|---|---|
| Invalid values — the values sent cannot be accepted, whatever the data | `400` | `amount.not_positive`, `amount.too_many_decimals`, `amount.out_of_range`, `name.required`, `name.too_long`, `description.too_long`, `currency.invalid`, `month.invalid`, `account.type_invalid`, `category.kind_invalid`, `category.too_deep`, `category.parent_itself`, `category.kind_mismatch`, `category.kind_follows_parent`, `category.not_a_subcategory`, `transfer.same_account`, `transfer.currency_mismatch`, `budget.category_not_expense` |
| State conflicts — the values are fine, the current data does not allow the change | `409` | `account.archived`, `account.currency_locked`, `category.archived`, `category.has_subcategories`, `category.kind_locked`, `transaction.is_transfer_leg`, `budget.branch_conflict` |
| Programming errors — invariants slices must guarantee before calling the domain | `500` (logged, no details) | `user.required`, `user.different`, `account.transaction_of_another_account`, `budget.entries_of_another_category` |

The `detail` is the domain message (English). A new code in `DomainErrors` makes `ErrorMappingTests` fail until it is placed in a family.

### Duplicates

Names are unique per user through unique indexes ([database/conventions.md](../database/conventions.md)). Slices do not check before saving: the database refuses the duplicate (`23505`), even when two devices send it at the same moment, and `UniqueConstraintErrors` maps the index name to a code:

| Unique index | Code |
|---|---|
| `ix_accounts_user_id_normalized_name` | `account.name_taken` |
| `ix_categories_user_id_kind_parent_id_normalized_name` | `category.name_taken` |
| `ix_tags_user_id_normalized_name` | `tag.name_taken` |
| `ix_budgets_user_id_category_id_effective_from` | `budget.month_taken` |
| `ix_transactions_transfer_id_outgoing`, `…_incoming` | Internal invariant → `500` |

`UniqueConstraintErrorsTests` reads the unique indexes from PostgreSQL and fails when one is neither mapped nor listed as internal. Other database errors (foreign keys, `CHECK`) are prevented by the domain and give `500`.

### References in the body

`GetReferenceAsync` loads the entity an id in the body points to, through the query filter:

```csharp
var account = await db.Accounts.GetReferenceAsync(request.AccountId, "account", "accountId", ct);
```

Another user's id and a nonexistent id give the same `400` with code `account.not_found` and `errors.accountId`.

### Request validation

Built-in .NET 10 Minimal API validation, registered once in `Program.cs` with `builder.Services.AddValidation()`. A source generator finds the parameters of every endpoint and validates them before the handler runs.

```csharp
public sealed record Request(
    [Required, MaxLength(100)] string Name,
    [Required, RegularExpression("^[A-Z]{3}$")] string Currency,
    decimal InitialBalance);
```

- Request records are **`public`**: the generator skips non-public types without warning.
- Attributes go on the positional parameters of the record (no `[property: …]` needed).
- Rules between fields: implement `IValidatableObject` on the record.
- Nested records and collections are validated recursively.
- An endpoint can opt out with `.DisableValidation()` (none should need it).

The response is `400` with `code: "validation.failed"` and `errors` by field. Field names and messages are reviewed with the first endpoint and `[Frontend]`.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Built-in .NET 10 validation for the request shape | No dependency; generated at compile time; standard `400` validation problem | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Business rules only in `Domain`, raised as `DomainException` with a stable code | One place for rules, shared by API and `Worker` | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| One global `IExceptionHandler`; ProblemDetails with `code` and `traceId` everywhere | One shape for the frontend; slices stay linear | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md) |
| Domain codes by family (`400` / `409` / `500`) in one table checked by a test | Meaningful statuses, no forgotten code | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md) |
| Duplicates detected by the database (`23505` → `*.name_taken`, `409`) | Correct under simultaneous requests, no repeated checks | [ADR-0013](../architecture/adr/0013-error-responses-and-concurrency.md) |

## Tests

- `ErrorMappingTests`: every `DomainErrors` code is mapped and nothing else; status of each family; code of each status; entity tag format and parsing.
- `ErrorHandlingTests`, on a host with sample endpoints over the class database: `400` / `409` / `500` from domain codes (no details on `500`), unexpected exception, empty `404` and `401` with a ProblemDetails body, duplicate name `409`, references to another user's or a nonexistent id `400` on the field. Every response is checked for `application/problem+json`, `status`, `code` and `traceId`.
- `UniqueConstraintErrorsTests`: unique indexes in PostgreSQL = mapped + internal.
- From block 4, every endpoint that accepts input has a test that sends an invalid request and checks `400` and the field in `errors` ([testing.md](testing.md#minimum-per-endpoint)).

## Scalability

- **More complex conditional rules**: `IValidatableObject` first; FluentValidation can be added per slice if that grows (ADR-0012).
- **CSV import**: rows go through `Domain`; the stable codes allow a per-row report.
- **Localization**: the frontend translates stable codes; server messages stay in English.
- **New tables with unique names**: the unique index test fails until the code is added.
- **Public API**: the codes table becomes part of the published contract; codes are never renamed, only added.

## To do / open questions

- Block 4: review the shape of validation errors with `[Frontend]` (field names, messages).
- `[Frontend]`: map `code` to Italian messages; read `errors` for form fields; on `412` reload the resource and tell the user it changed elsewhere.

---
_Last updated: 2026-11-12_
