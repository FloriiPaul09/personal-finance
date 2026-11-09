# UI components

## Scope

The UI component library, theming, the shared components built in the project, and the rules for forms, dates, amounts and mobile use. Application structure, routing and state are covered in their own documents.

## Current state

Decided ([ADR-0004](../architecture/adr/0004-angular-ui-library.md)), **not implemented yet**.

### Foundation

- **Angular Material** for components, **Angular CDK** for lower-level behavior (overlays, accessibility, layout breakpoints).
- No other component library alongside Material: mixing libraries leads to inconsistent look, double theming and double upgrade effort.
- **Charts**: a dedicated library, still to be chosen.

### Theme

- One **Material 3 theme** built on design tokens, defined in a single place (global styles) and used everywhere.
- Components use theme tokens, never hard-coded colors, so a restyling or a dark theme is a change in one place.

### Shared components (to build)

| Component | Why | Notes |
|---|---|---|
| Amount input | Material has no currency/amount field | Numeric keyboard on phones (`inputmode="decimal"`), Italian format (`1.234,56`), sends a plain decimal to the API |
| List filter bar | Material tables have sorting and paging, not filters | Period, account, category, text — reused by every list |
| Empty and error states | Consistent messages across pages | Italian texts, i18n-ready |

Shared components live in a shared UI folder (exact location defined in `app-structure.md`) and wrap Material: pages use them instead of re-implementing the same pattern.

### Dates and amounts

- A transaction date is a **calendar date without time** (`DateOnly` in the API, `YYYY-MM-DD` in JSON). The date picker must use a date adapter that **never shifts the day because of time zones** when converting to and from that format.
- Amounts are never handled as floating-point for calculations in the frontend: the API is the source of truth for totals and balances.
- Locale `it-IT` for dates, numbers and currency formatting.

### Mobile

- Mobile-first layouts: the transaction entry form must be usable with one hand, with the minimum number of fields visible by default.
- Responsive behavior through CDK breakpoints (e.g. full-screen dialog on phones, standard dialog on desktop).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Angular Material + CDK, no other component library | Upgrades in step with Angular, accessibility, familiar to contributors | [ADR-0004](../architecture/adr/0004-angular-ui-library.md) |
| Missing components built as shared wrappers | Few and simple; keeps one look and one upgrade path | [ADR-0004](../architecture/adr/0004-angular-ui-library.md) |
| Separate chart library | Material has no charts | [ADR-0004](../architecture/adr/0004-angular-ui-library.md) |

## Scalability

- **More features**: new pages reuse Material and the shared components; the shared set grows only when a pattern repeats.
- **Multi-user**: per-user preferences (theme, locale, default account) fit on top of the token-based theme and i18n setup.
- **PWA** (roadmap): Material works offline once cached; installability is a build/config concern, not a library change.
- **Upgrades**: `ng update @angular/material` together with every Angular major release.
- **Restyling**: through theme tokens; replacing the library would require a new ADR.

## To do / open questions

- `[Frontend]` Choose the chart library (needs: bar/pie for categories, line for trends, good mobile rendering, active maintenance).
- `[Frontend]` Choose the date adapter and verify the no-time-zone-shift behavior with tests.
- `[Frontend]` Define theme colors, typography and whether a dark theme is part of the MVP.
- `[Frontend]` i18n approach (built-in Angular i18n or a runtime library) — affects how shared components receive texts.
- `[Frontend]` Location of shared components → `app-structure.md`.
- `[Frontend]` Login page, route guard and `401` interceptor (from [authentication.md](../architecture/authentication.md)).

---
_Last updated: 2026-11-09_
