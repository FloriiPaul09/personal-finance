# Routing

## Scope

Routes of the application, lazy loading, the shell layout and its navigation, page titles, the login page, the route guards and what happens when the session ends.

## Current state

Implemented in block 1 ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)) in `app.routes.ts`, `core/layout/` and each feature's `<feature>.routes.ts`. Block 3 ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)) added the login page, the settings page, the guards and the handling of an expired session (`core/auth/`); block 5 ([ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md)) the form pages of accounts and categories; block 6 ([ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)) the movement and transfer routes, the list filters in the URL and `PageExit`; block 7 ([ADR-0027](../architecture/adr/0027-frontend-budgets.md)) the budget routes; block 8 ([ADR-0028](../architecture/adr/0028-frontend-dashboard.md)) the month of the dashboard.

### Routes

| Path | Page | Feature |
|---|---|---|
| `/` | Redirect to `/dashboard` | — |
| `/dashboard?month=…` | Home: the month (the current one without `month`, or when it is not valid or in the future) | `dashboard` |
| `/transactions?month=…` or `?from=…&to=…`, plus `kind`, `accountId`, `categoryId`, `q` | Movements of a month (the current one without `month`) or of a custom period, with the filters | `transactions` |
| `/transactions/new`, `/transactions/new?kind=income` | New expense or income (quick entry) | `transactions` |
| `/transactions/:id` | Edit an expense or income; a transfer leg redirects to its transfer | `transactions` |
| `/transfers` | Redirect to `/transactions?kind=transfer` | `transfers` |
| `/transfers/new` | New transfer | `transfers` |
| `/transfers/:id` | Edit a transfer | `transfers` |
| `/budgets?month=…` | Budgets of a month (the current one without `month`) | `budgets` |
| `/budgets/new?month=…` | New budget, from the month the budgets page showed | `budgets` |
| `/budgets/:categoryId?month=…` | Budget of a category, with its history | `budgets` |
| `/accounts` | Accounts with balance | `accounts` |
| `/accounts/new` | New account | `accounts` |
| `/accounts/:id` | Edit an account | `accounts` |
| `/categories?kind=expense\|income` | Categories of a kind (a tab each; expenses without `kind`) | `categories` |
| `/categories/new?kind=…&parent=…` | New category, optionally of a kind and under a parent | `categories` |
| `/categories/:id` | Edit a category | `categories` |
| `/settings` | Account: email, logout, password change | `settings` |
| `/login` | Login (outside the shell, `guestGuard`) | `auth` |
| `/unavailable` | "Il server non risponde" with "Riprova" (outside the shell) | `core/layout` |
| `/dev/components` | Catalog of the shared components, **development only** (not in the navigation; the production build has no `/dev` route) | `dev` |
| `**` | Redirect to `/dashboard` | — |

- **Paths are English** and do not change with the language of the UI: links and bookmarks keep working in every locale.
- Every feature is **loaded on demand** from its barrel: `loadChildren: () => import('./features/accounts').then((m) => m.ACCOUNTS_ROUTES)`.
- Every route above, except redirects, `/login` and `/unavailable`, is a child of the **shell** route (`path: ''`, component `Shell`, `canMatch: [authGuard]`). Pages without the navigation are siblings of the shell.
- Development-only pages come from `dev-routes.ts`, spread into the shell's children; the production build replaces it with `dev-routes.prod.ts` (an empty list), so `/dev/…` falls to the dashboard there.
- `withComponentInputBinding()`: route and query parameters reach the page as inputs (e.g. `id` of `/accounts/:id`, `month` of `/budgets?month=2026-11`).

### Shell and navigation

`core/layout/shell.ts` follows the M3 navigation patterns:

| Width | Navigation | New expense button |
|---|---|---|
| < 600px (phone) | Navigation bar fixed at the bottom, 80px, safe area respected | Floating button above the bar, on the right |
| ≥ 600px | Navigation rail on the left, 80px wide, sticky | Floating button at the top of the rail |

- Five destinations at most (`core/layout/nav-items.ts`): Home, Movimenti, Budget, Conti, Categorie. Transfers are reached from the "Trasferimento" tab of the new expense form and from their legs in the movement list (block 6); `/transfers` has no destination of its own.
- The active item shows the filled icon and the indicator; its link has `aria-current="page"`.
- The switch between bar and rail is CSS only: no breakpoint state in TypeScript.
- The floating button ("Nuova spesa") links to `/transactions/new` from every page: it is the fastest path to the main use case. **On phones it is hidden on form pages** (routes with `data: FORM_PAGE`), where it covered "Salva"; on the rail it stays.
- The **account button** (`account_circle`, "Impostazioni") opens `/settings`: fixed at the top right on phones, at the bottom of the rail from 600px. Settings are not a sixth destination. On phones `.pf-page-title` leaves room for the button, so a long title wraps before it.

