import { components } from './schema';

// Names the app uses for the types generated from the OpenAPI document (schema.ts).
// Contract: docs/backend/api-endpoints.md. Regenerate with `pnpm api:types`.
type Schemas = components['schemas'];

/** A calendar date without time, written `yyyy-MM-dd` (e.g. a transaction date). */
export type IsoDate = string;
/** A month, written `yyyy-MM` (budgets, dashboard). */
export type YearMonth = Schemas['YearMonth'];
/** Version of a resource: send it back in `If-Match` to change or delete it (`ifMatch`). */
export type Version = number;

/** Every list of the API: `{ "items": [...] }`. */
export interface ListResponse<T> {
  items: T[];
}

/** A page of a list read with a cursor (`nextCursor` is `null` on the last page). */
export interface PageResponse<T> extends ListResponse<T> {
  nextCursor: string | null;
}

export type CurrentUser = Schemas['GetCurrentUserResponse'];
export type LoginRequest = Schemas['LoginRequest'];
export type ChangePasswordRequest = Schemas['ChangePasswordRequest'];

export type AccountType = Schemas['AccountType'];
export type AccountResponse = Schemas['AccountResponse'];
export type AccountRequest = Schemas['CreateAccountRequest'];

export type CategoryKind = Schemas['CategoryKind'];
export type CategoryResponse = Schemas['CategoryResponse'];
export type CategoryRequest = Schemas['CreateCategoryRequest'];

export type TransactionKind = Schemas['TransactionKind'];
export type TransactionResponse = Schemas['TransactionResponse'];
export type CreateTransactionRequest = Schemas['CreateTransactionRequest'];
export type UpdateTransactionRequest = Schemas['UpdateTransactionRequest'];
export type TagResponse = Schemas['TagResponse'];

export type TransferResponse = Schemas['TransferResponse'];
export type CreateTransferRequest = Schemas['CreateTransferRequest'];
export type UpdateTransferRequest = Schemas['UpdateTransferRequest'];

export type BudgetResponse = Schemas['BudgetResponse'];
export type CreateBudgetRequest = Schemas['CreateBudgetRequest'];
export type UpdateBudgetRequest = Schemas['UpdateBudgetRequest'];
export type BudgetProgressResponse = Schemas['BudgetProgressResponse'];

export type DashboardResponse = Schemas['DashboardResponse'];
