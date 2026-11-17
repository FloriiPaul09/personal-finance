import { HttpTestingController } from '@angular/common/http/testing';
import { AccountResponse, CategoryResponse, TransactionResponse, TransferResponse } from '..';

// Data shaped like the API's answers, for unit tests (docs/backend/api-endpoints.md).

let sequence = 0;
/** A UUID-shaped id, different at each call. */
export function testId(): string {
  sequence++;
  return `0193a5c4-0000-7000-8000-${sequence.toString().padStart(12, '0')}`;
}

export function anAccount(overrides: Partial<AccountResponse> = {}): AccountResponse {
  return {
    id: testId(),
    name: 'Conto corrente',
    type: 'checking',
    currency: 'EUR',
    initialBalance: 100,
    balance: 1339.85,
    isArchived: false,
    hasMovements: true,
    version: 81723,
    ...overrides,
  };
}

export function aCategory(overrides: Partial<CategoryResponse> = {}): CategoryResponse {
  return {
    id: testId(),
    name: 'Spesa',
    kind: 'expense',
    parentId: null,
    isArchived: false,
    isInUse: false,
    hasSubcategories: false,
    version: 90211,
    ...overrides,
  };
}

/** A few categories in the API's order: expenses (a parent with children), then income. */
export function someCategories(): CategoryResponse[] {
  const trasporti = aCategory({ name: 'Trasporti', hasSubcategories: true });
  return [
    aCategory({ name: 'Spesa', isInUse: true }),
    trasporti,
    aCategory({ name: 'Auto', parentId: trasporti.id }),
    aCategory({ name: 'Carburante', parentId: trasporti.id, isInUse: true }),
    aCategory({ name: 'Svago', isArchived: true }),
    aCategory({ name: 'Stipendio', kind: 'income', isInUse: true }),
    aCategory({ name: 'Altro', kind: 'income' }),
  ];
}

export function aTransaction(overrides: Partial<TransactionResponse> = {}): TransactionResponse {
  return {
    id: testId(),
    kind: 'expense',
    accountId: testId(),
    categoryId: testId(),
    transferId: null,
    date: '2026-11-12',
    amount: 12.5,
    signedAmount: -12.5,
    description: 'Pranzo',
    tags: [],
    version: 90411,
    ...overrides,
  };
}

export function aTransfer(overrides: Partial<TransferResponse> = {}): TransferResponse {
  return {
    id: testId(),
    fromAccountId: testId(),
    toAccountId: testId(),
    date: '2026-11-12',
    amount: 300,
    description: 'Risparmio',
    outgoingTransactionId: testId(),
    incomingTransactionId: testId(),
    version: 91022,
    ...overrides,
  };
}

/** Answers the pending read of the accounts list. */
export function flushAccounts(backend: HttpTestingController, items: AccountResponse[]): void {
  backend.expectOne((r) => r.url === '/api/accounts' && r.method === 'GET').flush({ items });
}

/** Answers the pending read of the categories list. */
export function flushCategories(backend: HttpTestingController, items: CategoryResponse[]): void {
  backend.expectOne((r) => r.url === '/api/categories' && r.method === 'GET').flush({ items });
}
