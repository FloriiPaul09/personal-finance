import { inject, Service } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import {
  CategoriesDirectory,
  CategoryKind,
  CategoryRequest,
  CategoryResponse,
  DataChanges,
} from '../../../core';
import { CategoriesApi } from './categories-api';

/**
 * The categories feature's only way to the API (docs/frontend/state-management.md#the-store-pattern).
 * Reads come from `CategoriesDirectory` in core, the list the selectors use too; every write
 * tells `DataChanges`, so that list is read again.
 */
@Service()
export class CategoriesStore {
  private readonly api = inject(CategoriesApi);
  private readonly directory = inject(CategoriesDirectory);
  private readonly changes = inject(DataChanges);

  readonly all = this.directory.all;
  readonly error = this.directory.error;
  readonly loading = this.directory.loading;

  byId(id: string | null | undefined): CategoryResponse | undefined {
    return this.directory.byId(id);
  }

  groups(kind: CategoryKind, options: { includeArchived?: boolean } = {}) {
    return this.directory.groups(kind, options);
  }

  reload(): void {
    this.directory.reload();
  }

  create(body: CategoryRequest): Promise<CategoryResponse> {
    return this.write(this.api.create(body));
  }

  update(category: CategoryResponse, body: CategoryRequest): Promise<CategoryResponse> {
    return this.write(this.api.update(category, body));
  }

  archive(category: CategoryResponse): Promise<CategoryResponse> {
    return this.write(this.api.archive(category));
  }

  /**
   * Restores the category, then the given subcategories one by one: the API restores only the
   * category it is called on (undo of archiving a parent restores the children it archived).
   */
  async unarchive(
    category: CategoryResponse,
    subcategories: readonly CategoryResponse[] = [],
  ): Promise<CategoryResponse> {
    const restored = await this.write(this.api.unarchive(category));
    for (const subcategory of subcategories) {
      await this.write(this.api.unarchive(subcategory));
    }
    return restored;
  }

  delete(category: CategoryResponse): Promise<void> {
    return this.write(this.api.delete(category));
  }

  /** Sends a write, then has the categories read again: also after a failure (e.g. a `412`). */
  private async write<T>(request: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(request);
    } finally {
      this.changes.notify('categories');
    }
  }
}
