# UI components

## Scope

The UI component library, theming, typography and icons, the shared components built in the project, and the rules for forms, dates, amounts and mobile use. Application structure, routing and state are covered in their own documents.

## Current state

Library decided in [ADR-0004](../architecture/adr/0004-angular-ui-library.md); theme, typography, icons and layout implemented in block 1 ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)). Forms (Signal Forms with Material) since block 3 ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)). Shared components arrive with block 4, charts with block 8.

### Foundation

- **Angular Material 22** for components, **Angular CDK** for lower-level behavior (overlays, accessibility, layout breakpoints).
- No other component library alongside Material: mixing libraries leads to inconsistent look, double theming and double upgrade effort.
- **Charts**: a dedicated library, chosen with the dashboard (block 8).

### Theme

Defined once in `src/styles.scss`:

```scss
html {
  color-scheme: light dark;
  @include mat.theme((
    color: (primary: mat.$azure-palette, theme-type: color-scheme),
    typography: Roboto,
    density: 0,
  ));
  --pf-income: light-dark(<green 40>, <green 80>);
  --pf-expense: var(--mat-sys-error);
}
```

- **Material 3**, palette `azure`. Changing palette is a one-line change.
- **Dark mode follows the operating system**: every color token is emitted as `light-dark(light, dark)` and `color-scheme: light dark` lets the browser pick. `<meta name="color-scheme">` in `index.html` avoids a white flash on dark devices. No manual switch in the MVP.
- Components use **`--mat-sys-*` tokens** (e.g. `--mat-sys-surface-container`, `--mat-sys-on-surface-variant`, `--mat-sys-label-medium`) and the app tokens below, **never hard-coded colors**.

| App token / class | Use |
|---|---|
| `--pf-income`, `.pf-income` | Income amounts (M3 green, tone 40 on light, 80 on dark) |
| `--pf-expense`, `.pf-expense` | Expense amounts (the `error` role) |
| `.pf-amount` | Every amount: tabular figures, so digits line up in columns |
| `.pf-page`, `.pf-page-title` | Page container (padding, max width 1200px) and its `h1`; on phones the title leaves 40px on the right for the account button |
| `.pf-centered` | Centered column for pages outside the shell (login, "unavailable") |

### Typography

- **Roboto**, self-hosted with `@fontsource/roboto` (weights 400, 500, 700; latin, latin-ext and other subsets downloaded only when a character needs them). No font CDN: privacy, strict CSP, offline PWA.
- Material type scale through tokens: `font: var(--mat-sys-body-large)`, `var(--mat-sys-headline-small)`, …

### Icons

- **Material Symbols (outlined) as SVG files** from `@material-symbols/svg-400`, copied to `icons/` by the build (`angular.json` assets).
- `provideAppIcons()` registers a resolver: `<mat-icon svgIcon="receipt_long" />` loads `icons/receipt_long.svg`; the `-fill` variant (`receipt_long-fill`) is used for active states. Only icons actually shown are downloaded (a few hundred bytes each), instead of a 4 MB icon font.
- Names are validated (lower-case words joined by `_`, optional `-fill`); anything else is not resolved.
- Decorative icons next to a label need nothing else (`mat-icon` is `aria-hidden` by default); an icon-only button needs an `aria-label`.

### Layout

