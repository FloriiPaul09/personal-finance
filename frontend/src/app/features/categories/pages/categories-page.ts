import { Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { CategoryResponse, ErrorState, Notifier } from '../../../core';
import { confirmAction, EmptyState } from '../../../shared/ui';
import { CategoriesStore } from '../data/categories-store';
import { CATEGORY_KINDS, categoryKindOf } from '../data/category-kinds';

/**
 * Categories of one kind (`?kind=expense|income`, a tab each): every top-level category with its
 * subcategories indented below, as the selectors show them. A row opens the form; its menu adds a
 * subcategory, archives, restores or deletes. Archiving does not ask: the snackbar offers
 * "Annulla". Deleting asks, and is offered only for an unused category without subcategories
 * (docs/frontend/ui-components.md#archive-and-delete).
 */
@Component({
  selector: 'app-categories-page',
  imports: [
    EmptyState,
    ErrorState,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTabsModule,
    RouterLink,
  ],
  templateUrl: './categories-page.html',
  styleUrl: './categories-page.scss',
})
export class CategoriesPage {
  protected readonly store = inject(CategoriesStore);
  private readonly notifier = inject(Notifier);
  private readonly dialog = inject(MatDialog);

  /** From the query string. */
  readonly kind = input<string>();

  protected readonly kinds = CATEGORY_KINDS;
  protected readonly selectedKind = computed(() => categoryKindOf(this.kind()));
  protected readonly showArchived = signal(false);
  protected readonly hasArchived = computed(() =>
    (this.store.all() ?? []).some((c) => c.kind === this.selectedKind() && c.isArchived),
  );
  /** Each top-level category followed by its subcategories. */
  protected readonly rows = computed(() =>
    this.store
      .groups(this.selectedKind(), { includeArchived: this.showArchived() })
      .flatMap((group) => [group.parent, ...group.children]),
  );
  protected readonly hasAny = computed(() =>
    (this.store.all() ?? []).some((c) => c.kind === this.selectedKind()),
  );

  /** Name of the menu button of a row, for screen readers. */
  protected actionsLabel(category: CategoryResponse): string {
    return $localize`:Button that opens the actions of a category:Azioni per ${category.name}:name:`;
  }

  /** The parent of an archived subcategory, while it is archived too: restore it first. */
  protected archivedParentOf(category: CategoryResponse): CategoryResponse | undefined {
    const parent = this.store.byId(category.parentId);
    return parent?.isArchived ? parent : undefined;
  }

  protected async archive(category: CategoryResponse): Promise<void> {
    // Archiving a parent archives its subcategories: "Annulla" restores the ones active now.
    const activeChildren = (this.store.all() ?? []).filter(
      (c) => c.parentId === category.id && !c.isArchived,
    );
    await this.store.archive(category);
    void this.notifier.undoable(
      $localize`:Snackbar after archiving a category:Categoria «${category.name}:name:» archiviata.`,
      () => void this.store.unarchive(category, activeChildren),
    );
  }

  protected async unarchive(category: CategoryResponse): Promise<void> {
    await this.store.unarchive(category);
    void this.notifier.show(
      $localize`:Snackbar after restoring a category:Categoria «${category.name}:name:» ripristinata.`,
    );
  }

  protected async delete(category: CategoryResponse): Promise<void> {
    const confirmed = await confirmAction(this.dialog, {
      title: $localize`:Title of the delete confirmation:Eliminare «${category.name}:name:»?`,
      message: $localize`:Message of the category delete confirmation:La categoria non è usata. L'eliminazione non si può annullare.`,
      confirm: $localize`:Button that confirms a delete:Elimina`,
      cancel: $localize`:Button that closes a confirmation:Annulla`,
    });
    if (!confirmed) {
      return;
    }
    await this.store.delete(category);
    void this.notifier.show(
      $localize`:Snackbar after deleting a category:Categoria «${category.name}:name:» eliminata.`,
    );
  }
}
