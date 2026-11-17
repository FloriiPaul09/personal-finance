# App structure

## Scope

Folders of the Angular application, what goes where, the module boundaries and how they are checked, the API types, the locale and i18n setup, and the tooling of the workspace (lint, formatting, scripts). Routes are in [routing.md](routing.md), state in [state-management.md](state-management.md), components and theme in [ui-components.md](ui-components.md).

## Current state

Implemented in block 1 ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)): the shell, five placeholder features and the boundaries. Block 2 ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)) added the API types and the HTTP layer in `core`; block 3 ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)) the session (`core/auth`), form helpers (`core/forms`), the `auth` and `settings` features and the Playwright tests (`e2e/`); block 4 ([ADR-0024](../architecture/adr/0024-frontend-shared-components.md)) the first shared libraries (`shared/money`, `shared/dates`, `shared/ui`), `core/feedback` and the development-only `dev` feature; block 5 ([ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md)) `core/data`, `core/selectors`, `core/testing` and the `accounts` and `categories` features; block 6 ([ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md)) the `transactions` pages, the `transfers` feature, `RecentChoices`, `PageExit` and the kind switch; block 7 ([ADR-0027](../architecture/adr/0027-frontend-budgets.md)) the `budgets` pages, `<app-budget-progress>` in `shared/ui`, `BUDGET_CURRENCY` and the warning palette; block 8 ([ADR-0028](../architecture/adr/0028-frontend-dashboard.md)) the `dashboard` page, `shared/charts` with Chart.js and the account types in `core/selectors`.

### Folders

```
frontend/
├── angular.json            # build, i18n (sourceLocale "it"), assets (icons), lint, test (coverage)
├── eslint.config.js        # angular-eslint + Sheriff
├── sheriff.config.ts       # module boundaries
├── playwright.config.ts    # end-to-end tests (testing.md#end-to-end-playwright)
├── package.json            # scripts below
├── e2e/                    # Playwright tests and their support code
├── scripts/
│   └── api-types.mjs       # generates core/api/schema.ts (pnpm api:types)
└── src/
    ├── index.html
    ├── main.ts
    ├── styles.scss         # theme and global tokens (ui-components.md#theme)
    ├── _income-palette.scss # generated tonal palette of the income green
    ├── _warning-palette.scss # generated tonal palette of the budget warning (amber)
    ├── _list-rows.scss     # mixins: rows of the accounts and categories lists
    ├── _form-page.scss     # mixin: pages with one form
    └── app/
        ├── app.ts / app.config.ts / app.routes.ts   # root: wires everything (tag "root")
        ├── dev-routes.ts / dev-routes.prod.ts     # development-only pages; the production build uses the empty one
        ├── core/           # one module (tag "core")
        │   ├── index.ts    # public API of core
        │   ├── auth/       # SessionStore, authGuard / guestGuard, sessionInterceptor, safeReturnUrl
        │   ├── forms/      # apiFormErrors, focusFirstError — own barrel, keeps @angular/forms out of the initial bundle
        │   ├── selectors/  # <app-account-select>, <app-category-select>, <app-movement-kind-switch>, account types (labels, icons) — own barrel (components)
        │   ├── testing/    # test data and HTTP helpers for unit tests — own barrel, not in the build
        │   ├── data/       # DataChanges, referenceList, AccountsDirectory, CategoriesDirectory, RecentChoices, BUDGET_CURRENCY
        │   ├── api/        # openapi.json (snapshot), schema.ts (generated), models.ts (aliases), ApiError, messages
        │   ├── errors/     # global ErrorHandler, ErrorNotifier (snackbar)
        │   ├── feedback/   # ErrorState (data that could not be loaded), Notifier (snackbars)
        │   ├── http/       # provideApiHttp (XSRF, interceptor, antiforgery), ifMatch, cursorPager
        │   ├── ids/        # newId() — UUIDv7 for client-created resources
        │   ├── icons/      # Material Symbols resolver
        │   └── layout/     # Shell, navigation items, FORM_PAGE, PageExit, page titles, snackbar options, "unavailable" page
        ├── shared/         # one module per library (tag "shared")
        │   ├── money/      # parseAmount, formatAmount, AmountPipe, <app-amount-input>, currencies
        │   ├── dates/      # ISO dates and months, IsoDateAdapter (no components: imported at startup)
        │   ├── date-pickers/ # <app-date-input>, <app-month-selector>
        │   ├── ui/         # <app-empty-state>, confirmAction(), <app-budget-progress>
        │   ├── charts/     # <app-bar-chart>, resolveCssColor — the only place that imports Chart.js
        │   └── testing/    # helpers for unit tests only (time zones)
        └── features/       # one module per feature (tag "feature")
            ├── dashboard/  # home page of a month: totals, budgets, spending, current balances
            ├── transactions/ # movement list, expense and income form, tags input
            ├── transfers/  # transfer form (no page in the navigation)
            ├── budgets/    # budgets of a month, budget form with its history
            ├── accounts/
            ├── categories/
            ├── auth/       # login page
            ├── settings/   # account settings: logout, password change
            └── dev/        # catalog of the shared components (development only)
```

