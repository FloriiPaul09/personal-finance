# ADR-0020: The dashboard module

- **Status**: Accepted
- **Date**: 2026-11-12
- **Deciders**: Paul

## Context

- The MVP home page shows:
  - the balance per account;
  - income and expenses of the month;
  - spending per category;
  - the budgets of the month.
- The data is already available:
  - account balances are computed in SQL ([ADR-0015](0015-resource-conventions-and-accounts.md));
  - budget progress comes from one query with the domain's arithmetic ([ADR-0019](0019-budgets-module.md));
  - transactions carry a kind, and transfers are legs of kind `transfer` ([ADR-0017](0017-transactions-and-tags.md)).
- Accounts can have different currencies; transfers between currencies are not supported yet. The domain rules say that an archived account keeps its balance and appears in reports ([database/domain-model.md](../../database/domain-model.md#account-balance)).
- The home page is opened from the phone (PWA), often on a slow network. Charts over time are on the roadmap.

## Decision

1. **One endpoint, `GET /api/dashboard?month=yyyy-MM`**, with every section in one response. Each section comes from its own query:
   - `AccountQueries.SelectResponses`;
   - `TotalsOfAsync` and `SpendingOfAsync`, which take a date range, not a month, so charts over several months can reuse them;
   - `ProgressOf`.

   Without `month`, the server's current month in UTC is used, as for budget progress.
2. **Every sum per currency**: total balances, income and expenses, spending per category. Amounts of different currencies are never added together.
3. **Spending per top-level category**: expenses of subcategories add up into their parent. There is one item per category and currency, largest first. The detail comes from the transaction list filtered on that category.
4. **Archived accounts**: they are left out of the account list but included in the total balance of their currency, which also states how much of it sits on archived accounts (`archivedBalance`).

Transfers are never income or expenses. The section is read-only: no versions, no `If-Match`.

Details: [backend/api-endpoints.md](../../backend/api-endpoints.md#dashboard).

## Consequences

**Positive**
- The home page opens with one request; the queries use the existing indexes.
- Charts can become endpoints of their own (e.g. spending per category over a range of months) by calling the same queries.
- Numbers are right with accounts in several currencies; with only euros, every per-currency list has one item.
- Net worth stays right when an archived account still holds money.
- No migration, no new domain rule: balances and budget arithmetic come from the code already tested against the domain.

**Negative**
- Every opening recomputes every section (five small queries). If it ever matters, sections can be cached per user and month, or split into their own endpoints.
- The account list does not add up to the total when archived accounts hold money; `archivedBalance` explains the difference.
- Account balances are current balances (all movements, future-dated ones included), not the balance at the end of the selected month.
- Budgets do not distinguish currencies: an expense on a budgeted category counts at its face value whatever the account's currency (an MVP limitation of the budget model, [ADR-0019](0019-budgets-module.md)).

## Alternatives considered

**One endpoint per section** — Rejected for now. Each widget would load and refresh on its own, but opening the home page from the phone would take four requests. The queries are already separate, so splitting later is cheap.

**One total across currencies** — Rejected. Simpler, but wrong as soon as an account in another currency exists.

**Every category separately, or parents with nested subcategories** — Rejected. The first crowds the chart and pushes grouping to the client; the second is a more complex response for a need not yet there. A nested detail can be added as an optional field later.

**Archived accounts left out of the totals too** — Rejected. The rows would add up exactly, but net worth would be wrong when an archived account still holds money.
