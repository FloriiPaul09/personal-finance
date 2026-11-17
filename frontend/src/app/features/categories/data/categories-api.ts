import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { CategoryRequest, CategoryResponse, ifMatch } from '../../../core';

/** The writes of the categories API (docs/backend/api-endpoints.md#categories); reads are in core. */
@Service()
export class CategoriesApi {
  private readonly http = inject(HttpClient);

  create(body: CategoryRequest): Observable<CategoryResponse> {
    return this.http.post<CategoryResponse>('/api/categories', body);
  }

  update(category: CategoryResponse, body: CategoryRequest): Observable<CategoryResponse> {
    return this.http.put<CategoryResponse>(`/api/categories/${category.id}`, body, {
      headers: ifMatch(category.version),
    });
  }

  /** Archiving a top-level category archives its subcategories too. */
  archive(category: CategoryResponse): Observable<CategoryResponse> {
    return this.http.post<CategoryResponse>(`/api/categories/${category.id}/archive`, null);
  }

  /** Restores only this category: subcategories are restored one by one, after their parent. */
  unarchive(category: CategoryResponse): Observable<CategoryResponse> {
    return this.http.post<CategoryResponse>(`/api/categories/${category.id}/unarchive`, null);
  }

  delete(category: CategoryResponse): Observable<void> {
    return this.http.delete<void>(`/api/categories/${category.id}`, {
      headers: ifMatch(category.version),
    });
  }
}