### Form pages

Block 5: creating or changing an item opens a page, not a dialog (`/accounts/new`, `/accounts/:id`, …): the phone's back button leaves the form, a form can be linked, and the PWA can reopen it. The page has a back button next to its title, its destination stays current in the navigation, and saving goes back to the list (for categories on the tab of the saved kind). An id that does not exist shows "non trovato" with a link back.

Form routes carry `data: FORM_PAGE` (`core/layout/nav-items.ts`): the shell reads it from the deepest route after each navigation.

**Leaving a form** (block 6, `PageExit` in `core/layout`): after saving or deleting a movement or a transfer, the app goes back to the page the person came from, so a movement added from the dashboard returns to the dashboard. It goes back only when an entry of the app is behind the current one; otherwise (a form opened from a link, a bookmark or a reload) it opens `/transactions`, or for a budget (block 7) `/budgets?month=<the month saved>`. `PageExit` counts those entries from startup (`providePageExit()` in `app.config`): a navigation adds one, `replaceUrl` and `skipLocationChange` add none, back and forward restore the count kept for the entry they reach (by the `navigationId` Angular writes in `history.state`). A previous navigation is not enough: the kind tabs replace the entry (`/transactions/new` → `/transfers/new`), so a form opened as the first page has a navigation before it but nothing of the app behind.

**The kind tabs** ("Uscita / Entrata / Trasferimento") navigate with `replaceUrl`: back leaves the form instead of walking through the tabs.

### Movement list in the URL

Block 6: the list reads its period and filters from the query string (inputs through `withComponentInputBinding()`), and every change writes them back with `queryParamsHandling: 'merge'` and `replaceUrl`, so back leaves the list instead of undoing filters one by one. A valid `from`–`to` pair is a custom period, otherwise `month` (the current month when missing); values that are not valid are ignored, and `categoryId` is dropped with `kind=transfer`. A reload, a bookmark or a link from the dashboard (block 8) open the same view.

Block 8: the dashboard keeps its month in the query string the same way (`replaceUrl`), never in the future: a later month in the URL shows the current one. Its rows open `/transactions?categoryId=…&month=…` (a category), `/transactions?accountId=…&month=…` (an account), `/transactions?month=…` (the totals) and `/budgets?month=…`, so back returns to the same month of the dashboard.

### Page titles

Each route has a `title` (translated with `$localize`). `PageTitleStrategy` sets the browser title to `"<title> · Personal Finance"`, or `"Personal Finance"` when a route has none.

### Authentication

Block 3, [ADR-0023](../architecture/adr/0023-frontend-authentication.md), code in `core/auth/`:

| Situation | What happens |
|---|---|
| A protected page is opened | `authGuard` (`canMatch` on the shell) asks `SessionStore.ensureLoaded()`: one `GET /api/auth/me` per page load, shared by concurrent calls |
| No session (`me` answers `401`) | `/login?returnUrl=<page>`; after the login the user lands on that page |
| The API cannot be reached | `/unavailable?returnUrl=<page>`: message and "Riprova", which navigates to the page again (and asks `me` again) |
| A signed-in user opens `/login` | `guestGuard` sends them to `returnUrl`, or to the dashboard |
| A request answers `401 auth.required` (expired session, password changed elsewhere) | `sessionInterceptor` marks the session signed out and opens `/login?returnUrl=<current page>`; the error reaches the caller, the global handler stays silent. `/api/auth/me` and `/api/auth/login` are excluded: their `401` is an answer, not an expired session |
| "Esci" in the settings | `POST /api/auth/logout`, then a **full page load** of `/login`, which drops every store |

