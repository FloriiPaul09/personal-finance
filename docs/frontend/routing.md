# Routing

## Scope

Routes of the application, lazy loading, the shell layout and its navigation, page titles, and (from block 3) the login page, the route guard and the redirect on `401`.

## Current state

Implemented in block 1 ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)) in `app.routes.ts`, `core/layout/` and each feature's `<feature>.routes.ts`. Login, guard and `401` handling arrive with block 3.

### Routes

| Path | Page | Feature |
|---|---|---|
| `/` | Redirect to `/dashboard` | — |
| `/dashboard` | Home | `dashboard` |
| `/transactions` | Movements | `transactions` |
| `/transactions/new` | New expense (quick entry) | `transactions` |
| `/budgets` | Budgets | `budgets` |
| `/accounts` | Accounts | `accounts` |
| `/categories` | Categories | `categories` |
| `**` | Redirect to `/dashboard` | — |

- **Paths are English** and do not change with the language of the UI: links and bookmarks keep working in every locale.
- Every feature is **loaded on demand** from its barrel: `loadChildren: () => import('./features/accounts').then((m) => m.ACCOUNTS_ROUTES)`.
- Every route above, except redirects, is a child of the **shell** route (`path: ''`, component `Shell`). Pages without the navigation (login, block 3) are siblings of the shell.
- `withComponentInputBinding()`: route and query parameters reach the page as inputs (e.g. `id` of `/accounts/:id`, `month` of `/budgets?month=2026-11`).

### Shell and navigation

`core/layout/shell.ts` follows the M3 navigation patterns:

| Width | Navigation | New expense button |
|---|---|---|
| < 600px (phone) | Navigation bar fixed at the bottom, 80px, safe area respected | Floating button above the bar, on the right |
| ≥ 600px | Navigation rail on the left, 80px wide, sticky | Floating button at the top of the rail |

- Five destinations at most (`core/layout/nav-items.ts`): Home, Movimenti, Budget, Conti, Categorie. Transfers are reached from the movements and from the new expense form (block 6).
- The active item shows the filled icon and the indicator; its link has `aria-current="page"`.
- The switch between bar and rail is CSS only: no breakpoint state in TypeScript.
- The floating button ("Nuova spesa") links to `/transactions/new` from every page: it is the fastest path to the main use case.

### Page titles

Each route has a `title` (translated with `$localize`). `PageTitleStrategy` sets the browser title to `"<title> · Personal Finance"`, or `"Personal Finance"` when a route has none.

### Authentication (block 3)

Planned, to be confirmed with block 3:

- `/login` outside the shell; the shell route protected by a guard that asks the session state (`GET /api/auth/me` at startup, [backend/security.md](../backend/security.md)).
- An HTTP interceptor sends the user to `/login?returnUrl=…` on any `401`.

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| English paths, one lazy route file per feature | Stable URLs in every language; small initial bundle | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Navigation bar on phones, rail from 600px, floating new expense button | One-handed use; adding an expense is one tap from every page | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Shell as a parent route | Pages without navigation (login) need no condition in the layout | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Unknown paths redirect to the dashboard | No dead end for a mistyped or old link | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |

## Tests

`app.spec.ts` runs the real routes with `RouterTestingHarness`:

- `/` opens the dashboard;
- the navigation shows the five destinations in Italian, with their links;
- each destination loads its feature lazily, has `aria-current="page"` on its link only and sets the browser title;
- the new expense button is on every page, has its label and opens the form;
- an unknown path goes to the dashboard.

`core/layout/page-title-strategy.spec.ts`: title with and without a page title.

The layout (bar vs rail) is checked in the browser: block 1 was verified with screenshots at 390×844 (light and dark) and 1280×720; the end-to-end tests of block 3 run on a phone viewport ([testing.md](testing.md)).

## Scalability

- **New feature**: one entry in `app.routes.ts`; a sixth destination does not fit the phone bar, so it goes in a page of an existing destination or in a "more" destination.
- **Deep links** (`/transactions/:id`, filters in the query string) work with input binding and survive reloads.
- **PWA**: the service worker serves `index.html` for every path; routes need no change.
- **Multi-user / roles**: guards per route (`canMatch`) when roles exist.

## To do / open questions

- Block 3: login page, guard, `401` interceptor, logout; end-to-end login test.
- Block 6: access to transfers (from the new expense form and from the movement list).
- Filters in the query string for the movement list (block 6), so a filtered list can be bookmarked.

---
_Last updated: 2026-11-13_
