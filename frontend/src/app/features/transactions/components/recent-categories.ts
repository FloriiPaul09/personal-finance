import { Component, input, output } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { CategoryResponse } from '../../../core';

/**
 * The categories used last, as chips: one tap chooses one (docs/frontend/ui-components.md#quick-entry).
 * The full selector stays next to it for every other category.
 */
@Component({
  selector: 'app-recent-categories',
  imports: [MatChipsModule],
  template: `
    @if (categories().length > 0) {
      <mat-chip-listbox
        aria-label="Categorie recenti"
        i18n-aria-label="Chips of the categories used last"
        hideSingleSelectionIndicator
        [value]="selectedId()"
        (change)="choose.emit($event.value ?? null)"
      >
        @for (category of categories(); track category.id) {
          <mat-chip-option [value]="category.id">{{ category.name }}</mat-chip-option>
        }
      </mat-chip-listbox>
    }
  `,
  styles: `
    :host {
      display: block;
      margin-bottom: 8px;
    }
  `,
})
export class RecentCategories {
  readonly categories = input.required<readonly CategoryResponse[]>();
  readonly selectedId = input<string | null>(null);
  readonly choose = output<string | null>();
}
