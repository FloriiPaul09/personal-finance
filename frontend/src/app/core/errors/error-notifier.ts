import { inject, Service } from '@angular/core';
import { ApiError } from '../api/api-error';
import { messageOf } from '../api/error-messages';
import { Notifier } from '../feedback/notifier';

/** How long an error message stays on screen. */
export const ERROR_DURATION_MS = 8000;

/**
 * Shows an error the page does not show itself, as a snackbar with the Italian message of its code.
 * When the API gave a `traceId`, the snackbar offers to copy it, to quote it when reporting a bug.
 */
@Service()
export class ErrorNotifier {
  private readonly notifier = inject(Notifier);

  show(error: unknown): Promise<void> {
    const traceId = error instanceof ApiError ? error.traceId : null;
    return this.notifier.show(messageOf(error), {
      duration: ERROR_DURATION_MS,
      ...(traceId
        ? {
            action: $localize`:Snackbar action that copies the error reference:Copia riferimento`,
            onAction: () => void navigator.clipboard?.writeText(traceId),
          }
        : {}),
    });
  }
}
