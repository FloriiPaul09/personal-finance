import { booleanAttribute, Component, computed, inject, input } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { CategoriesDirectory, CategoryKind, CategoryResponse, messageOf } from '..';
import { SelectControl } from './select-control';

interface CategoryOption {
  readonly category: CategoryResponse;
  readonly sub: boolean;
}

/** The options of one kind; `label` names the kind when both kinds are shown. */
interface CategorySection {
  readonly kind: CategoryKind;
  readonly label: string | null;
  readonly options: readonly CategoryOption[];
}

const KIND_LABELS: Record<CategoryKind, string> = {
  expense: $localize`:Group of expense categories in a selector:Uscite`,
  income: $localize`:Group of income categories in a selector:Entrate`,
};

/**
 * A category of one kind:
 * `<app-category-select label="Categoria" kind="expense" [formField]="form.categoryId" />`.
 *
 * Lists the active categories of `kind` from `CategoriesDirectory`, each top-level category
 * followed by its subcategories, indented; every one can be chosen. An archived category appears
 * only when it is the current value. `topLevelOnly` offers only top-level categories (the parent
 * of a category), `excludeId` leaves one out, `noneLabel` adds a choice for "none" (`null`).
 * `kind` `null` offers both kinds under "Uscite" and "Entrate", and `includeArchived` archived
 * categories too (filters of the movement list).
 */
@Component({
  selector: 'app-category-select',
  imports: [MatFormFieldModule, MatSelectModule],
  template: `
    <mat-form-field [floatLabel]="noneLabel() ? 'always' : 'auto'">
      <mat-label>{{ label() }}</mat-label>
      <mat-select
        panelClass="pf-select-panel"
        [value]="value()"
        [disabled]="disabled()"
        [required]="required()"
        [placeholder]="noneLabel() ?? ''"
        [errorStateMatcher]="errorStateMatcher"
        (selectionChange)="choose($event.value)"
        (openedChange)="onOpenedChange($event)"
      >
        <mat-select-trigger>{{ selectedLabel() }}</mat-select-trigger>
        @if (noneLabel(); as none) {
          <mat-option [value]="null">{{ none }}</mat-option>
        }
        @for (section of sections(); track section.kind) {
          @if (section.label) {
            <mat-optgroup [label]="section.label">
              @for (option of section.options; track option.category.id) {
                <mat-option [value]="option.category.id" [class.sub]="option.sub">
                  {{ option.category.name }}
                  @if (option.category.isArchived) {
                    <span class="archived" i18n="Label of an archived category in a selector"
                      >(archiviata)</span
                    >
                  }
                </mat-option>
              }
            </mat-optgroup>
          } @else {
            @for (option of section.options; track option.category.id) {
              <mat-option [value]="option.category.id" [class.sub]="option.sub">
                {{ option.category.name }}
                @if (option.category.isArchived) {
                  <span class="archived" i18n="Label of an archived category in a selector"
                    >(archiviata)</span
                  >
                }
              </mat-option>
            }
          }
        }
      </mat-select>
      @if (error(); as error) {
        <mat-error>{{ error.message }}</mat-error>
      } @else if (loadError(); as message) {
        <mat-hint>{{ message }}</mat-hint>
      } @else if (hint(); as hint) {
        <mat-hint>{{ hint }}</mat-hint>
      }
    </mat-form-field>
  `,
  styles: `
    :host {
      display: block;
    }

    mat-form-field {
      width: 100%;
    }

    .sub {
      padding-inline-start: 40px;
    }

    .archived {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class CategorySelect extends SelectControl {
  private readonly categories = inject(CategoriesDirectory);

  /** The kind to offer; `null` offers both. */
  readonly kind = input.required<CategoryKind | null>();
  /** Offer archived categories too (filters). */
  readonly includeArchived = input(false, { transform: booleanAttribute });
  /** Offer only top-level categories (choosing the parent of a category). */
  readonly topLevelOnly = input(false, { transform: booleanAttribute });
  /** A category not to offer, e.g. the one being edited when choosing its parent. */
  readonly excludeId = input<string | null>(null);
  /** Text of the "none" choice; without it, no such choice. */
  readonly noneLabel = input<string>();

  private readonly selected = computed(() => this.categories.byId(this.value()));
  protected readonly selectedLabel = computed(() => {
    const selected = this.selected();
    return selected ? this.categories.label(selected) : '';
  });

  protected readonly sections = computed<CategorySection[]>(() => {
    const kind = this.kind();
    const kinds: CategoryKind[] = kind ? [kind] : ['expense', 'income'];
    const selected = this.selected();
    return kinds.map((k) => {
      const options = this.optionsOf(k);
      const missing =
        selected?.kind === k &&
        selected.isArchived &&
        !options.some((o) => o.category.id === selected.id);
      return {
        kind: k,
        label: kind ? null : KIND_LABELS[k],
        options: missing ? [...options, { category: selected, sub: false }] : options,
      };
    });
  });

  protected readonly loadError = computed(() => {
    const error = this.categories.error();
    return error ? messageOf(error) : null;
  });

  private optionsOf(kind: CategoryKind): CategoryOption[] {
    return this.categories
      .groups(kind, { includeArchived: this.includeArchived() })
      .filter((group) => group.parent.id !== this.excludeId())
      .flatMap((group) => [
        { category: group.parent, sub: false },
        ...(this.topLevelOnly()
          ? []
          : group.children
              .filter((child) => child.id !== this.excludeId())
              .map((category) => ({ category, sub: true }))),
      ]);
  }
}
