# ADR-0002: Backend structure — vertical slices with Domain and Infrastructure projects

- **Status**: Accepted
- **Date**: 2026-11-09
- **Deciders**: Paul

## Context

- The domain is small but delicate: most operations are CRUD, a few rules protect money correctness (transfers between accounts, balances, budget progress).
- The project must be readable for external contributors (public repository) and grow without rewrites: more features, background processing (recurring transactions, CSV import), possibly multi-user ([ADR-0001](0001-authentication.md)).
- Features are developed end-to-end, one conversation per feature after the MVP.

## Decision

The .NET solution has **three projects**:

1. **`Api`** — ASP.NET Core host. Use cases are organized as **vertical slices** grouped by module (`Features/<Module>/<Slice>`), each containing request, response, validation, handling and endpoint. Cross-cutting HTTP concerns live in `Api/Common`.
2. **`Domain`** — entities, value objects and business rules, with **no dependencies** on EF Core or ASP.NET Core.
3. **`Infrastructure`** — `AppDbContext`, entity configurations, migrations, interceptors, Identity.

Dependencies: `Api → Infrastructure → Domain`, `Api → Domain`. Slices use `AppDbContext` directly, with **no repository layer**. A future `Worker` host references `Domain` and `Infrastructure`.

Duplication is handled by sharing mechanisms (query filters, interceptors, endpoint groups, global validation and error handling) and business rules, while accepting duplicated request/response shapes between slices. Details in [backend-structure.md](../backend-structure.md#keeping-duplication-under-control).

## Consequences

**Positive**
- Adding a feature means adding a slice: little ceremony, code that changes together lives together.
- Business rules have a single, dependency-free home, easy to unit test.
- Persistence and Identity are reusable by other hosts (worker, migration runner, tools).
- Module folders give a path to a modular monolith without rewrites.

**Negative**
- Boundaries inside `Api` (between slices and modules) rely on conventions, not on the compiler.
- Some request/response shapes are duplicated between slices, by design.
- A rule is needed on what belongs in `Domain` versus a slice; it is documented in `backend-structure.md` and must be kept up to date.
- Slices depend on EF Core directly: changing ORM would touch many slices (considered unlikely).

## Alternatives considered

**A. Clean Architecture with four projects (Api / Application / Domain / Infrastructure)** — Rejected. Strong compiler-enforced boundaries and familiar to many .NET developers, but heavy ceremony for a mostly-CRUD app (repository interfaces over EF Core, mapping at every layer); each feature spans four projects, and horizontal layers fit poorly with a later split into modules.

**B. Vertical slices in a single project** — Rejected. Lowest ceremony, but no home for shared business rules (risk of duplicating balance and transfer logic) and the `DbContext` would live in the API host, so a future worker could not reuse it.

**C. Vertical slices + Domain project only** — Rejected in favor of this decision. Same benefits, but persistence inside `Api` blocks reuse by a future worker; the extra `Infrastructure` project costs little.
