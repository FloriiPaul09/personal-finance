# ADR-0029: A precise OpenAPI contract, written at build time

- **Status**: Accepted
- **Date**: 2026-11-19
- **Deciders**: Paul

## Context

- The frontend generates its types from the OpenAPI document of the API with `openapi-typescript` ([ADR-0021](0021-frontend-foundation.md), [ADR-0022](0022-frontend-api-integration.md)). The document was looser than the API, so `frontend/scripts/api-types.mjs` corrected it before generating:
  - every number was described as `number | string` with a pattern, because ASP.NET Core's web JSON defaults (`JsonSerializerDefaults.Web`) also *read* numbers written as text;
  - `YearMonth` (months of budgets and dashboard, [ADR-0019](0019-budgets-module.md)) was an empty schema: its JSON converter is invisible to the generator; `?month=` was a free string;
  - `If-Match`, `ETag` and the `412` / `428` answers of [ADR-0013](0013-error-responses-and-concurrency.md) were not described at all, nor was the shape of an error.
- Validation errors named fields after the C# properties (`Email`, `AccountId`); the app lowered the first letter to find the form field.
- `[DevOps]` will check in CI that the committed snapshot and `schema.ts` match the API: that needs the document without starting the API and a database.
- The roadmap adds trend charts with months in the query (`?from=2026-01&to=2026-06`, [ADR-0020](0020-dashboard-module.md)), a PWA with an offline queue that replays versions, possibly native clients or a public API, more endpoints from `[Feature]` chats.

## Decision

1. **Numbers only as JSON numbers**: `JsonConventions` sets `NumberHandling = Strict`. The generator then describes amounts as `number` and versions as `integer` by itself. A number in quotes (`"12.5"`) is unreadable JSON → `400` `request.invalid`, like a number for an enumeration ([ADR-0015](0015-resource-conventions-and-accounts.md)).
2. **Months as `yyyy-MM` strings**: a schema transformer gives the `YearMonth` component `type: string`, pattern `^\d{4}-(0[1-9]|1[0-2])$`, a description and an example. Query parameters that hold a month carry `[MonthQuery]`; an operation transformer points them to the same component. The handler still parses them with `QueryValues.ParseMonth`, so a malformed month keeps its `400` on the parameter.
3. **Versions by convention**: an operation transformer
   - adds a required `If-Match` header (strong tag, pattern `^"\d+"$`) to every endpoint that takes an `IfMatch` parameter — the same metadata `ApiHostTests.EveryUpdateAndDelete_RequiresIfMatch` checks — with the `400` (`concurrency.version_invalid`), `412` and `428` responses;
   - adds an `ETag` header to every `200` / `201` whose body is one resource with a `Version` property. Lists have no `ETag`: their items carry `version`.
   No code in the slices: endpoints added later are described automatically.
4. **One error schema**: a document transformer adds the `ProblemDetails` component (`status`, `code`, `traceId` required; `errors` as messages by field), referenced by the error responses above as `application/problem+json`. Only the concurrency errors are described for now; the other errors (`401`, `404`, `409`, validation `400`) stay documented in [validation-and-errors.md](../../backend/validation-and-errors.md) and read from `code` at runtime.
5. **Field names in camelCase**: the ProblemDetails customization renames the keys of `errors` with the JSON naming policy of the API, segment by segment (`Tags[0]` → `tags[0]`, `Lines[1].AccountId` → `lines[1].accountId`), merging messages of keys that become equal. It covers every source: built-in validation and the errors the API raises on a field.
6. **Document written at build time, on request**: `Microsoft.Extensions.ApiDescription.Server` writes `backend/artifacts/openapi/openapi.json` (ignored by git) when the build runs with `-p:OpenApiGenerateDocuments=true`; off by default. While the build runs the app to read the document, startup skips migrations and the initial user (`ApiDocumentationExtensions.IsGeneratingDocument`). The document has no `servers`, so the built and the served documents are the same.
7. **Frontend without corrections**: `pnpm api:types` builds the API with the export on and generates the types from that file (`--file` for a document exported elsewhere, `--url` for a running API); the snapshot `core/api/openapi.json` and `schema.ts` are generated as they are. `ApiError` keeps the field names of the answer.

## Consequences

**Positive**
- The generated types are right without hand-written rules: a change in the backend shows in the snapshot diff of the same pull request.
- The CI of `[DevOps]` needs only the .NET SDK and Node: `dotnet build … -p:OpenApiGenerateDocuments=true`, `pnpm api:types --file …`, `git diff --exit-code`.
- The contract is stricter and honest: what the document says is what the API accepts. Native clients or a public API can rely on it.
- Trend charts reuse `[MonthQuery]` for their month parameters; new update and delete endpoints get `If-Match` and its errors without code; the offline queue of the PWA finds the header and the `412` in the contract.
- Forms put server errors on fields by name, with no translation step.

**Negative**
- Behavior change: a number written as text was accepted before and is refused now. The app always sent numbers; a test fixes the new rule.
- `ETag` is deduced from the response type, not from what the handler does; `OpenApiDocumentTests.EveryDocumentedETag_ComesFromAHandlerThatCanSendIt` and the `ETag` tests of each resource cover the gap.
- Building the document runs the app's startup code at build time: startup work that needs external services must check `IsGeneratingDocument`.
- Error keys are renamed after the fact; if a later .NET version names them from JSON itself, the step becomes a no-op.

## Alternatives considered

**Keep the web defaults and remove `string` from numbers with a schema transformer** — Rejected: the document would describe less than the API accepts, and the same workaround would move from the frontend to the backend.

**`YearMonth` component only, months in the query as free strings** — Rejected: trend charts will add month parameters; the mechanism costs one attribute.

**`ETag` declared on each endpoint (`.WithETag()`)** — Rejected: about 25 endpoints repeating what the response type already says, and easy to forget on new ones.

**Describe every error response now** — Postponed: a lot of per-endpoint metadata for little gain while the only client reads `code` at runtime. To reconsider if the API becomes public.

**Validation names from `[Display(Name = …)]` on every field** — Rejected: changes the messages too and must be remembered on every new field.

**Export the document straight into the frontend snapshot, or on every build** — Rejected: a backend build would change frontend files without Prettier, and every build (tests and `dotnet watch` included) would run the app's startup.
