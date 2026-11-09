# ADR-0006: Risk-focused testing strategy

- **Status**: Accepted
- **Date**: 2026-11-09
- **Deciders**: Paul

## Context

- The most serious failures for a personal finance app are wrong numbers (balances, transfers, budgets) and data visible to the wrong user, more than visual bugs.
- Several key mechanisms only work against a real database: EF Core global query filters per user ([ADR-0001](0001-authentication.md)), migrations, PostgreSQL-specific behavior.
- The project is developed by one person but published for contributors: tests must be sustainable today and give confidence to reviewers later.

## Decision

1. **Domain unit tests** (xUnit) for every business rule.
2. **API integration tests** with `WebApplicationFactory` and **Testcontainers on a real PostgreSQL**. Every endpoint covers at least: success, `401` without a session, validation error when it accepts input, and isolation between users.
3. **Architecture tests** enforcing the project dependencies of [ADR-0002](0002-backend-structure.md).
4. **Frontend unit tests** with Vitest on shared components and logic (amount input, date adapter).
5. **A few end-to-end tests** with Playwright on critical flows (login, adding an expense, transfer, dashboard).
6. **Coverage measured without a mandatory threshold**. All tests run in CI on GitHub Actions; pull requests are merged only when green.

Details: [testing-strategy.md](../testing-strategy.md).

## Consequences

**Positive**
- Tests concentrate on the failures that matter: money correctness, data isolation, authentication.
- Integration tests exercise the real database, migrations and query filters: no false confidence from an in-memory provider.
- Architecture rules are checked automatically, not only by convention.
- Sustainable effort for a single developer.

**Negative**
- Integration tests need Docker and are slower than unit tests.
- Without a threshold, coverage depends on discipline and on the "minimum required" checklist.
- End-to-end tests need the full stack running and are the most fragile, which is why they are kept few.

## Alternatives considered

**A. Unit tests only** — Rejected. Fast, but leaves untested exactly what is riskiest: queries, user filters, migrations and authentication.

**C. Full suite with a coverage threshold (e.g. 80%) enforced in CI** — Rejected for now. Strongest guarantee and a clear signal for contributors, but it slows a single developer considerably and thresholds encourage tests written for the number. To be reconsidered when there are regular contributors.

**In-memory database instead of Testcontainers** — Rejected. Faster, but it does not behave like PostgreSQL and would not reliably test query filters and migrations.
