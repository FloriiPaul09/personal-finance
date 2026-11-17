import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { Component, computed, input, model, output, signal } from '@angular/core';
import { FormValueControl, ValidationError } from '@angular/forms/signals';
import {
  MatAutocompleteModule,
  MatAutocompleteSelectedEvent,
} from '@angular/material/autocomplete';
import { MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';

/** Limits of the API (docs/backend/api-endpoints.md#create-a-transaction). */
export const MAX_TAGS = 20;
export const MAX_TAG_LENGTH = 50;

/**
 * The tags of a movement as chips: type a name and press Enter or a comma, or pick one of the
 * `suggestions` (existing tags, the most used first). Names are trimmed, cut at 50 characters and
 * never repeated regardless of case, so "Vacanza" and "vacanza" stay one tag. A Signal Forms
 * control holding `string[]`, like the API.
 */
@Component({
  selector: 'app-tags-input',
  imports: [MatAutocompleteModule, MatChipsModule, MatFormFieldModule, MatIconModule],
  template: `
    <mat-form-field>
      <mat-label>{{ label() }}</mat-label>
      <mat-chip-grid #grid [disabled]="disabled()">
        @for (tag of value(); track tag) {
          <mat-chip-row (removed)="remove(tag)">
            {{ tag }}
            <button matChipRemove [attr.aria-label]="removeLabel(tag)">
              <mat-icon svgIcon="close" />
            </button>
          </mat-chip-row>
        }
        <input
          #text
          autocomplete="off"
          [maxLength]="maxTagLength"
          [disabled]="disabled() || value().length >= maxTags"
          [matChipInputFor]="grid"
          [matChipInputSeparatorKeyCodes]="separators"
          [matAutocomplete]="list"
          (matChipInputTokenEnd)="addTyped($event)"
          (input)="query.set(text.value)"
          (blur)="touch.emit()"
        />
      </mat-chip-grid>
      <mat-autocomplete #list (optionSelected)="addSuggested($event, text)">
        @for (name of options(); track name) {
          <mat-option [value]="name">{{ name }}</mat-option>
        }
      </mat-autocomplete>
      @if (value().length >= maxTags) {
        <mat-hint i18n="Hint of the tag field when it is full">Al massimo 20 tag.</mat-hint>
      } @else {
        <mat-hint i18n="Hint of the tag field">Invio o virgola per aggiungere un tag.</mat-hint>
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
  `,
})
export class TagsInput implements FormValueControl<string[]> {
  readonly value = model<string[]>([]);
  readonly label = input.required<string>();
  /** Existing tag names, in the order to offer them. */
  readonly suggestions = input<readonly string[]>([]);

  // Bound by the `FormField` directive of Signal Forms.
  readonly errors = input<readonly ValidationError.WithOptionalFieldTree[]>([]);
  readonly disabled = input(false);
  readonly touch = output<void>();

  protected readonly maxTags = MAX_TAGS;
  protected readonly maxTagLength = MAX_TAG_LENGTH;
  protected readonly separators = [ENTER, COMMA];
  protected readonly query = signal('');

  /** Suggestions not chosen yet that contain what is typed, at most 8. */
  protected readonly options = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    const chosen = new Set(this.value().map((tag) => tag.toLocaleLowerCase()));
    return this.suggestions()
      .filter((name) => !chosen.has(name.toLocaleLowerCase()))
      .filter((name) => name.toLocaleLowerCase().includes(query))
      .slice(0, 8);
  });

  protected addTyped(event: MatChipInputEvent): void {
    this.add(event.value);
    event.chipInput.clear();
    this.query.set('');
  }

  protected addSuggested(event: MatAutocompleteSelectedEvent, text: HTMLInputElement): void {
    this.add(event.option.value as string);
    text.value = '';
    this.query.set('');
  }

  protected remove(tag: string): void {
    this.value.update((tags) => tags.filter((t) => t !== tag));
  }

  protected removeLabel(tag: string): string {
    return $localize`:Button that removes a tag:Togli il tag ${tag}:tag:`;
  }

  private add(text: string): void {
    const name = text.trim().slice(0, MAX_TAG_LENGTH);
    const tags = this.value();
    if (
      name === '' ||
      tags.length >= MAX_TAGS ||
      tags.some((tag) => tag.toLocaleLowerCase() === name.toLocaleLowerCase())
    ) {
      return;
    }
    this.value.set([...tags, name]);
  }
}
