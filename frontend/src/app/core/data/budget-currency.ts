/**
 * The currency budgets are shown in. Budgets have no currency in the API yet, and their spending
 * adds up expenses of every currency (ADR-0019, limit of the MVP); with only euro accounts this is
 * right. When `[Backend]` gives budgets a currency, the budget pages and the dashboard read it
 * from the response and this constant goes away (docs/architecture/adr/0027-frontend-budgets.md).
 */
export const BUDGET_CURRENCY = 'EUR';
