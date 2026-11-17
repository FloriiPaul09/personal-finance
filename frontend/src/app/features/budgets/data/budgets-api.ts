import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { BudgetResponse, CreateBudgetRequest, ifMatch, ListResponse } from '../../../core';

/** The calls of the budgets API (docs/backend/api-endpoints.md#budgets). */
@Service()
export class BudgetsApi {
  private readonly http = inject(HttpClient);

  /** Entries of one category, or of every category. */
  list(categoryId?: string): Observable<ListResponse<BudgetResponse>> {
    return this.http.get<ListResponse<BudgetResponse>>('/api/budgets', {
      params: categoryId ? { categoryId } : {},
    });
  }

  create(body: CreateBudgetRequest): Observable<BudgetResponse> {
    return this.http.post<BudgetResponse>('/api/budgets', body);
  }

  /** `amount` `null` stops the budget from the month of the entry. */
  update(entry: BudgetResponse, amount: number | null): Observable<BudgetResponse> {
    return this.http.put<BudgetResponse>(
      `/api/budgets/${entry.id}`,
      { amount },
      { headers: ifMatch(entry.version) },
    );
  }

  delete(entry: BudgetResponse): Observable<void> {
    return this.http.delete<void>(`/api/budgets/${entry.id}`, {
      headers: ifMatch(entry.version),
    });
  }
}
