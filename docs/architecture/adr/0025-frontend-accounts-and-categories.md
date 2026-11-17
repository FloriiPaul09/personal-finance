# ADR-0025: Frontend accounts and categories — reference data, selectors, archive and delete

- **Status**: Accepted
- **Date**: 2026-11-17
- **Deciders**: Paul

## Context

- The API lists accounts and categories without paging, archived ones on request (`includeArchived=true`), and tells in advance what can be done with each: `hasMovements` (an account can only be archived, its currency is fixed), `isInUse` and `hasSubcategories` (a category can only be archived, its kind is fixed, it stays top-level) ([ADR-0015](0015-resource-conventions-and-accounts.md), [ADR-0016](0016-categories-module.md)).
- Accounts and categories are read by the accounts and categories pages and by the account and category selectors of movements (block 6), budgets (block 7) and the dashboard (block 8). Features cannot import each other ([ADR-0021](0021-frontend-foundation.md)).
- A balance and `hasMovements` change with every movement; `isInUse` changes with movements and budgets. The state document planned a `core` service that publishes data changes by resource, to be built with the first write.
- Archiving can be undone (`unarchive`); deleting cannot. Archiving a top-level category archives its subcategories, while `unarchive` restores only the category it is called on.
- Writes send `If-Match`; a `412` means the item changed elsewhere ([ADR-0022](0022-frontend-api-integration.md)).
- About 20 categories exist by default ([default-data.md](../../database/default-data.md)); the roadmap adds a PWA, more currencies, CSV import and more users.
- The initial bundle budget is 500 kB (warning) and 600 kB (error) ([ADR-0024](0024-frontend-shared-components.md)).

## Decision

1. **Forms as pages with their own address**: `/accounts/new`, `/accounts/:id`, `/categories/new?kind=…&parent=…`, `/categories/:id`, with a back button next to the title. The list row opens the form; a menu button on the row holds the other actions.
2. **One source of reference data in `core`** (`core/data`): `AccountsDirectory` and `CategoriesDirectory` read the whole list once, archived items included, for every page and selector. `DataChanges` counts writes by resource: a store calls `notify('accounts')` after each write, failed ones included (a `412` means newer data); a reader calls `version('accounts', 'transactions', 'transfers')` inside the request of its `httpResource`, listing what its data is computed from. The last list stays on screen while it is read again, so a list does not flash after an archive. Feature stores (`AccountsStore`, `CategoriesStore`) keep the writes and read through the directories.
3. **Archive and delete as two actions**: "Archivia" never asks and the snackbar offers "Annulla" (for a top-level category it restores the subcategories it archived, one by one); "Elimina" asks in a dialog and is offered only when the API allows it, otherwise it is disabled with the reason ("Ha dei movimenti: puoi archiviarlo", "È usata: puoi archiviarla", "Ha delle sottocategorie"). "Ripristina" of a subcategory waits for its parent. Archived items are hidden behind a "Mostra archiviati" toggle.
4. **Categories page with a tab per kind** ("Uscite", "Entrate", in the query string), each top-level category followed by its subcategories indented, as the API orders them and as the selector shows them. The form does not offer what the API would refuse: the kind is fixed while the category is used or has subcategories, a category with subcategories stays top-level, the parent is a top-level category of the same kind (choosing another kind makes it top-level).
5. **Selectors as Material selects** (`core/selectors`, its own barrel): `<app-account-select>` (active accounts with their balance) and `<app-category-select>` (active categories of a kind, parents and indented subcategories, all selectable; `topLevelOnly`, `excludeId`, `noneLabel`), both Signal Forms controls holding the id. An archived item appears only when it is the current value, so editing an old movement does not lose it. No search field.
6. **Currency** as a text field with suggestions from `Intl.supportedValuesOf('currency')`, named in Italian ("CHF franco svizzero"), common currencies first, EUR by default; three letters, stored upper case; locked with the reason when the account has movements.
7. **A `412`** reads the list again and the form shows the current data with a message, so the person decides again. **The first field with an error gets the focus** when a form is submitted (`focusFirstError()` in `core/forms`).
8. **Nothing of Material's overlays at startup**: messages go through `Notifier` (`core/feedback`), which loads the snackbar with the first message and gives it the `pf-snackbar` class, and `<app-date-input>` provides the date adapter itself. While the snackbar and the date adapter were provided at startup, the code of menus, selects and dialogs that the new pages share with them moved into the initial bundle (571 kB); without them it is 479 kB, less than after block 4.

## Consequences

**Positive**
- One request per list for the whole app; a write anywhere refreshes balances, selectors and, later, the dashboard without features knowing each other.
- What cannot be done is never offered, and the person sees why; the only irreversible action asks first.
- Forms have an address: the phone's back button works, links can be saved, the PWA can reopen a form.
- Selectors are ready for blocks 6–8 and were tried with real data on `/dev/components`.
- The initial bundle stays small as later blocks add dialogs and menus.

**Negative**
- Every reader must list the resources its data depends on; a missing one shows stale data until the next write.
- Accounts and categories are read whole: fine for tens of items, not for thousands.
- No search in the selectors: with many categories, scrolling is slower.
- Pages call `Notifier` instead of `MatSnackBar`, and every date field must be `<app-date-input>`.

## Alternatives considered

**Forms in a dialog, or edited inline in the row** — Rejected: a dialog has no address and the phone's back button closes the app instead of the dialog; four fields do not fit a row on a phone.

**A store per feature plus a copy in `core` for the selectors** — Rejected: the same data downloaded twice, and the two copies can disagree.

**One "Rimuovi" that archives or deletes depending on the item** — Rejected: one word for two different effects, and deleting would be a surprise. **A confirmation for every action** — Rejected: slow for something that "Annulla" undoes.

**A page per top-level category with its subcategories** — Rejected: one more step for every change.

**Autocomplete selectors with search** — Rejected for now: on a phone they open the keyboard, slower for a few tens of items. Reconsidered if categories grow.

**A fixed list of currencies, or free text** — Rejected: a list to keep up by hand, or typing errors found only by the API.

**Raising the bundle budget** — Rejected: +92 kB at startup (+19 kB compressed), growing with every page that uses menus or dialogs.
