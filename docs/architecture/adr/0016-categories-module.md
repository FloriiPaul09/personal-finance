# ADR-0016: The categories module

- **Status**: Accepted
- **Date**: 2026-11-12
- **Deciders**: Paul

## Context

- The conventions of every resource are in place ([ADR-0015](0015-resource-conventions-and-accounts.md)): enums as strings, lists in `{ "items": [...] }`, one response type per resource, `PUT` with `If-Match`, idempotent `archive` / `unarchive` actions, delete only when nothing would be lost.
- Category rules in `Domain` ([database/domain-model.md](../../database/domain-model.md#categories)):
  - one level of subcategories;
  - a subcategory has the kind (income or expense) of its parent;
  - the kind is fixed once transactions or budgets use the category, or once it has subcategories;
  - archiving a parent archives its subcategories;
  - a budget sits on a parent or on its subcategories, never on both, so moves are checked against budgets.
- Every new user receives default categories ([database/default-data.md](../../database/default-data.md)). The transaction form picks a category of one kind, grouped by parent; budgets and reports group spending by category.
- Roadmap: CSV import (rows matched to categories by name), charts per category, PWA for quick entry, multi-user.

## Decision

1. **Flat list with `parentId`**: every top-level category is followed by its subcategories. Expense categories come first, then income, by name within each level. The order is applied in memory: a user has tens of categories, and the order needs each parent's name.
2. **Filters** `?kind=expense|income` (optional) and `?includeArchived=true` (archived categories shown in their place). Query enums follow the JSON rules: `camelCase` names in any case; numbers and unknown names give `400` `request.invalid` on the parameter (`QueryValues.ParseEnum`).
3. **`PUT` of the whole category** (`name`, `kind`, `parentId`) with `If-Match`. Renaming, moving and changing the kind go through one domain method, `Category.Change`; `BudgetPolicy.EnsureCanMove` runs before a move. `kind` is required for subcategories too and must match the parent. An archived parent is refused only when newly chosen, so an archived subcategory can still be renamed.
4. **`unarchive` restores only that category**: subcategories archived with their parent stay archived until restored one by one. A subcategory of an archived parent gives `409` `category.archived` (`Category.Unarchive`).
5. **`DELETE` only for an unused category without subcategories**: `409` `category.in_use` (transactions or budgets) or `category.has_subcategories` otherwise (`Category.EnsureCanDelete`). Every response has `isInUse` and `hasSubcategories`, so the app knows in advance whether the kind is locked and whether to offer delete or archive.
6. **Alphabetical order only**. A user-defined order can be added later as a new field without breaking clients.

Details: [backend/api-endpoints.md](../../backend/api-endpoints.md#categories).

## Consequences

**Positive**
- One response type for list, read and writes; every item has its version and can be edited from the list.
- The transaction form asks for `?kind=expense` and builds its grouped menu from consecutive items.
- One domain method decides every combination of rename, move and kind change, in the right order; the `Worker` and the CSV import reuse it.
- History and budgets never lose their category; categories created by mistake can still be deleted.
- No migration.

**Negative**
- The client groups the flat list itself (consecutive items with the same `parentId`).
- Restoring a whole branch needs one call per category.
- Two `EXISTS` subqueries per category on every read (`isInUse`, `hasSubcategories`): cheap at tens of categories, to watch if users ever keep thousands.
- A transaction or budget saved between the usage check and the delete fails on the foreign key and gives `500` (the same rare race as accounts, [ADR-0015](0015-resource-conventions-and-accounts.md)).

## Alternatives considered

**Tree (`subcategories` nested in their parent)** — Rejected. Ready for the menu, but a second response type for the list, and nested items are harder to update.

**No filters** — Rejected. Every screen would filter by kind and archived state itself.

**Rename in `PUT`, `move` and `change-kind` as actions** — Rejected. One route per rule, but three versions to manage, inconsistent with accounts, and moving to another kind needs both actions in a precise order.

**`unarchive` of a parent restores every subcategory** — Rejected. One click, but it also restores subcategories the user had archived on purpose.

**Cascade delete of unused subcategories; no delete at all** — Rejected. The first deletes more than the user selected; the second keeps categories created by mistake forever.

**User-defined order (`position` column)** — Rejected for now. Most-used categories on top, but a migration, a reorder endpoint and position bookkeeping before anyone asked for it. Ordering by usage is another option for the PWA later.
