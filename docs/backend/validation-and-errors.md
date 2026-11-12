# Validation and errors

## Scope

How the API checks requests and how every failure becomes an HTTP response: validation of the request shape, business rule violations (`DomainException`), concurrency conflicts, access to data that is not the user's, unexpected errors.

## Current state

**Validation decided and registered** ([ADR-0012](../architecture/adr/0012-api-foundation.md)); its first tests come with the first endpoint that accepts input (block 4). **Error mapping open**: block 2.

### Two levels of checks

| Level | Checks | Where | Failure |
|---|---|---|---|
| Request shape | Required fields, lengths, ranges, formats, enum values, paging limits | Request records of the slice, built-in validation | `400` validation problem, errors by field |
| Business rules | Everything that protects money and data: amounts (zero, more than 2 decimals), archived accounts, category kinds, transfers, budgets | `Domain` entities and services ([database/domain-model.md](../database/domain-model.md)) | `DomainException` with a stable code → mapping in block 2 |

A rule is never written twice: if `Domain` already enforces it, the request record does not repeat it, except for checks that give a better message per field (e.g. a required name). The `Worker` and the CSV import go through the same `Domain` rules, so they get the same codes.

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

Response for an invalid request (`400`, `application/problem+json`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["The Name field is required."]
  }
}
```

Field names and messages (language, casing) are reviewed with the first endpoint and `[Frontend]`, which maps errors to form fields.

### Errors (to decide in block 2)

| Case | Source | Options |
|---|---|---|
| Business rule violated | `DomainException.Code` (e.g. `account.archived`) | Status per code family (`400` / `409` / `422`) and the code in a ProblemDetails extension |
| Concurrency conflict | `DbUpdateConcurrencyException` on `xmin` ([database/conventions.md](../database/conventions.md)) | `409` with the version in the body, or `412` with `ETag` / `If-Match` |
| Another user's data | The query filter returns nothing | `404` (existence not revealed) or `403` |
| `UserIsolationException` | Programming error ([database/user-isolation.md](../database/user-isolation.md)) | `500`, logged |
| Unexpected exception | — | `500` ProblemDetails without details outside Development |

Until block 2 an unhandled exception gives the ASP.NET Core default (`500`, developer page in Development).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Built-in .NET 10 validation for the request shape | No dependency; generated at compile time; standard `400` validation problem | [ADR-0012](../architecture/adr/0012-api-foundation.md) |
| Business rules only in `Domain`, raised as `DomainException` with a stable code | One place for rules, shared by API and `Worker` | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |

## Tests

- Every endpoint that accepts input has a test that sends an invalid request and checks `400` and the field in `errors` ([testing.md](testing.md#minimum-per-endpoint)), from block 4.
- Business rules are unit tested in `Domain.Tests`; their HTTP mapping gets one test per code family in block 2.

## Scalability

- **More complex conditional rules**: `IValidatableObject` first; FluentValidation can be added per slice if that grows (ADR-0012).
- **CSV import**: rows go through `Domain`; the stable codes allow a per-row report.
- **Localization**: the frontend translates stable codes rather than server messages; server messages stay in English.

## To do / open questions

- Block 2: everything in [Errors](#errors-to-decide-in-block-2).
- Block 4: shape of validation errors as seen by the frontend (field names, messages), first validation tests.

---
_Last updated: 2026-11-12_
