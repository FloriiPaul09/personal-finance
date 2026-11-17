import { inject, Injector, Service } from '@angular/core';
import {
  SNACK_BAR_DURATION_MS,
  SNACK_BAR_PANEL_CLASS,
  UNDO_DURATION_MS,
} from '../layout/snack-bars';

export interface NotifyOptions {
  /** Text of the button of the snackbar, e.g. "Annulla". */
  readonly action?: string;
  /** Called when the person presses the button. */
  readonly onAction?: () => void;
  /** How long the message stays; `SNACK_BAR_DURATION_MS` by default. */
  readonly duration?: number;
}

const UNDO = $localize`:Snackbar action that undoes the last action:Annulla`;

/**
 * Short messages at the bottom of the screen (Material snackbar), for every page and for
 * `ErrorNotifier`: "Conto creato.", "Conto archiviato." with "Annulla", errors.
 *
 * The snackbar is loaded with the first message, not at startup: anything the app loads at
 * startup shares its code with the pages, so the menus, selects and dialogs of the pages
 * entered the initial bundle with it (block 5: 571 kB instead of 479 kB,
 * docs/frontend/app-structure.md#boundaries). Every message gets the `pf-snackbar` class.
 */
@Service()
export class Notifier {
  private readonly injector = inject(Injector);

  async show(message: string, options: NotifyOptions = {}): Promise<void> {
    const { MatSnackBar } = await import('@angular/material/snack-bar');
    const ref = this.injector.get(MatSnackBar).open(message, options.action, {
      panelClass: [SNACK_BAR_PANEL_CLASS],
      duration: options.duration ?? SNACK_BAR_DURATION_MS,
    });
    const onAction = options.onAction;
    if (onAction) {
      ref.onAction().subscribe(() => onAction());
    }
  }

  /** A message with "Annulla", for actions that do not ask first (archiving). */
  undoable(message: string, undo: () => void): Promise<void> {
    return this.show(message, { action: UNDO, onAction: undo, duration: UNDO_DURATION_MS });
  }
}
