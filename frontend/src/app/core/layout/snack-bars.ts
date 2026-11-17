/**
 * Class of every snackbar of the app. On phones, styles.scss lifts it above the navigation bar
 * and the "new expense" button, as Material 3 asks: at the bottom it covered the navigation for
 * the whole time it was shown (found in block 2 review).
 */
export const SNACK_BAR_PANEL_CLASS = 'pf-snackbar';
/** How long a confirmation stays on screen (errors stay longer, `ERROR_DURATION_MS`). */
export const SNACK_BAR_DURATION_MS = 4000;
/** How long a snackbar with "Annulla" stays on screen: time to read it and reach the button. */
export const UNDO_DURATION_MS = 8000;
