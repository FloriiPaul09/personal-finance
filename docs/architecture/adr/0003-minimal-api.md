# ADR-0003: Endpoints with native Minimal API

- **Status**: Accepted
- **Date**: 2026-11-09
- **Deciders**: Paul

## Context

- [ADR-0002](0002-backend-structure.md) organizes the `Api` project in vertical slices grouped by module: each slice should contain everything about one use case, endpoint included.
- The repository is public and meant to last: every core dependency is something contributors must learn and the project must keep up to date.
- Adding a feature should be cheap and must not require editing a central file.

## Decision

Endpoints are written with **native ASP.NET Core Minimal API**, without third-party endpoint libraries:

1. **One endpoint per slice**, defined in the slice file.
2. Each endpoint implements a small project-owned interface (`IEndpoint`) and is **registered automatically at startup**.
3. **One route group per module** sets prefix, authorization and OpenAPI tag once.
4. Handlers return **`TypedResults`**; cross-cutting behavior uses **endpoint filters**.
5. The OpenAPI document comes from the **built-in** ASP.NET Core support.

Details and an illustrative slice: [backend-structure.md](../backend-structure.md#endpoints-minimal-api).

## Consequences

**Positive**
- The endpoint lives inside its slice: one place to read and change a use case.
- No external dependency in the request pipeline; follows the direction ASP.NET Core is investing in.
- New features don't touch `Program.cs`; module groups remove repeated route and authorization setup.
- `TypedResults` give accurate OpenAPI metadata and handlers that can be unit tested as plain methods.

**Negative**
- The registration pattern (`IEndpoint` + discovery) is project code: it must be written, tested and documented.
- Less built-in structure than controllers: consistency relies on the documented pattern and on code review.
- Some MVC features have different or missing equivalents (action filters → endpoint filters; model binding is more limited, e.g. complex objects from query strings need explicit handling).

## Alternatives considered

**A. MVC controllers** — Rejected. Familiar and mature, but a controller groups several actions, so a slice would be split between the controller and its handler, against ADR-0002.

**C. Minimal API through an endpoint library (e.g. FastEndpoints)** — Rejected. Ready-made per-endpoint structure and integrated validation, but a central dependency in a long-lived public project, with its own conventions for contributors to learn; the benefit over a small project-owned pattern is limited.
