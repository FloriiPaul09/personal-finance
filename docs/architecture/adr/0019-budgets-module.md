# ADR-0019: The budgets module

- **Status**: Accepted
- **Date**: 2026-11-12
- **Deciders**: Paul

## Context

- A budget is a history of entries per expense category ([ADR-0010](0010-data-model-and-user-isolation.md), [database/domain-model.md](../../database/domain-model.md#budget-and-progress)): "300 € from January", "350 € from June", "no budget from September" (no amount). One entry per category and month is enforced by a unique index (`budget.month_taken`).
- How progress is computed:
  - the spending counted includes subcategories;
  - a budget sits on a parent or on its subcategories, never on both (`BudgetPolicy`);
  - `BudgetProgress` computes limit, spent, remaining, ratio and over budget for a month.
- Every `PUT` / `PATCH` / `DELETE` requires `If-Match`, checked by a test over the whole API ([ADR-0013](0013-error-responses-and-concurrency.md)). Lists use `{ "items": [...] }` ([ADR-0015](0015-resource-conventions-and-accounts.md)).
- Categories that are archived keep their budgets but cannot be chosen for new ones ([ADR-0016](0016-categories-module.md)).
- Roadmap:
  - the dashboard (block 9) shows the budgets of the month;
  - charts over time will need progress of several months;
  - the PWA;
  - multi-user.

## Decision

1. **Entries are a resource** at `/api/budgets`: create `{ categoryId, from, amount }`, change the amount with `PUT` and `If-Match`, delete, list (with `?categoryId=`) and read one. Category and month of an entry never change. There is no client id: the unique month already prevents duplicates (`409` `budget.month_taken`).
2. **Months as `"yyyy-MM"`** in JSON (`YearMonthJsonConverter`) and in the query (`QueryValues.ParseMonth`). Anything else is `400` `request.invalid`.
3. **`GET /api/budgets/progress?month=yyyy-MM`** returns, for each category with a budget that month: `limit`, `spent` (subcategories included), `remaining`, `ratio`, `isOverBudget`.
   - Limits and spending come from one SQL query (`ProgressOf`), and the dashboard will reuse it.
   - Remaining, ratio and over-budget come from the domain record `BudgetProgress`, so they are never computed twice.
   - Without `month`, the server's current month (UTC) is used.
4. **Two ways to end a budget**:
   - an entry without amount stops the budget from a month and keeps its history;
   - `DELETE` removes a wrong entry. A category without entries is unbudgeted again, which is how a budget moves between a parent and its subcategories.
5. **No paging for entries**: a category gets a few entries a year at most.

New domain rule: an entry **with an amount** on an archived category is refused (`409` `category.archived`); ending its budget stays possible.

Details: [backend/api-endpoints.md](../../backend/api-endpoints.md#budgets).

## Consequences

**Positive**
- Same rules as every other resource: ids, versions, `If-Match`, `{ items }`.
- The progress in the API equals the domain calculation; an API test compares them, as for account balances.
- A month can never arrive with a day other than the first.
- History is kept when a budget stops, so past months still show their limit; corrections remain possible.
- Charts over time can call the progress query for each month, or a range endpoint can reuse `ProgressOf`, without changing the response.

**Negative**
- To change the budget from a month, the client has to know whether that month already has an entry (create, or change it).
- The interface must explain the difference between "stop from this month" and "delete".
- `PUT` with `amount` omitted ends the budget, like `null`: a form must always send the amount.
- The default month is the server's month in UTC: near midnight on the last day of a month, the app should send its own month.
- The OpenAPI document does not describe the `"yyyy-MM"` format of months yet (a schema transformer can add it).

## Alternatives considered

**`PUT /api/categories/{id}/budget/{month}` that creates or replaces** — Rejected. One gesture, idempotent, but a `PUT` that creates without `If-Match` breaks the rule checked over the whole API.

**Dates `"2026-06-01"`** — Rejected. No new format, but one more possible error (a day other than the first) and the client always appends `-01`.

**Progress only in the dashboard** — Rejected. The budget screen would load the whole dashboard.

**Only `DELETE` to stop a budget** — Rejected. Simpler, but stopping a budget would erase its history, and the progress of past months could not be rebuilt.

**Cursor paging for entries** — Rejected for now. Complexity without need; it can be added without breaking clients.
