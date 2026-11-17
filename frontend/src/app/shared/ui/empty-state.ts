import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * What a page shows when there is nothing yet, and what to do about it:
 *
 * ```html
 * <app-empty-state icon="account_balance_wallet" title="Nessun conto"
 *                  message="Aggiungi il primo conto per registrare le spese.">
 *   <button matButton="filled" (click)="add()">Aggiungi conto</button>
 * </app-empty-state>
 * ```
 * Texts arrive translated from the page; the action is projected.
 */
@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  template: `
    <mat-icon class="icon" [svgIcon]="icon()" />
    <p class="title">{{ title() }}</p>
    @if (message(); as message) {
      <p class="message">{{ message }}</p>
    }
    <ng-content />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 32px 16px;
      text-align: center;
    }

    .icon {
      width: 48px;
      height: 48px;
      color: var(--mat-sys-on-surface-variant);
    }

    .title {
      margin: 0;
      font: var(--mat-sys-title-medium);
    }

    .message {
      margin: 0 0 8px;
      max-width: 36em;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
    }
  `,
})
export class EmptyState {
  /** A Material Symbols name (docs/frontend/ui-components.md#icons). */
  readonly icon = input('inbox');
  readonly title = input.required<string>();
  readonly message = input<string>();
}
