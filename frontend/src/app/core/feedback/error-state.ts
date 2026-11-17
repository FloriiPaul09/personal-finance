import { Component, computed, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiError } from '../api/api-error';
import { messageOf } from '../api/error-messages';

/**
 * In place of data that could not be loaded:
 * `<app-error-state [error]="accounts.error()" (retry)="accounts.reload()" />`.
 *
 * Shows the Italian message of the error `code`, "Riprova", and the reference of the request
 * (`traceId`) to copy when reporting the problem. It lives in `core` because it reads `ApiError`;
 * errors of a form go on its fields instead (docs/frontend/ui-components.md#forms).
 */
@Component({
  selector: 'app-error-state',
  imports: [MatButtonModule, MatIconModule],
  template: `
    <mat-icon class="icon" [svgIcon]="icon()" />
    <p class="message">{{ message() }}</p>
    @if (retryable()) {
      <button matButton="filled" type="button" (click)="retry.emit()" i18n="Button">Riprova</button>
    }
    @if (traceId(); as traceId) {
      <p class="reference">
        <span i18n="Error reference label">Riferimento:</span>
        <code>{{ traceId }}</code>
        <button
          matIconButton
          type="button"
          [attr.aria-label]="copied() ? copiedLabel : copyLabel"
          (click)="copy(traceId)"
        >
          <mat-icon [svgIcon]="copied() ? 'check' : 'content_copy'" />
        </button>
      </p>
    }
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
      color: var(--mat-sys-error);
    }

    .message {
      margin: 0 0 8px;
      max-width: 36em;
      font: var(--mat-sys-body-large);
    }

    .reference {
      display: flex;
      align-items: center;
      gap: 4px;
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);

      code {
        overflow-wrap: anywhere;
      }
    }
  `,
  host: { role: 'alert' },
})
export class ErrorState {
  /** The error of the failed read (e.g. `httpResource.error()`). */
  readonly error = input.required<unknown>();
  /** Show "Riprova" (default); the page reloads on `retry`. */
  readonly retryable = input(true);
  readonly retry = output<void>();

  protected readonly copyLabel = $localize`:Button that copies the error reference:Copia riferimento`;
  protected readonly copiedLabel = $localize`:Button after copying the error reference:Riferimento copiato`;
  protected readonly copied = signal(false);

  protected readonly message = computed(() => messageOf(this.error()));
  protected readonly traceId = computed(() => {
    const error = this.error();
    return error instanceof ApiError ? error.traceId : null;
  });
  /** Offline or API down: a cloud; any other failure: the error sign. */
  protected readonly icon = computed(() => {
    const error = this.error();
    return error instanceof ApiError && (error.status === 0 || error.code === 'server.unavailable')
      ? 'cloud_off'
      : 'error';
  });

  protected copy(traceId: string): void {
    void navigator.clipboard?.writeText(traceId).then(() => this.copied.set(true));
  }
}
