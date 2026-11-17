import { computed, Service, Signal } from '@angular/core';
import { CategoryKind, CategoryResponse } from '../api/models';
import { referenceList } from './reference-list';

/** A top-level category with its subcategories, in the API's order (by name). */
export interface CategoryGroup {
  readonly parent: CategoryResponse;
  readonly children: readonly CategoryResponse[];
}

/**
 * The categories of the user, archived ones included, read once for the whole app: the
 * categories page, the category selector and later movements and budgets share this list
 * (docs/frontend/state-management.md#reference-data). Read again after writes to categories,
 * transactions and budgets, because `isInUse` comes from them.
 */
@Service()
export class CategoriesDirectory {
  private readonly list = referenceList<CategoryResponse>('/api/categories', [
    'categories',
    'transactions',
    'budgets',
  ]);

  /** Every category in the API's order: expenses then income, each parent before its children. */
  readonly all: Signal<readonly CategoryResponse[] | undefined> = this.list.items;
  readonly error = this.list.error;
  readonly loading = this.list.loading;

  private readonly byIds = computed(
    () => new Map((this.all() ?? []).map((category) => [category.id, category])),
  );

  /** The category with this id, archived or not; `undefined` while loading or when unknown. */
  byId(id: string | null | undefined): CategoryResponse | undefined {
    return id ? this.byIds().get(id) : undefined;
  }

  /**
   * Top-level categories of a kind with their subcategories, in the API's order. Without
   * `includeArchived`, archived categories are left out (archiving a parent archives its
   * subcategories, so an active subcategory always has an active parent).
   */
  groups(kind: CategoryKind, options: { includeArchived?: boolean } = {}): CategoryGroup[] {
    const visible = (this.all() ?? []).filter(
      (category) => category.kind === kind && (options.includeArchived || !category.isArchived),
    );
    const children = new Map<string, CategoryResponse[]>();
    for (const category of visible) {
      if (category.parentId) {
        children.set(category.parentId, [...(children.get(category.parentId) ?? []), category]);
      }
    }
    return visible
      .filter((category) => !category.parentId)
      .map((parent) => ({ parent, children: children.get(parent.id) ?? [] }));
  }

  /** "Trasporti › Carburante" for a subcategory, the name for a top-level category. */
  label(category: CategoryResponse): string {
    const parent = this.byId(category.parentId);
    return parent ? `${parent.name} › ${category.name}` : category.name;
  }

  reload(): void {
    this.list.reload();
  }
}