Inside a feature:

```
features/accounts/
├── index.ts               # public API: ACCOUNTS_ROUTES only
├── accounts.routes.ts
├── pages/                 # routed components: accounts-page.ts, …
├── components/            # components used only by this feature (when needed)
└── data/                  # API service and store of the feature (state-management.md)
```

| Folder | Contains | Never contains |
|---|---|---|
| `core` | Singletons used by the whole app: layout, HTTP setup and interceptors (block 2), session and guard (block 3), API types, reference data shared by several features (accounts and categories lists) | Pages of a feature |
| `shared/<library>` | Reusable UI and utilities that know nothing about the app, one library per topic: `money`, `dates`, `date-pickers`, `ui`, `charts`, `testing`. They take data and translated texts through inputs | Calls to the API, imports from `core` or features |
| `features/<feature>` | Pages, components and stores of one area, named after the backend module | Imports from another feature |

### Naming

- Files in kebab-case, without the `.component` / `.service` suffixes (Angular 20+ style): `accounts-page.ts`, `accounts-store.ts`, `shell.ts`.
- Classes named after their role: `AccountsPage`, `AccountsStore`, `Shell`.
- Selector prefix `app-` (checked by angular-eslint).
- Route constants `<FEATURE>_ROUTES`, exported by the feature barrel.
- Singletons use `@Service()` (Angular 22), which provides them in the root injector; classes that are provided by hand (e.g. the title strategy) use `@Service({ autoProvided: false })`.

### Boundaries

