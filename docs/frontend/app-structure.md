# App structure

## Scope

Folders of the Angular application, what goes where, the module boundaries and how they are checked, the API types, the locale and i18n setup, and the tooling of the workspace (lint, formatting, scripts). Routes are in [routing.md](routing.md), state in [state-management.md](state-management.md), components and theme in [ui-components.md](ui-components.md).

## Current state

Implemented in block 1 ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)): the shell, five placeholder features and the boundaries are in place; the API types arrive with block 2.

### Folders

```
frontend/
├── angular.json            # build, i18n (sourceLocale "it"), assets (icons), lint, test (coverage)
├── eslint.config.js        # angular-eslint + Sheriff
├── sheriff.config.ts       # module boundaries
├── package.json            # scripts below
└── src/
    ├── index.html
    ├── main.ts
    ├── styles.scss         # theme and global tokens (ui-components.md#theme)
    └── app/
        ├── app.ts / app.config.ts / app.routes.ts   # root: wires everything (tag "root")
        ├── core/           # one module (tag "core")
        │   ├── index.ts    # public API of core
        │   ├── api/        # schema.ts generated from OpenAPI (block 2)
        │   ├── icons/      # Material Symbols resolver
        │   └── layout/     # Shell, navigation items, page titles
        ├── shared/         # one module per library (tag "shared"), e.g. shared/ui, shared/util (block 4)
        └── features/       # one module per feature (tag "feature")
            ├── dashboard/
            ├── transactions/
            ├── budgets/
            ├── accounts/
            └── categories/
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
| `shared/<library>` | Reusable UI and utilities that know nothing about the app: amount input, date adapter, filter bar, empty and error states (block 4). They take data through inputs | Calls to the API, imports from `core` or features |
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
- When two features need the same thing, it moves to `core` (app-specific: e.g. the accounts list for selectors) or `shared` (generic: e.g. an amount input).

### API types

- `pnpm api:types` (API running in Development on `http://localhost:5080`) writes `src/app/core/api/schema.ts` with `openapi-typescript` and formats it. The file is committed and excluded from lint and coverage.
- Services are written by hand on `HttpClient` and use the generated types for bodies and responses ([state-management.md](state-management.md)).
- What the types do not say, and services add: `If-Match` with the version, the error `code`, months `yyyy-MM`, client UUIDv7 ids (block 2).

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
| `pnpm api:types` | Regenerate `core/api/schema.ts` from the running API |

Formatting: Prettier (`.prettierrc`, width 100, single quotes).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Folders by feature, named like the backend modules; `core`, `shared`, `features` | A new feature is one backend module plus one frontend folder | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Boundaries checked by Sheriff through ESLint | Same idea as the architecture tests of the backend: the structure cannot erode silently | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Types generated from OpenAPI, services by hand | Types follow the backend; versions and error codes stay under our control | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| `@angular/localize`, source locale `it` in `angular.json` | Built into Angular, no runtime cost; locale, locale data and `lang` from one setting | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |

## Tests

- `pnpm lint` runs Sheriff: when block 1 was set up, an import from `features/accounts` into `features/transactions` and a deep import into `core/layout` were both reported, then removed.
- `app-locale.spec.ts`: `LOCALE_ID` is `it` without providers; numbers and dates are formatted the Italian way (`1.234,50`, `13 novembre 2026`).
- `app.spec.ts`: every feature is reached through its lazy route ([routing.md](routing.md#tests)).
- `core/icons/app-icons.spec.ts`: icon names resolve to `icons/<name>.svg`; anything else (paths, URLs, namespaces) is refused.

## Scalability

- **New feature**: a folder in `features/` with its barrel and routes, one line in `app.routes.ts`; Sheriff tags it automatically.
- **Growing core**: when `core` gets large, it splits into `core/<library>` modules (`core/http`, `core/auth`, …) with the same tag; the rules do not change.
- **Contributors**: the boundaries and the naming are checked by lint, not by review.
- **Multi-user**: per-user preferences (language, theme, default account) live in `core` and come from the API.
- **PWA**: the service worker and the offline queue belong to `core`; features keep calling their stores.

## To do / open questions

- Block 2: first `schema.ts` (needs the API running in Development), `core/http` (antiforgery, errors, versions), UUIDv7 generator.
- `[Backend]` / `[DevOps]`: export the OpenAPI document at build time and check in CI that `schema.ts` matches it.
- `[DevOps]`: run `pnpm lint` and `pnpm test:coverage` in CI.
- Split `core` into libraries when it grows past a handful of folders.

---
_Last updated: 2026-11-13_
