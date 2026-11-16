import { inject, Service } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '../api/api-error';
import { messageOf } from '../api/error-messages';

/** How long an error message stays on screen. */
export const ERROR_DURATION_MS = 8000;

/**
 * Shows an error the page does not show itself, as a snackbar with the Italian message of its code.
 * When the API gave a `traceId`, the snackbar offers to copy it, to quote it when reporting a bug.
 */
@Service()
export class ErrorNotifier {
  private readonly snackBar = inject(MatSnackBar);

  show(error: unknown): void {
    const traceId = error instanceof ApiError ? error.traceId : null;
    const ref = this.snackBar.open(
      messageOf(error),
      traceId
        ? $localize`:Snackbar action that copies the error reference:Copia riferimento`
        : undefined,
      { duration: ERROR_DURATION_MS },
    );
    if (traceId) {
      ref.onAction().subscribe(() => void navigator.clipboard?.writeText(traceId));
    }
  }
}
