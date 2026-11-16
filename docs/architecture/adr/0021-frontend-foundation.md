# ADR-0021: Frontend foundation — theme, structure, navigation, state, API client and i18n

- **Status**: Accepted
- **Date**: 2026-11-13
- **Deciders**: Paul

## Context

- Every MVP endpoint exists (ADR-0012–0020). The Angular 22 workspace has pnpm, Vitest and the `/api` proxy, but no UI library, no lint and no page yet.
- The main use case is **adding an expense in a few seconds from a phone**; the PWA with an offline queue is on the roadmap (phase 3).
- Angular 22 made the signal APIs the default path: Signal Forms, `resource()` / `httpResource()` and the `@Service()` decorator are stable, components are `OnPush` by default and the app is zoneless.
- The backend contract has things a generic client does not know: versions in `ETag` / `If-Match` (not described in the OpenAPI document), errors switched on a stable `code`, months written `yyyy-MM`, amounts never added on the client ([backend/api-endpoints.md](../../backend/api-endpoints.md#conventions)).
- The repository is public and meant to grow (more features, contributors, multi-user): the backend checks its structure with architecture tests (ADR-0012), and the frontend needs the same kind of guard.
- UI in Italian, ready for i18n; code and documentation in English.

## Decision

1. **Theme**: one Material 3 theme with `mat.theme`, palette `azure`, `theme-type: color-scheme`. **Dark mode follows the operating system** (`color-scheme: light dark`, colors emitted as `light-dark()`); no manual switch in the MVP. Money colors are app tokens: `--pf-income` (M3 green, tone 40 / 80) and `--pf-expense` (the `error` role). Components use `--mat-sys-*` and `--pf-*` tokens only.
2. **Typography and icons**: Roboto self-hosted with `@fontsource/roboto` (400, 500, 700), no font CDN. Amounts use tabular figures (`.pf-amount`). Icons are the **Material Symbols SVG files** (`@material-symbols/svg-400`), copied to `icons/` at build time and resolved by `MatIconRegistry`: only the icons shown are downloaded, instead of a 4 MB icon font.
3. **Structure by feature**, mirroring the backend modules: `core/` (singletons, layout, HTTP, API types), `shared/<library>/` (reusable UI and utilities, nothing app-specific), `features/<feature>/` (`dashboard`, `transactions`, `budgets`, `accounts`, `categories`, later `transfers`, `auth`). Every module exposes a barrel (`index.ts`). **Boundaries are checked by Sheriff through ESLint** (`angular-eslint` added): a feature uses `core` and `shared` only, never another feature; `core` uses `shared`; `shared` uses `shared`; no deep imports past a barrel.
4. **Layout and navigation**: an M3 **navigation bar at the bottom on phones** and a **navigation rail from 600px**, switched by CSS only, with at most five destinations (Home, Movimenti, Budget, Conti, Categorie). A **floating "Nuova spesa" button** on every page opens `/transactions/new`.
5. **Routing**: English paths, the same in every language; one lazy route file per feature, loaded from its barrel; the shell is a parent route, so the login page (block 3) lives outside it; unknown paths go to the dashboard; route parameters bound to inputs; browser title "Page · Personal Finance" from the route `title`.
6. **State**: no state library. Each feature has **store services** (`@Service()`) that expose signals, read with `httpResource()` / `resource()` and write through `HttpClient`, then reload. Components never call `HttpClient`. The store is the feature's facade, so its inside can move to NgRx SignalStore without touching components.
7. **API client**: **TypeScript types generated** from the OpenAPI document with `openapi-typescript` into `core/api/schema.ts` (committed, regenerated with `pnpm api:types` while the API runs in Development); **services written by hand**, which add versions, error codes and months.
8. **i18n**: built-in **`@angular/localize`**. The source locale `it` is set in `angular.json` (`i18n.sourceLocale`), which also sets `LOCALE_ID`, the locale data and `<html lang>`. Texts are marked with `i18n` / `$localize` with a meaning, and keep the generated ids. Another language is another build, served under its own path.
9. **Coverage**: Vitest's **`v8`** provider through the builder's `coverage` option (`pnpm test:coverage`), reports `text-summary`, `html`, `lcov`, no threshold (ADR-0006).

Details: [frontend/app-structure.md](../../frontend/app-structure.md), [frontend/routing.md](../../frontend/routing.md), [frontend/state-management.md](../../frontend/state-management.md), [frontend/ui-components.md](../../frontend/ui-components.md), [frontend/testing.md](../../frontend/testing.md).

## Consequences

**Positive**
- A new end-to-end feature adds a backend module and a frontend feature with the same name; lint fails as soon as two features depend on each other.
- One-handed use: every section is one tap away and the new expense button is always under the thumb.
- Dark mode costs no code; a restyling is a change of palette in one file.
- Nothing is loaded from third-party servers (fonts, icons): good for privacy, for a strict CSP and for the offline PWA.
- No state or i18n dependency to upgrade beside Angular; the stable signal APIs are used as they are.
- Request and response types follow the backend; headers and errors stay under our control.

**Negative**
- Cache and refresh after writes are written by hand: a convention (`state-management.md`) replaces a library.
- Regenerating the types needs the API running; until the document is exported at build time (handed off to `[Backend]` / `[DevOps]`), a drift is found only when someone regenerates.
- Changing language reloads the page and every locale is a separate build.
- Two more development dependencies (ESLint, Sheriff) and one config file to maintain.
- The `icons/` folder of the build holds every Material Symbol (about 7,900 small files), even though only a few are requested.

## Alternatives considered

**Palette generated from a brand color** — Rejected for now: a distinct look for the public repository, but a generated file to maintain and a brand to choose today. Switching later is a change in `styles.scss`.

**Inter or the system font** — Rejected: Inter moves away from the metrics Material is tuned for; the system font changes the look on every platform and makes screenshots unstable.

**Light theme only, or a manual light/dark switch** — Rejected: light-only saves almost nothing with `light-dark()` tokens; a manual switch is a preference that belongs on the server once there are several users.

**Folders by technical type, or features without enforced boundaries** — Rejected: by type, every feature touches every folder; without enforcement, the structure erodes silently.

**Side navigation drawer everywhere** — Rejected: one more tap for every section on the phone.

**NgRx SignalStore** — Rejected for now: more structure and devtools, but more concepts and a major to follow with each Angular release. The store facade keeps it possible later.

**TanStack Query** — Rejected: the right features (cache, cursor pages, offline persistence), but the Angular adapter is still published as experimental.

**Client written entirely by hand** — Rejected: types would drift from the backend unnoticed.

**Client fully generated (services included)** — Rejected: generated services know neither the versions nor the error codes, and the generators change often.

**Transloco (runtime i18n)** — Rejected: instant language switch, but keys instead of text, missing keys that fail silently and one more dependency.

**`istanbul` coverage** — Rejected: slower, with no advantage here since Vitest remaps V8 coverage to the same results.
