import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateTransactionRequest,
  ifMatch,
  PageResponse,
  TransactionKind,
  TransactionResponse,
  UpdateTransactionRequest,
} from '../../../core';

/** Filters of the movement list, as the API takes them (docs/backend/api-endpoints.md#list-transactions). */
export interface MovementFilters {
  /** First and last day, both included (`YYYY-MM-DD`). */
  readonly from: string;
  readonly to: string;
  readonly accountId?: string | null;
  /** A top-level category includes its subcategories. */
  readonly categoryId?: string | null;
  readonly kind?: TransactionKind | null;
  /** Text in the description. */
  readonly q?: string | null;
  readonly tag?: string | null;
}

/** Movements per page of the list: enough to fill a desktop screen. */
export const PAGE_SIZE = 50;

/** The calls of the transactions API (docs/backend/api-endpoints.md#transactions). */
@Service()
export class TransactionsApi {
  private readonly http = inject(HttpClient);

  list(
    filters: MovementFilters,
    cursor: string | null,
  ): Observable<PageResponse<TransactionResponse>> {
    let params = new HttpParams().set('limit', PAGE_SIZE);
    for (const [name, value] of Object.entries(filters)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(name, value);
      }
    }
    if (cursor) {
      params = params.set('cursor', cursor);
    }
    return this.http.get<PageResponse<TransactionResponse>>('/api/transactions', { params });
  }

  get(id: string): Observable<TransactionResponse> {
    return this.http.get<TransactionResponse>(`/api/transactions/${id}`);
  }

  /** `body.id` is chosen by the app (UUIDv7): sending the same request again creates nothing new. */
  create(body: CreateTransactionRequest): Observable<TransactionResponse> {
    return this.http.post<TransactionResponse>('/api/transactions', body);
  }

  update(
    transaction: TransactionResponse,
    body: UpdateTransactionRequest,
  ): Observable<TransactionResponse> {
    return this.http.put<TransactionResponse>(`/api/transactions/${transaction.id}`, body, {
      headers: ifMatch(transaction.version),
    });
  }

  delete(transaction: TransactionResponse): Observable<void> {
    return this.http.delete<void>(`/api/transactions/${transaction.id}`, {
      headers: ifMatch(transaction.version),
    });
  }
}
