# ADR-0004: Angular Material as the UI component library

- **Status**: Accepted
- **Date**: 2026-11-09
- **Deciders**: Paul

## Context

- The main use case is quick data entry from a phone, plus lists with filters, date pickers and a dashboard with charts.
- The repository is public and meant to last for years: it will follow every Angular major release, so the cost of upgrades weighs more than the speed of the first version.
- The UI is in Italian and must be ready for i18n; accessibility matters for a public project.

## Decision

1. **Angular Material + Angular CDK** are the UI foundation. No other component library is used alongside them.
2. Components Material doesn't provide (amount/currency input, filter bar for lists, empty states) are built as **small shared components** on top of Material and the CDK.
3. **Charts** come from a dedicated library, chosen in `[Frontend]`.
4. One **Material 3 theme**, based on design tokens, defined once for the whole app.

Details: [frontend/ui-components.md](../../frontend/ui-components.md).

## Consequences

**Positive**
- Maintained by the Angular team and released in step with Angular: upgrades go through `ng update` with migrations.
- Good accessibility and touch-friendly components out of the box.
- Well known to Angular developers: low entry cost for contributors.
- Theme tokens make a later restyling (or user-selectable themes) a localized change.

**Negative**
- Fewer ready-made components than richer libraries: some (filters, amount input) must be written and maintained in the project.
- No charts: an additional dependency is needed.
- The default look is recognizably "Material"; deep customization beyond the theme tokens is laborious.

## Alternatives considered

**B. PrimeNG** — Rejected. Very rich catalog (advanced tables, formatted numeric inputs, charts) that would speed up the first version, but it is a third-party dependency with a history of major releases with significant breaking changes (e.g. the theming overhaul), heavier, and with uneven quality across components.

**C. Tailwind + Angular CDK with custom components** — Rejected. Full control over design and bundle size, but date pickers, tables and dialogs would have to be built before any real feature, and consistency for contributors would require a documented design system.
