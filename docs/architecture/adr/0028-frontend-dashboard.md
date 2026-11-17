# ADR-0028: Frontend dashboard — home page of the month and chart library

- **Status**: Accepted
- **Date**: 2026-11-17
- **Deciders**: Paul

## Context

- The dashboard is the home page: the first page after login, opened often from a phone, so its code is downloaded at every start of the app.
- `GET /api/dashboard?month=yyyy-MM` answers with every section in one response ([ADR-0020](0020-dashboard-module.md)):
  - active accounts with their **current** balance;
  - per currency: total balance (archived accounts included, with `archivedBalance`), income, expenses and net of the month;
  - spending per **top-level** category and currency, largest first;
  - the progress of every budget of the month.

  Sums are per currency; empty sections are empty lists; without `month` the API uses its own month in UTC.
- The response covers one month only. Charts over time (roadmap) need an endpoint over a range of months, which ADR-0020 already foresees.
- [ADR-0004](0004-angular-ui-library.md) leaves charts to a dedicated library, chosen in `[Frontend]` with the dashboard. Requirements: bars and pies for categories, lines for trends, good rendering on phones, active maintenance, colors from the theme in light and dark. Candidates in November 2026:
  - **Chart.js 4** with ng2-charts 11 (Angular 22), about 46–60 kB gzip;
  - **Apache ECharts 6** with ngx-echarts 22 (released for Angular 22 in June 2026), about 157–177 kB gzip, with ARIA descriptions and decal patterns built in;
  - **ApexCharts**, similar weight to ECharts.
- The app never adds amounts up ([ADR-0024](0024-frontend-shared-components.md)). Features cannot import each other ([ADR-0021](0021-frontend-foundation.md)). The budget progress bar exists in `shared/ui` ([ADR-0027](0027-frontend-budgets.md)).

## Decision

1. **Chart.js 4 through ng2-charts** is the chart library. It is the only one imported, and only in `shared/charts`:
   - `<app-bar-chart>` draws horizontal bars of one series and registers just their parts (bar controller and element, category and linear scales, tooltip), so Chart.js enters only a chunk loaded with the pages that draw a chart (about 53 kB gzip, with the dashboard), never the initial bundle.
   - Names longer than 16 characters are cut on the axis and shown whole in the tooltip; a click on a bar emits its index, so the page can open what is behind it.
   - Chart colors are tokens. A chart of one series uses `--pf-chart-1`. Charts of several series (the trend charts of the roadmap) take `--pf-chart-1` … `--pf-chart-5` in a fixed order (blue, orange, aqua, yellow, magenta), with their own steps for light and dark, checked with a palette validator on the card surface: adjacent pairs ΔE ≥ 8 for color blindness and ≥ 15 for normal vision.
   - The canvas cannot read CSS, so the component resolves the tokens to colors and reads them again when the system switches light / dark. It does not animate when the system asks for reduced motion.
   - A chart always comes with the same figures in text, with their links: a canvas is not read by screen readers nor reached with the keyboard, and some light colors are below 3:1 on the surface.
2. **Sections, one column on phones, two from 905px**:
   1. the month selector;
   2. "Entrate e uscite": income, expenses and net, one row per currency, with a link to the movements of the month;
   3. "Budget": the three budgets closest to their limit (sorted by the API's `ratio`), with `<app-budget-progress>`, and "Vedi tutti (N)" / "Vai ai budget";
   4. "Spesa per categoria": per currency, horizontal bars as long as each amount compared with the largest, then the same categories as a list with their amounts. Five categories, then "Mostra tutte (N)" for the bars and the list together; a row, or its bar, opens `/transactions?categoryId=…&month=…`. No "Altre" row, which would be a sum made by the app: the length of a bar is a proportion drawn by the chart, not an amount;
   5. "Saldi attuali": active accounts with their current balance, each opening its movements of the month, and the total per currency, with "di cui … su conti archiviati" when archived accounts hold money.
3. **The month is in the URL** (`/dashboard?month=2026-11`) and always sent to the API. Months in the future are not offered (`max` on the selector, the current month instead of a future one in the URL). The account section is titled "Saldi attuali" because its balances are those of today, whatever the month shown.
4. **First use**: with no account at all (not even archived) the page only shows "Aggiungi il tuo primo conto". An empty section invites to the next step ("Aggiungi una spesa", "Imposta un budget").
5. **One request, read again** after every write the dashboard is computed from: `version('accounts', 'transactions', 'transfers', 'budgets', 'categories')`. The numbers of the month stay on screen while it is read again.
6. **Account types move to `core/selectors`** (labels and icons), so the accounts feature and the dashboard share them without importing each other and without growing the initial bundle.
7. **One E2E test of the critical flow**: an account in a currency of its own (CHF on the phone project, SEK on desktop), so the totals are exactly its movements whatever else the user has; an income, an expense and a budget over its limit created through the API; the page checked section by section, the category opening its movements, the previous month and back; everything deleted at the end.

## Consequences

**Positive**
- The home page answers "how is the month going" first, and every number leads to the movements behind it.
- Bars compare close amounts better than slices and stay readable with many categories on a phone.
- The lightest of the candidate libraries, loaded only where a chart is drawn; the trend charts of the roadmap (lines, bars) use the same library and tokens.
- Colors that stay apart for people with color vision deficiencies, in light and dark, and figures always readable as text.
- Correct with several currencies: one row, one chart, one total each.
- The E2E test does not depend on the user's own data nor on tests running at the same time.

**Negative**
- Names and amounts appear twice, on the bars and in the list: the section is longer on a phone (about 400 px for five categories).
- Chart.js draws on a canvas: theme colors must be resolved and redrawn by code, and accessibility comes from the text next to the chart, not from the chart.
- In a past month the balances are today's, next to the figures of that month: the title says so, the numbers do not.

## Alternatives considered

**Apache ECharts 6** — Rejected: ARIA descriptions, decal patterns and dark theme built in, many chart types, but about three times heavier on a page opened at every start from a phone; a more complex configuration.

**No library yet: ranked bars made like the budget bar, the library chosen with the trend charts** — Rejected: zero kilobytes more and colors already from the theme, but the home page should already show a real chart; the choice of the library is settled now.

**A donut with its list as the legend** — Rejected: slices compare close amounts badly and read poorly past five or six categories on a small screen; a first version of this block used it. **Bars without the list, amounts at the end of each bar** — Rejected: on a canvas the rows could not be links reachable by keyboard nor read by screen readers.

**Balance first** — Rejected: the balance changes little from day to day but would take the best place. **The current month only** — Rejected: the previous month could be seen only from the movements. **Every budget on the home page** — Rejected: with ten budgets the page gets long on a phone. **Empty sections without invitations** — Rejected: a new user would not know where to start.

**An "Altre" row** — Rejected: its amount would be a sum made by the app. **The EUR totals checked as differences before and after the test's movements** — Rejected: other E2E tests add expenses in the same month at the same time.