[Sheriff](https://github.com/softarc-consulting/sheriff) reads `sheriff.config.ts` and reports, through ESLint, every import that crosses a boundary:

| From | May import |
|---|---|
| root (`main.ts`, `app.*`) | `core`, `shared`, features |
| `core` | `core`, `shared` |
| `shared/*` | `shared` |
| `features/*` | `core`, `shared` — **never another feature** |

- A module is a folder with an `index.ts`; other modules import only that file (**no deep imports**).
- A barrel that matches no tag gets `noTag`, which may import nothing: a new top-level folder cannot slip past the rules.
- `core/forms`, `core/selectors` and `core/testing` are further barrels of `core` (same tag): importing `core` must not pull `@angular/forms` or Material's select into the initial bundle, which has a 500 kB budget (block 3 reached 548 kB before the split, 473 kB after); test helpers are never built into the app.
- **Nothing loaded at startup may use Material's overlays** (snackbar, dialog, menu, select, date picker): the build splits code by file, so when the startup code reaches a file, every part of that file that a page uses is loaded at startup too. With the snackbar and the date adapter provided in `app.config`, the menus, selects and dialogs of the block 5 pages raised the initial bundle from 479 kB to 571 kB. Messages go through `Notifier`, which imports the snackbar on first use; the date adapter is provided by `<app-date-input>`.
- **A component used by two features goes to `core`** even when it is small: the kind switch of the new-movement form links the `transactions` and `transfers` forms, so it lives in `core/selectors`. A component that knows nothing of the API goes to `shared`: the budget progress bar takes numbers and is used by the budgets page and the dashboard, so it lives in `shared/ui` (which nothing loaded at startup imports).
- **A chart library enters only the chunks of the pages that draw a chart**: Chart.js is imported only by `shared/charts`, which nothing loaded at startup imports, and each chart component registers only the parts it draws (block 8: the bar chart, about 53 kB gzip loaded with the dashboard).
- The account types (labels and icons) are in `core/selectors` since block 8: the accounts pages and the dashboard both show them, and features cannot import each other.
- Something generic that needs an app type goes to `core` (e.g. `ErrorState` reads `ApiError`, so it is in `core/feedback`, not `shared/ui`).
- A barrel imported at startup (by `app.config` or `core`) exports no component: tree shaking does not remove an unused component, so its whole library would enter the initial bundle (`shared/dates` vs `shared/date-pickers`).
- Test helpers go in `shared/testing` or a `testing/` folder: excluded from the app build and from coverage.
- When two features need the same thing, it moves to `core` (app-specific: e.g. the accounts list for selectors) or `shared` (generic: e.g. an amount input).

### API types

`scripts/api-types.mjs` ([ADR-0022](../architecture/adr/0022-frontend-api-integration.md)):

| Command | Source of the document |
|---|---|
| `pnpm api:types` | The API running in Development (`http://localhost:5080/openapi/v1.json`); the document is saved as `core/api/openapi.json` |
| `pnpm api:types --url <url>` | Another address |
| `pnpm api:types --file src/app/core/api/openapi.json` | The committed snapshot, without the API (e.g. a CI check) |

1. The document is **adjusted** before generation, where it is looser than the API: every number is described as `number | string` (ASP.NET Core also *reads* numbers written as text) → `number` / `integer` only; `YearMonth` is an empty schema → a `yyyy-MM` string. Each rule goes away once `[Backend]` describes the value precisely.
2. `openapi-typescript` writes `core/api/schema.ts` (formatted with Prettier). Both `openapi.json` and `schema.ts` are committed; `schema.ts` is excluded from lint and coverage. A change of the contract shows in the pull request as a diff of `openapi.json`.
3. The app imports the **aliases** of `core/api/models.ts`, never `schema.ts` directly: `AccountResponse`, `CategoryResponse`, `TransactionResponse`, `TransferResponse`, `BudgetResponse`, `BudgetProgressResponse`, `DashboardResponse`, request types, `ListResponse<T>`, `PageResponse<T>`, `Version`, `IsoDate`, `YearMonth`.

Services are written by hand per feature on `HttpClient` ([state-management.md](state-management.md#the-store-pattern)); what the types do not say comes from `core`: `ifMatch(version)`, `ApiError` and its `code`, `newId()`, `cursorPager()`.

### Locale and i18n

- `angular.json` → `i18n.sourceLocale: "it"`: the build sets `LOCALE_ID` to `it`, registers the Italian locale data and writes `<html lang="it">`. No provider is needed; a test fails if the setting is lost.
- Texts in templates carry `i18n="meaning"`; texts in TypeScript use `` $localize`:meaning:text` ``. Generated ids are kept (no `@@custom-id`): a changed Italian text shows up as a missing translation.
- Error messages for API `code`s are a `$localize` map in `core` (block 2).
- Product name `Personal Finance` is not translated.
- Another language later: translation file from `ng extract-i18n`, `localize` in `angular.json`, one build per locale served under its own path by nginx (`[DevOps]`).

### Scripts

| Command (from `frontend/`) | What it does |
|---|---|
| `pnpm start` | Dev server on http://localhost:4200, `/api` proxied to the API |
| `pnpm build` | Production build in `dist/` |
| `pnpm test` | Vitest in watch mode; `pnpm test --watch=false` for one run |
| `pnpm test:coverage` | One run with coverage (`coverage/`) |
| `pnpm lint` | ESLint: Angular rules, template accessibility, module boundaries |
| `pnpm api:types` | Regenerate `core/api/openapi.json` and `schema.ts` from the running API (`--file` for a saved document) |
| `pnpm e2e` | Playwright tests on the full stack ([testing.md](testing.md#end-to-end-playwright)); `pnpm e2e:report` opens the last report |

Formatting: Prettier (`.prettierrc`, width 100, single quotes).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Folders by feature, named like the backend modules; `core`, `shared`, `features` | A new feature is one backend module plus one frontend folder | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Boundaries checked by Sheriff through ESLint | Same idea as the architecture tests of the backend: the structure cannot erode silently | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Types generated from OpenAPI, services by hand | Types follow the backend; versions and error codes stay under our control | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| `@angular/localize`, source locale `it` in `angular.json` | Built into Angular, no runtime cost; locale, locale data and `lang` from one setting | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Session in `core/auth`, form helpers in a separate `core/forms` barrel, Playwright in `frontend/e2e` | Every feature needs the session; forms only where there is a form; E2E share the workspace's dependencies | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |
| `shared/money`, `shared/dates`, `shared/date-pickers`, `shared/ui`, `shared/testing`; development pages replaced by an empty list in production; initial budget error at 600 kB | One library per topic, imported only where used; nothing heavy at startup; developer tools cost nothing in production | [ADR-0024](../architecture/adr/0024-frontend-shared-components.md) |
| `core/data` for reference data, `core/selectors` and `core/testing` as own barrels; snackbar and date adapter out of the startup code | Shared lists without feature imports; the initial bundle stays at 479 kB as pages add menus and dialogs | [ADR-0025](../architecture/adr/0025-frontend-accounts-and-categories.md) |
| `transfers` as its own feature; kind switch, `RecentChoices` and `PageExit` in `core` | Transfers have their own resource and rules; what both movement forms use cannot live in one of them | [ADR-0026](../architecture/adr/0026-frontend-transactions-and-transfers.md) |
| Budget progress in `shared/ui`, `BUDGET_CURRENCY` and `referenceList` exported by `core` | The dashboard shows the same bars; one constant to replace when budgets get a currency; the entries list reuses the reference lists | [ADR-0027](../architecture/adr/0027-frontend-budgets.md) |
| Chart.js only in `shared/charts`, with only the parts each chart draws; account types in `core/selectors` | The library stays out of the initial bundle; accounts and dashboard share the types without importing each other | [ADR-0028](../architecture/adr/0028-frontend-dashboard.md) |

## Tests

- `pnpm build` checks the bundle budgets: a warning above 500 kB for the initial bundle, an error above 600 kB (481 kB after block 4, 479 kB after block 5, 491.5 kB after block 6, 491.6 kB after block 7, 492.3 kB after block 8; 571 kB in block 5 before the snackbar and the date adapter left the startup code). Block 6 added 12 kB because code that lazy pages share with the startup code loads at startup: `uuid` (through `newId()`), `cursorPager`, `RecentChoices`, `PageExit`. Adding the catalog as a normal lazy route raised it to 530 kB, because code shared between a lazy chunk and the app is loaded at startup: that is why the production build drops the development routes with `fileReplacements`; `app.spec.ts` checks the production list is empty.
- `pnpm lint` also lints `e2e/` and `playwright.config.ts`; `e2e/tsconfig.json` type-checks the Playwright tests.
- `pnpm lint` runs Sheriff: when block 1 was set up, an import from `features/accounts` into `features/transactions` and a deep import into `core/layout` were both reported, then removed.
- `app-locale.spec.ts`: `LOCALE_ID` is `it` without providers; numbers and dates are formatted the Italian way (`1.234,50`, `13 novembre 2026`).
- `app.spec.ts`: every feature is reached through its lazy route ([routing.md](routing.md#tests)).
- `core/icons/app-icons.spec.ts`: icon names resolve to `icons/<name>.svg`; anything else (paths, URLs, namespaces) is refused.
- The generation was checked against the API in Development: the snapshot is byte-identical to the document it served (SHA-256), and the generated types have `number` amounts and versions and `string` months.

## Scalability

- **New feature**: a folder in `features/` with its barrel and routes, one line in `app.routes.ts`; Sheriff tags it automatically.
- **Growing core**: when `core` gets large, it splits into `core/<library>` modules (`core/http`, `core/auth`, …) with the same tag; the rules do not change.
- **Contributors**: the boundaries and the naming are checked by lint, not by review.
- **Multi-user**: per-user preferences (language, theme, default account) live in `core` and come from the API.
- **PWA**: the service worker and the offline queue belong to `core`; features keep calling their stores.

## To do / open questions

- `[Backend]`: describe numbers as `number` only and `YearMonth` as a `yyyy-MM` string in the OpenAPI document, then remove the adjustments of `scripts/api-types.mjs`.
- `[Backend]` / `[DevOps]`: export the OpenAPI document at build time and check in CI that `openapi.json` and `schema.ts` match it (`pnpm api:types --file …` then `git diff --exit-code`).
- `[DevOps]`: run `pnpm lint` and `pnpm test:coverage` in CI.
- Split `core` into libraries when it grows past a handful of folders.

---
_Last updated: 2026-11-17_
