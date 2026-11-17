import { Provider } from '@angular/core';
import { MAT_SNACK_BAR_DEFAULT_OPTIONS, MatSnackBarConfig } from '@angular/material/snack-bar';

/**
 * Class of every snackbar of the app. On phones, styles.scss lifts it above the navigation bar
 * and the "new expense" button, as Material 3 asks: at the bottom it covered the navigation for
 * the whole time it was shown (found in block 2 review).
 */
export const SNACK_BAR_PANEL_CLASS = 'pf-snackbar';
/** How long a confirmation stays on screen (errors stay longer, `ERROR_DURATION_MS`). */
export const SNACK_BAR_DURATION_MS = 4000;

/** Defaults for every snackbar; options passed to `open()` are merged on top of them. */
export function provideSnackBars(): Provider {
  const defaults: MatSnackBarConfig = {
    panelClass: [SNACK_BAR_PANEL_CLASS],
    duration: SNACK_BAR_DURATION_MS,
  };
  return { provide: MAT_SNACK_BAR_DEFAULT_OPTIONS, useValue: defaults };
}
