import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import {
  disabled,
  form,
  FormField,
  maxLength,
  required,
  submit,
  validate,
} from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import {
  ApiError,
  CategoryKind,
  CategoryRequest,
  CategoryResponse,
  ErrorState,
  Notifier,
} from '../../../core';
import { apiFormErrors, focusFirstError } from '../../../core/forms';
import { CategorySelect } from '../../../core/selectors';
import { EmptyState } from '../../../shared/ui';
import { CategoriesStore } from '../data/categories-store';
import { CATEGORY_KINDS, categoryKindOf } from '../data/category-kinds';

/** Longest name the API accepts. */
export const CATEGORY_NAME_MAX_LENGTH = 100;

interface CategoryModel {
  name: string;
  kind: CategoryKind;
  parentId: string | null;
}

/** Same category at the same version: a list read again with nothing changed resets nothing. */
function sameVersion(a: CategoryResponse | undefined, b: CategoryResponse | undefined): boolean {
  return a?.id === b?.id && a?.version === b?.version;
}

const CATEGORIES_PATH = '/categories';

/**
 * New category (`/categories/new?kind=income&parent=<id>`) and changes to one
 * (`/categories/:id`): name, kind and parent. What the API would refuse is not offered: the kind
 * is fixed once the category is used or has subcategories, a category with subcategories stays
 * top-level, and the parent is chosen among the top-level categories of the same kind. A `412`
 * reads the list again and shows the current data (docs/frontend/state-management.md#versions).
 */
@Component({
  selector: 'app-category-form-page',
  imports: [
    CategorySelect,
    EmptyState,
    ErrorState,
    FormField,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    RouterLink,
  ],
  templateUrl: './category-form-page.html',
  styleUrl: './category-form-page.scss',
})
export class CategoryFormPage {
  protected readonly store = inject(CategoriesStore);
  private readonly router = inject(Router);
  private readonly notifier = inject(Notifier);

  /** From the route: absent for a new category. */
  readonly id = input<string>();
  /** From the query string of a new category: its kind and parent. */
  readonly kind = input<string>();
  readonly parent = input<string>();

  protected readonly kinds = CATEGORY_KINDS;
  protected readonly isNew = computed(() => !this.id());
  protected readonly category = computed(() => this.store.byId(this.id()), {
    equal: sameVersion,
  });
  protected readonly notFound = computed(
    () => !this.isNew() && this.store.all() !== undefined && !this.category(),
  );
  /** The category changed elsewhere while the form was open (`412`). */
  protected readonly changedElsewhere = signal(false);

  /** Why the kind cannot change, if it cannot. */
  protected readonly kindLock = computed(() => {
    const category = this.category();
    if (category?.hasSubcategories) {
      return $localize`:Why the kind of a category cannot change:Non si può cambiare: ha delle sottocategorie.`;
    }
    if (category?.isInUse) {
      return $localize`:Why the kind of a category cannot change:Non si può cambiare: la categoria è già usata.`;
    }
    return null;
  });
  /** A category with subcategories cannot become a subcategory itself. */
  protected readonly parentLocked = computed(() => !!this.category()?.hasSubcategories);
  protected readonly parentHint = computed(() =>
    this.parentLocked()
      ? $localize`:Why a category cannot become a subcategory:Ha delle sottocategorie: resta una categoria principale.`
      : $localize`:Hint of the parent field:Scegli una categoria madre per farne una sottocategoria.`,
  );

  protected readonly model = linkedSignal<CategoryModel>(() => {
    const category = this.category();
    return category
      ? { name: category.name, kind: category.kind, parentId: category.parentId }
      : { name: '', kind: categoryKindOf(this.kind()), parentId: this.parent() ?? null };
  });
  protected readonly categoryForm = form(this.model, (path) => {
    required(path.name, {
      message: $localize`:Category form error:Inserisci il nome della categoria.`,
    });
    validate(path.name, ({ value }) =>
      value().trim() === ''
        ? {
            kind: 'blank',
            message: $localize`:Category form error:Inserisci il nome della categoria.`,
          }
        : undefined,
    );
    maxLength(path.name, CATEGORY_NAME_MAX_LENGTH, {
      message: $localize`:Category form error:Al massimo ${CATEGORY_NAME_MAX_LENGTH}:count: caratteri.`,
    });
    disabled(path.kind, () => this.kindLock() !== null);
    disabled(path.parentId, () => this.parentLocked());
  });

  /** Path of the list, on the tab of the kind in the form. */
  protected readonly listQuery = computed(() => ({ kind: this.model().kind }));

  /** A new kind leaves the parent of the other kind: the category becomes top-level. */
  protected onKindChange(): void {
    const parent = this.store.byId(this.model().parentId);
    if (parent && parent.kind !== this.model().kind) {
      this.categoryForm.parentId().value.set(null);
    }
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    this.changedElsewhere.set(false);
    await submit(this.categoryForm, {
      action: async () => {
        const category = this.category();
        try {
          if (category) {
            await this.store.update(category, this.request());
          } else {
            await this.store.create(this.request());
          }
        } catch (error) {
          if (error instanceof ApiError && error.status === 412) {
            // The store reads the list again: the form shows the current data.
            this.changedElsewhere.set(true);
            return undefined;
          }
          const errors = apiFormErrors(this.categoryForm, error, {
            fieldsByCode: {
              'category.name_taken': 'name',
              'category.kind_locked': 'kind',
              'category.kind_mismatch': 'parentId',
              'category.too_deep': 'parentId',
              'category.parent_itself': 'parentId',
              'category.archived': 'parentId',
              'category.has_subcategories': 'parentId',
              'budget.branch_conflict': 'parentId',
            },
          });
          if (errors) {
            return errors;
          }
          throw error;
        }
        void this.notifier.show(
          category
            ? $localize`:Snackbar after saving a category:Categoria salvata.`
            : $localize`:Snackbar after creating a category:Categoria creata.`,
        );
        await this.router.navigate([CATEGORIES_PATH], { queryParams: this.listQuery() });
        return undefined;
      },
      onInvalid: (field) => focusFirstError(field),
    });
  }

  private request(): CategoryRequest {
    const { name, kind, parentId } = this.model();
    return { name: name.trim(), kind, parentId };
  }
}
