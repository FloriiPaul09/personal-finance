import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '../api/api-error';
import { ErrorNotifier } from '../errors/error-notifier';
import { provideSnackBars, SNACK_BAR_PANEL_CLASS } from './snack-bars';

// Regression: on phones the snackbar covered the bottom navigation bar. The class lifts it
// (styles.scss); the layout itself is checked in the browser (docs/frontend/testing.md).
describe('provideSnackBars', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideSnackBars()] }));

  function lastContainer(): Element | null {
    const all = document.querySelectorAll('.mat-mdc-snack-bar-container');
    return all.item(all.length - 1);
  }

  it('marks every snackbar with the app class', () => {
    TestBed.inject(MatSnackBar).open('Salvato');
    TestBed.tick();

    expect(lastContainer()?.classList).toContain(SNACK_BAR_PANEL_CLASS);
  });

  it('keeps the class when a caller passes its own options (error messages)', () => {
    TestBed.inject(ErrorNotifier).show(new ApiError(500, 'server.error', {}, '00-trace-01'));
    TestBed.tick();

    expect(lastContainer()?.classList).toContain(SNACK_BAR_PANEL_CLASS);
  });
});