- `safeReturnUrl()` accepts only paths of the app: absolute URLs, `//host`, backslashes and the login page itself become `/dashboard` (no open redirect).
- The login keeps the user signed in by default ("Resta connesso" checked: 14-day sliding cookie, [backend/security.md](../backend/security.md)).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| English paths, one lazy route file per feature | Stable URLs in every language; small initial bundle | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Navigation bar on phones, rail from 600px, floating new expense button | One-handed use; adding an expense is one tap from every page | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Shell as a parent route | Pages without navigation (login) need no condition in the layout | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Unknown paths redirect to the dashboard | No dead end for a mistyped or old link | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Session asked on the first protected page, `canMatch` guards, `/unavailable` when the API is down | Startup never waits for the network; lazy code is not loaded for a signed-out user | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |
| One interceptor for an expired session, back to the page after the login | Pages handle only their own errors | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |
| Logout with a full page load | No data of a user stays in memory | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |
| Settings behind an account button, not a destination | Five destinations at most on phones | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |
| Forms as pages with their own address; category kind in the query string | Back button and links work; the list reopens on the right tab | [ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md) |
| Transfers as their own routes; kind tabs with `replaceUrl`; list filters in the URL; FAB hidden on phone form pages; `PageExit` back only to a page of the app | Saved and shared views; back behaves as expected; "Salva" never covered | [ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md) |
| Budgets: month in the URL (`replaceUrl`), form pages by category id, a row linking to `/transactions?categoryId=…&month=…` | The month is always the one sent to the API; budget and expenses one tap apart | [ADR-0027](../architecture/adr/0027-frontend-budgets.md) |
| Dashboard: month in the URL, never in the future; every number links to the movements of that month | Bookmarks and back keep the month; each figure can be checked | [ADR-0028](../architecture/adr/0028-frontend-dashboard.md) |

## Tests

`app.spec.ts` runs the real routes with `RouterTestingHarness`:

- `/` opens the dashboard;
- the navigation shows the five destinations in Italian, with their links;
- each destination loads its feature lazily, has `aria-current="page"` on its link only and sets the browser title;
- the new expense button is on every page, has its label and opens the form;
- an unknown path goes to the dashboard;
- the form pages of accounts, categories and budgets (`/accounts/new`, `/categories/new`, `/budgets/new`) load with their title and keep their destination current (blocks 5 and 7);
- the new expense button gets the `form-page` class on form pages (block 6);
- the account button opens the settings; without a session a page opens the login, with the API down the "unavailable" page (block 3).

`core/layout/page-title-strategy.spec.ts`: title with and without a page title. Block 6: `core/layout/page-exit.spec.ts` (back from a page of the app, fallback for a first page and for a replaced first page, back past a replaced form, counts kept through back), `movement-query.spec.ts` (URL to filters), the list page writing filters to the URL. Block 3: `core/auth/*.spec.ts` (return URL, session store, guards, interceptor), `core/layout/unavailable-page.spec.ts`.

End-to-end ([testing.md](testing.md#end-to-end-playwright)): protected page → login → back to the page; signed-in user sent away from the login; logout; expired session → login with `returnUrl`; accounts and categories from list to form and back (block 5); expense from the FAB and back, transfer from the tab ending on `/transactions`, a text filter in the URL, the FAB hidden on phone form pages (block 6); a budget from the page to its form and back to the month, its row opening the movements of the category (block 7); the dashboard's category row opening its movements, then another month in the URL (block 8).

The layout (bar vs rail) is checked in the browser: block 1 was verified with screenshots at 390×844 (light and dark) and 1280×720, and by measuring that the page is never taller than the window from 390px to 3840px (a 16px overflow on the rail was found and fixed in review). Since block 3 Playwright repeats the check on every page on a phone (Pixel 7, 412×839) and a 1280×720 desktop, together with reachable navigation buttons and a page title clear of the account button ([testing.md](testing.md)).

## Scalability

- **New feature**: one entry in `app.routes.ts`; a sixth destination does not fit the phone bar, so it goes in a page of an existing destination or in a "more" destination.
- **Deep links** (`/transactions/:id`, filters in the query string) work with input binding and survive reloads.
- **PWA**: the service worker serves `index.html` for every path; routes need no change.
- **Multi-user / roles**: guards per route (`canMatch`) when roles exist; `SessionStore.user` is where roles would be read.
- **PWA offline**: `SessionStore` can answer from the last known user instead of `unavailable`, so the app opens offline.

## To do / open questions

- ✅ Block 3: login page, guards, expired session, logout, settings; end-to-end tests ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)).
- ✅ Block 6: access to transfers (from the new expense form and from the movement list); filters in the query string for the movement list ([ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)).
- ✅ Block 7: `/budgets?month=…`, budget form pages ([ADR-0027](../architecture/adr/0027-frontend-budgets.md)).
- ✅ Block 8: `/dashboard?month=…` ([ADR-0028](../architecture/adr/0028-frontend-dashboard.md)).

---
_Last updated: 2026-11-17_
