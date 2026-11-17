# UI components

## Scope

The UI component library, theming, typography and icons, the shared components built in the project, and the rules for forms, dates, amounts and mobile use. Application structure, routing and state are covered in their own documents.

## Current state

Library decided in [ADR-0004](../architecture/adr/0004-angular-ui-library.md); theme, typography, icons and layout implemented in block 1 ([ADR-0021](../architecture/adr/0021-frontend-foundation.md)). Forms (Signal Forms with Material) since block 3 ([ADR-0023](../architecture/adr/0023-frontend-authentication.md)); amount input, dates, month selector and page states since block 4 ([ADR-0024](../architecture/adr/0024-frontend-shared-components.md)). Account and category selectors arrive with block 5, the list filter bar with block 6, charts with block 8.

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
  --pf-income: light-dark(<income 40>, <income 80>);   // _income-palette.scss
  --pf-expense: var(--mat-sys-error);
}
```

- **Material 3**, palette `azure`. Changing palette is a one-line change.
- **Dark mode follows the operating system**: every color token is emitted as `light-dark(light, dark)` and `color-scheme: light dark` lets the browser pick. `<meta name="color-scheme">` in `index.html` avoids a white flash on dark devices. No manual switch in the MVP.
- Components use **`--mat-sys-*` tokens** (e.g. `--mat-sys-surface-container`, `--mat-sys-on-surface-variant`, `--mat-sys-label-medium`) and the app tokens below, **never hard-coded colors**.

| App token / class | Use |
|---|---|
| `--pf-income`, `.pf-income` | Income amounts: tone 40 on light (`#1b6d24`), 80 on dark (`#88d982`) of a palette generated from `#2E7D32` (`src/_income-palette.scss`, Material `theme-color` schematic); same contrast as the error role |
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
- Account button (settings) at the top right on phones, on its own `surface-container-high` circle so the page can scroll under it, and at the bottom of the rail from 600px ([routing.md](routing.md#shell-and-navigation)).
- Phone first: the main content leaves room at the bottom for the bar and the floating button; safe areas (`env(safe-area-inset-bottom)`) are respected for phones with a gesture bar.

### Shared components

Block 4, [ADR-0024](../architecture/adr/0024-frontend-shared-components.md). Every one of them can be tried at `/dev/components` with `pnpm start` ([testing.md](testing.md#component-catalog)).

| Component | Where | Use |
|---|---|---|
| `<app-amount-input>` | `shared/money` | Money field of a Signal Form: `<app-amount-input label="Importo" [currency]="account.currency" [formField]="form.amount" />` |
| `AmountPipe` (`amount`) | `shared/money` | Amounts to read: `{{ balance \| amount: 'EUR' }}` → `1.234,56 €`; `{ signed: true }` → `+50,00 €` |
| `IsoDateAdapter` | `shared/dates` | Every Material date picker holds a `YYYY-MM-DD` string (provided once in `app.config`) |
| `<app-date-input>` | `shared/date-pickers` | Date field of a Signal Form: `<app-date-input label="Data" [formField]="form.date" />`; calendar in a dialog on phones, Italian texts |
| `<app-month-selector [(month)]>` | `shared/date-pickers` | ‹ Novembre 2026 › for budgets and the dashboard, "Mese corrente" when another month is shown, optional `max` |
| `<app-empty-state>` | `shared/ui` | Nothing to show yet: icon, title, message, projected action |
| `<app-error-state [error] (retry)>` | `core/feedback` | Data that could not be loaded: Italian message of the `code`, "Riprova", the reference (`traceId`) on its own line with a copy button |
| Loading | Material | `<mat-progress-bar mode="indeterminate">` above the content while a resource loads |
| Account and category selectors | `core` (block 5) | Fed by the reference data of `core`; category menu grouped by parent from the flat list (`parentId`) |
| List filter bar | transactions (block 6) | Period, account, category, text for the movement list, in the query string |

Pages show the state of a read with `@if`:

```html
@if (accounts.error(); as error) {
  <app-error-state [error]="error" (retry)="accounts.reload()" />
} @else if (accounts.isLoading() && !accounts.hasValue()) {
  <mat-progress-bar mode="indeterminate" />
} @else if (accounts.value()?.items?.length === 0) {
  <app-empty-state icon="account_balance_wallet" title="Nessun conto" i18n-title …>…</app-empty-state>
} @else { … }
```

#### Amount input

- The field holds a `number | null`, like the API; the text is parsed through the string, so `12,30` is exactly the number the API reads as `12.30`. Amounts are never added up in the app.
- Accepted: `12`, `12,5`, `1.234,56`, `1234,56`, and `12.5` from phone keyboards that only offer a dot; `1.234` (three digits after a single dot) is a thousand. Refused, never rounded: a third decimal, letters, more than 12 integer digits, a minus sign unless `allowNegative` (opening balances).
- Text that is not an amount is a Signal Forms **parse error** (`transformedValue`): the form is invalid until it is corrected, and the text stays as typed. Leaving the field rewrites a valid amount the canonical way (`1234,5` → `1.234,50`).
- Rules of the use are validators of the form: `required`, greater than zero for movements (`validate`), and so on.
- `inputmode="decimal"` opens the numeric keyboard on phones; the currency symbol comes from `Intl` (`€`, `CHF`, `USD`) and the `currency` input.

#### Dates and months

- `shared/dates` works on `YYYY-MM-DD` (`IsoDate`) and `YYYY-MM` (`IsoMonth`) strings: `todayIso()` (the date on the person's clock, not in UTC), `addDays`, `addMonths` (31 January + 1 month = end of February), `monthOf`, `currentMonth`, `addMonthsToMonth`, `monthRange` (first and last day, for a period filter), `formatIsoDate` (`17 nov 2026`), `formatMonth` (`novembre 2026`).
- `IsoDateAdapter`: the date picker reads typed dates day first (`17/11/2026`, `17/11/26`, `17-11-2026`, `17.11.2026`, `2026-11-17`), shows `17/11/2026` in the field, starts the week on Monday (from the locale) and never builds a `Date` at local midnight.
- `<app-date-input>`: holds `YYYY-MM-DD` or `null`, like the API. Text that is not a day ("31/02/2026") is a Signal Forms parse error with an Italian message; with a plain `matDatepicker` input the field turned red without any message (block 4 review). The value is written into the field only when the page changes it, so typing `1/2/2` is not rewritten to `01/02/2002` halfway. The calendar opens in a centered dialog on phones (`touchUi` below 600px), a popup on wider screens, and speaks Italian (`ItalianDatepickerIntl`, provided by the component). `min` / `max` limit the calendar; the form validates the range.
- **Libraries**: `shared/dates` (helpers and adapter, no components) is imported by `app.config` at startup; the fields live in `shared/date-pickers`, imported only by pages. Exporting the date field from `shared/dates` put the whole date picker in the initial bundle (779 kB instead of 481 kB): a component in a barrel imported at startup is not removed by tree shaking.

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

- A transaction date is a **calendar date without time** (`DateOnly` in the API, `YYYY-MM-DD` in JSON), and it stays that string in the app, date picker included (`IsoDateAdapter`): no time zone can shift the day.
- Amounts are `number` with at most 2 decimals, read and written with `Intl`, never added up in the frontend: the API is the source of truth for totals and balances.
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
| Amounts as `number` with an own Italian parser, `<app-amount-input>` as a Signal Forms control | Forms hold what the API takes; no conversion in services, the offline queue or the CSV import | [ADR-0024](../architecture/adr/0024-frontend-shared-components.md) |
| Own `DateAdapter<string>` on `YYYY-MM-DD`, `<app-date-input>` as a Signal Forms control | No `Date` in date fields, so no day shift; Italian messages and texts; `Temporal` once Safari supports it | [ADR-0024](../architecture/adr/0024-frontend-shared-components.md) |
| Small state components chosen with `@if`; error state in `core` | Explicit templates; the error state reads `ApiError` | [ADR-0024](../architecture/adr/0024-frontend-shared-components.md) |
| Income green from a generated palette (`#2E7D32`) | Material's green palette is neon on dark surfaces | [ADR-0024](../architecture/adr/0024-frontend-shared-components.md) |
| Selectors in block 5, filter bar in block 6 | Built with their real data and first page | [ADR-0024](../architecture/adr/0024-frontend-shared-components.md) |

## Tests

- `core/icons/app-icons.spec.ts`: names resolve to `icons/<name>.svg` and `-fill`; paths, URLs, upper case and namespaces are refused.
- `app-locale.spec.ts`: Italian number and date formats.
- Forms: `login-page.spec.ts`, `settings-page.spec.ts` (client validation, server errors on the right field or the form, nothing shown before a field is touched, `mat-error` below the field), `api-form-errors.spec.ts`.
- Theme and layout: checked with screenshots on a phone (390×844, light and dark) and a desktop (1280×720) when block 1 was built; Playwright covers the critical flows on a phone viewport from block 3.
- Block 4: `amount.spec.ts` (every accepted and refused text, round trip, JSON value without floating point noise), `amount-input.spec.ts` (number in the model, text kept while typing, canonical text on leaving, parse errors block the form, messages after touch, reset), `amount.pipe.spec.ts`, `iso-date.spec.ts` and `iso-date-adapter.spec.ts` (in three time zones, the date picker with a Signal Form included), `date-input.spec.ts` (in three time zones: typed and picked days, parse error with message, reset, Italian labels), `month-selector.spec.ts`, `empty-state.spec.ts`, `error-state.spec.ts` ([testing.md](testing.md)).

## Scalability

- **More features**: new pages reuse Material and the shared components; the shared set grows only when a pattern repeats.
- **Multi-user**: per-user preferences (theme override, locale, default account) fit on top of the token-based theme and i18n setup.
- **PWA** (roadmap): fonts and icons are local files, cacheable by the service worker; the precache should list only the icons in use. The offline queue stores amounts and dates exactly as the forms hold them.
- **CSV import**: rows reuse `parseAmount()` and the date helpers, with the same errors as the form.
- **Multi-currency and other languages**: formatting goes through `Intl` with the app locale and the account currency.
- **Upgrades**: `ng update @angular/material` together with every Angular major release.
- **Restyling**: through theme tokens; replacing the library would require a new ADR.

## To do / open questions

- ✅ Block 4: amount input, date adapter, month selector, page states, softer income green ([ADR-0024](../architecture/adr/0024-frontend-shared-components.md)).
- Block 5: account and category selectors with the reference data of `core`.
- Block 6: list filter bar for the movements.
- `Temporal.PlainDate` instead of strings in the adapter, once Safari supports `Temporal`.
- Block 8: chart library (bar/pie for categories, line for trends, good mobile rendering, active maintenance, theme tokens in light and dark).
- Manual light/dark switch, if wanted after the MVP (per-user preference on the server with multi-user).

---
_Last updated: 2026-11-17_
