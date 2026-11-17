# ADR-0026: Frontend transactions and transfers — quick entry, movement list, filters, undo

- **Status**: Accepted
- **Date**: 2026-11-17
- **Deciders**: Paul

## Context

- The first goal of the app is entering an expense in a few seconds, often from a phone ([ADR-0021](0021-frontend-foundation.md)). The "Nuova spesa" button (FAB on phones, in the rail on desktop) already opened `/transactions/new` as a placeholder.
- The API takes a kind and a positive amount; it accepts an id chosen by the client (UUIDv7) and creates nothing new for a repeated request. It lists movements newest first with keyset paging (`nextCursor`, 50 per page, no total) and one value per filter: `from`/`to`, `accountId`, `categoryId` (a top-level category includes its subcategories), `kind`, `tag`, `q`. Tags travel by name in the transaction, at most 20; `/api/tags` lists them with their usage count ([ADR-0017](0017-transactions-and-tags.md)).
- A transfer is its own resource with two legs, which appear in the movement list as transactions of kind `transfer` carrying the `transferId`; the two accounts must differ and share the currency ([ADR-0018](0018-transfers-module.md)).
- Accounts and categories come from the directories of `core/data`, and writes are announced with `DataChanges` ([ADR-0025](0025-frontend-accounts-and-categories.md)). Features cannot import each other.
- The PWA (later) will reopen pages from shortcuts and links, and may queue writes while offline.
- The initial bundle budget is 500 kB (warning) and 600 kB (error) ([ADR-0024](0024-frontend-shared-components.md)).

## Decision

1. **Quick entry as one short page**: the amount comes first and has the focus, then the category, the account (the last one used) and the date (today); description and tags stay behind a "Descrizione e tag" button (shown open when the movement already has them). The id is chosen when the form opens, so a save sent twice creates one movement.
2. **Recent categories as chips**: the 6 categories used last for that kind sit above the full selector, one tap away. `RecentChoices` (`core/data`) keeps them, with the last account, in the browser storage per user (`pf:<userId>:recent`): a convenience of the device, not data, and nothing breaks without storage.
3. **One form per kind behind tabs** "Uscita / Entrata / Trasferimento" (`<app-movement-kind-switch>`, `core/selectors`), shown only on a new movement. Expenses and incomes share `/transactions/new` (`?kind=income`); transfers are their own feature, `features/transfers` (`/transfers/new`, `/transfers/:id`), because they have their own resource and fields. Switching tab replaces the history entry, so back leaves the form instead of walking through its tabs.
4. **The movement list grouped by day** ("Oggi", "Ieri", "Domenica 15 novembre"), newest first, loading more as it scrolls (`cursorPager`, an `IntersectionObserver` and a "Carica altri movimenti" button), with no totals (they belong to the dashboard). A transfer leg reads "Trasferimento in uscita/entrata" and opens its transfer.
5. **The period and the filters live in the URL**: a month selector, and a "Filtri" panel with kind, account (archived ones too), category, text and a custom period (`from`/`to`). Active filters show as chips with a remove button. An invalid value is ignored, so an old or edited link still opens a list.
6. **Delete from the edit page, after a confirmation** (for a transfer: "Spariscono entrambi i movimenti…"); no delete on the list row.
7. **After saving, back to where the person came from**, with a snackbar "Spesa salvata." and "Annulla", which deletes what was just created. "Salva e aggiungi un'altra" keeps the account and the date and puts the focus back on the amount. `PageExit` (`core/layout`) goes back only when an entry of the app is behind the current one (it counts them from startup: a navigation adds one, `replaceUrl` adds none, back and forward restore the count of their entry); otherwise it opens the list. The kind tabs replace the first entry, so "a previous navigation" is not enough.
8. **Tags as a chip input with suggestions** (`<app-tags-input>`): the tags in use, most used first; Enter or a comma adds one. The comma is also split from the text, because phone keyboards do not send it as a key.
9. **No FAB on form pages on phones**: routes of forms carry `data: FORM_PAGE`, and the shell hides the button there, where it covered "Salva".
10. **A `412`** shows the current data with a message, as in [ADR-0025](0025-frontend-accounts-and-categories.md); errors of the API land on their field.

## Consequences

**Positive**
- An expense from a phone is: FAB, amount, a chip, "Salva" — the account and the date are already right in most cases.
- A mistake right after saving costs one tap ("Annulla"), and nothing irreversible happens without a question.
- The list and its filters have an address: back, reload and saved links keep the view; the dashboard (block 8) can link to a filtered list.
- Repeated saves are harmless, which the offline queue of the PWA will need.
- Transfers keep their own rules and screen while sharing the entry point with expenses.

**Negative**
- Recent choices stay on the device: a second phone starts from nothing.
- The kind tabs reach a different feature for transfers, so the switch lives in `core/selectors`.
- `PageExit` must exist from the first navigation (`providePageExit()`); after a reload it knows nothing of the entries before it and opens the list.
- The initial bundle grew from 479 to 491.5 kB: code that lazy pages share with the startup code (UUIDs, `cursorPager`, `RecentChoices`, `PageExit`) loads at startup.

## Alternatives considered

**A dialog or bottom sheet for the quick entry** — Rejected: no address, back closes the app on phones, and the PWA cannot reopen it.

**A wizard (amount, then category, then account)** — Rejected: more taps for the common case, where only the amount and the category change.

**Recent categories kept by the API** — Rejected for now: a new endpoint and table for a convenience. Reconsidered with more devices per person.

**Transfers as a third kind of the same form** — Rejected: other fields (two accounts), other rules (same currency), another resource.

**Numbered pages, or totals on the list** — Rejected: the API pages by cursor and has no count; totals are the dashboard's job.

**Filters kept in a store** — Rejected: lost on reload, and a filtered list could not be linked.

**A swipe or a menu to delete on the row** — Rejected: easy to trigger by mistake while scrolling on a phone; deleting is rare.

**A plain text field for tags** — Rejected: no suggestions, so the same tag gets typed in many ways.

**Going back whenever there was a previous navigation** — Rejected after the E2E test of transfers: a form opened from a bookmark and switched to another kind went back to the page before the app.