- **Shell** with M3 navigation bar on phones and navigation rail from 600px, floating "Nuova spesa" button ([routing.md](routing.md#shell-and-navigation)).
- Account button (settings) at the top right on phones, at the bottom of the rail from 600px ([routing.md](routing.md#shell-and-navigation)).
- Phone first: the main content leaves room at the bottom for the bar and the floating button; safe areas (`env(safe-area-inset-bottom)`) are respected for phones with a gesture bar.

### Shared components (block 4)

| Component | Why | Notes |
|---|---|---|
| Amount input | Material has no currency/amount field | Numeric keyboard on phones (`inputmode="decimal"`), Italian format (`1.234,56`), sends a plain decimal to the API, refuses a third decimal |
| Date adapter | The date picker must not shift days | Works on `YYYY-MM-DD` strings, never through UTC |
| List filter bar | Material tables have sorting and paging, not filters | Period, account, category, text — reused by every list |
| Empty and error states | Consistent messages across pages | Italian texts with `i18n`, error state reads the API `code` |
| Account and category selectors | Used by transactions, transfers, budgets | Category menu grouped by parent from the flat list (`parentId`) |

They live in `shared/ui` (generic ones) or `core` (those that need app data, like the selectors fed by reference data), as described in [app-structure.md](app-structure.md#folders), and wrap Material: pages use them instead of re-implementing the same pattern.

### Forms

Signal Forms (`@angular/forms/signals`) with Material form fields, block 3 ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)):

```ts
protected readonly model = signal({ email: '', password: '' });
protected readonly loginForm = form(this.model, (path) => {
  required(path.email, { message: $localize`:Login form error:Inserisci l'email.` });
  email(path.email, { message: $localize`:Login form error:Email non valida.` });
});

protected async signIn(event: Event): Promise<void> {
  event.preventDefault();
  await submit(this.loginForm, async () => {
    try {
      await this.session.login(this.model());
    } catch (error) {
      const errors = apiFormErrors(this.loginForm, error, { formCodes: ['auth.invalid_credentials'] });
      if (errors) return errors;   // shown on the fields / the form
      throw error;                 // anything else: global handler
    }
    return undefined;
  });
}
```

```html
<mat-form-field>
  <mat-label i18n="Login form field">Email</mat-label>
  <input matInput type="email" autocomplete="username" [formField]="loginForm.email" />
  @if (loginForm.email().touched() && loginForm.email().errors()[0]; as error) {
    <mat-error>{{ error.message }}</mat-error>
  }
</mat-form-field>
@for (error of loginForm().errors(); track $index) {
  <p class="form-error" role="alert">{{ error.message }}</p>
}
```

- Each validator carries its Italian message; the template shows the first error of a field.
- **Messages appear once the field is touched** (blur, or submit, which touches every field). The `@if` sits directly inside `mat-form-field` with `touched()` in the same condition: a nested `@if` stops `mat-error` from being projected under the field, and it showed inside the input on load (block 3).
- **Server errors** go through `apiFormErrors()` (`core/forms`): on the fields the API named (`validation.failed`, generic "Valore non valido." because the API's own messages are English), on a field chosen by code (`fieldsByCode`, e.g. `auth.password_incorrect` → `currentPassword`) or on the whole form (`formCodes`, shown in a `role="alert"` paragraph). A server error stays until the field changes, so the same wrong value is not sent twice.
- `novalidate` on the `<form>`: the browser's own bubbles never show; the submit button is disabled only while submitting (`form().submitting()`), never because the form is invalid, so pressing it shows what is missing.
- Password fields: `autocomplete` set (`current-password`, `new-password`) for password managers; the login has a show/hide button with `aria-pressed` and a label.

### Dates and amounts

- A transaction date is a **calendar date without time** (`DateOnly` in the API, `YYYY-MM-DD` in JSON). The date picker must use a date adapter that **never shifts the day because of time zones** when converting to and from that format.
- Amounts are never handled as floating-point for calculations in the frontend: the API is the source of truth for totals and balances.
- Locale `it` for dates, numbers and currency formatting, set by `i18n.sourceLocale` ([app-structure.md](app-structure.md#locale-and-i18n)).

### Texts and i18n

- Every visible text is marked: `i18n="meaning"` in templates (and `i18n-aria-label` for attributes), `` $localize`:meaning:text` `` in TypeScript. Shared components receive their texts already translated (inputs) or mark their own.
- Italian is the source language; another language is a translation file and a build ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)).

### Mobile

- Mobile-first layouts: the transaction entry form must be usable with one hand, with the minimum number of fields visible by default.
- Responsive behavior through CSS media queries where only the layout changes (shell), CDK breakpoints where behavior changes (e.g. full-screen dialog on phones, standard dialog on desktop).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Angular Material + CDK, no other component library | Upgrades in step with Angular, accessibility, familiar to contributors | [ADR-0004](../architecture/adr/0004-angular-ui-library.md) |
| Missing components built as shared wrappers | Few and simple; keeps one look and one upgrade path | [ADR-0004](../architecture/adr/0004-angular-ui-library.md) |
| Separate chart library | Material has no charts | [ADR-0004](../architecture/adr/0004-angular-ui-library.md) |
| M3 palette `azure`, money colors as app tokens | Zero maintenance, contrasts checked by M3; restyling in one place | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Dark mode from the operating system, no switch | Costs nothing with `light-dark()`; a manual switch is a per-user preference for later | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Roboto and Material Symbols self-hosted, icons as SVG files | Same look everywhere, no third-party requests, small downloads | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| `@angular/localize` with Italian as the source | Built-in, no runtime cost | [ADR-0021](../architecture/adr/0021-frontend-foundation.md) |
| Signal Forms with Material, server errors through `apiFormErrors()` | Forms on signals like the rest of the app; one way to show client and server errors | [ADR-0023](../architecture/adr/0023-frontend-authentication.md) |

## Tests

- `core/icons/app-icons.spec.ts`: names resolve to `icons/<name>.svg` and `-fill`; paths, URLs, upper case and namespaces are refused.
- `app-locale.spec.ts`: Italian number and date formats.
- Forms: `login-page.spec.ts`, `settings-page.spec.ts` (client validation, server errors on the right field or the form, nothing shown before a field is touched, `mat-error` below the field), `api-form-errors.spec.ts`.
- Theme and layout: checked with screenshots on a phone (390×844, light and dark) and a desktop (1280×720) when block 1 was built; Playwright covers the critical flows on a phone viewport from block 3.
- Block 4: amount parsing and formatting, dates never shifted by the time zone ([testing.md](testing.md)).

## Scalability

- **More features**: new pages reuse Material and the shared components; the shared set grows only when a pattern repeats.
- **Multi-user**: per-user preferences (theme override, locale, default account) fit on top of the token-based theme and i18n setup.
- **PWA** (roadmap): fonts and icons are local files, cacheable by the service worker; the precache should list only the icons in use.
- **Upgrades**: `ng update @angular/material` together with every Angular major release.
- **Restyling**: through theme tokens; replacing the library would require a new ADR.

## To do / open questions

- Block 4: the dark-mode income green (`#02e600`) is too bright; amount input, date adapter (no time-zone shift, with tests), filter bar, empty and error states, selectors.
- Block 8: chart library (bar/pie for categories, line for trends, good mobile rendering, active maintenance, theme tokens in light and dark).
- Manual light/dark switch, if wanted after the MVP (per-user preference on the server with multi-user).

---
_Last updated: 2026-11-17_
