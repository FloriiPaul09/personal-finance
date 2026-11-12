# ADR-0012: API foundation — endpoint discovery, secure-by-default authorization, validation and tooling

- **Status**: Accepted
- **Date**: 2026-11-12
- **Deciders**: Paul

## Context

- [ADR-0003](0003-minimal-api.md) chose native Minimal API with one endpoint per slice, automatic registration and one route group per module, leaving the exact mechanism to `[Backend]`.
- [ADR-0001](0001-authentication.md) requires a secure default: nothing reachable without a session except login and health.
- The data model is implemented ([ADR-0010](0010-data-model-and-user-isolation.md)): the query filter `UserScope` and the `SaveChanges` interceptor read an `ICurrentUser`, which needs an HTTP implementation.
- The roadmap adds a `Worker` (recurring transactions, CSV import), exports, charts, attachments, a PWA and multi-user. Contributors may join once the repository is public.
- Every core dependency is something to learn and keep up to date; MediatR and AutoMapper became commercial in 2025.

## Decision

1. **Endpoint discovery**: an endpoint implements `IEndpoint<TModule>`; a module is a sealed `ApiModule` subclass with `Prefix` and `Tag`. At startup a reflection scan maps every endpoint on `/api/{prefix}` of its module, with the module's OpenAPI tag. Endpoints without a module, in two modules, or modules with an invalid or duplicate prefix stop startup.
2. **Authorization**: a fallback policy requires an authenticated user on every endpoint; only listed public endpoints use `AllowAnonymous()`. The Identity cookie answers `401` / `403`, never a redirect.
3. **Current user**: `HttpCurrentUser` (scoped) reads the `NameIdentifier` claim through `IHttpContextAccessor` on every access; anything that is not a valid id gives no user.
4. **Validation**: built-in .NET 10 Minimal API validation (`AddValidation()`, data annotations and `IValidatableObject` on public request records). It checks the shape of a request; business rules stay in `Domain`.
5. **Mapping**: manual — projections with `Select` in queries, small static mapping methods in the slice. No mapping library.
6. **No mediator**: the endpoint calls the slice handler directly; cross-cutting HTTP behavior uses endpoint filters.
7. **OpenAPI**: the built-in document generator; the document (`/openapi/v1.json`) and the Scalar UI (`/scalar`) are mapped in Development only.
8. **Architecture tests** with ArchUnitNET, plus `Microsoft.CodeAnalysis.BannedApiAnalyzers` in `Api`: calling `IgnoreQueryFilters` is a compile error there.

Details: [backend/README.md](../../backend/README.md), [backend/security.md](../../backend/security.md), [backend/validation-and-errors.md](../../backend/validation-and-errors.md), [backend/testing.md](../../backend/testing.md).

## Consequences

**Positive**
- A slice is one file; a module is one class. URLs do not depend on folder names, so code can move toward a modular monolith without breaking the frontend.
- A forgotten authorization call leaves an endpoint closed, never open: the failure mode that matters for financial data and multi-user.
- The current user reads the claim, not the cookie: it works unchanged behind several API instances and with bearer tokens for future clients.
- No third-party library in the request pipeline. Projections read only the needed columns, which scales with years of transactions, exports and charts.
- Architecture rules and the banned API are enforced by the build and the tests, not by memory during reviews.

**Negative**
- Discovery is project code: written, tested and documented here.
- Built-in validation is recent; conditional rules are written by hand in `IValidatableObject`. Request records must be `public`, or the source generator silently skips them.
- Some mapping code is repeated between slices (accepted by the duplication rule of [backend-structure.md](../backend-structure.md#keeping-duplication-under-control)).
- Without a mediator, use cases are not callable from the `Worker`, which does not reference `Api`. Shared business logic must therefore stay in `Domain`; when the `Worker` arrives, where shared orchestration lives gets its own ADR.
- Requests matching no endpoint also get `401` when anonymous (fallback policy), not `404`.

## Alternatives considered

**Discovery by namespace convention** (`Features.Accounts` → `/api/accounts`) — Rejected. No code per module, but renaming a folder changes public URLs, and auth or dashboard routes need exceptions.

**Explicit registration in each module file** — Rejected. Easy to read, but a forgotten endpoint is never mapped, against the automatic registration of ADR-0003.

**`RequireAuthorization()` on each module group** — Rejected. Visible in the module, but a forgotten group or an endpoint outside a group stays public.

**Current user set by a middleware** — Rejected. No accessor, but it depends on the order of the pipeline; a mistake shows up as invisible data with no clear error.

**FluentValidation** — Rejected for now. Expressive and well known, but one more dependency and a filter to maintain; can be introduced per slice later if conditional rules grow.

**Mapperly** — Rejected. Less code, but a dependency and less visible mappings for small DTOs. **AutoMapper** — rejected: commercial license, runtime mapping.

**`Mediator` (source-generated) or MediatR** — Rejected. Pipelines reusable outside HTTP, but one more indirection on every slice for a need that does not exist yet; MediatR is commercial. To be reconsidered with the `Worker`.

**Swagger UI or no UI** — Rejected. Swagger UI is better known but dated; no UI means trying endpoints only from `.http` files.

**NetArchTest** — Rejected. Simple fluent API, but it checks type dependencies only and cannot forbid a single method such as `IgnoreQueryFilters`.
