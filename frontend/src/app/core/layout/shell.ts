import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { FORM_PAGE, NAV_ITEMS, NEW_EXPENSE_PATH, SETTINGS_PATH } from './nav-items';

/**
 * Layout of every signed-in page: M3 navigation bar at the bottom on phones,
 * navigation rail on the left from 600px, and the "new expense" button always in reach.
 * The switch is pure CSS (shell.scss), so there is no layout state to keep in sync.
 *
 * On a phone the floating button hides on form pages (routes with `data: { form: true }`): there
 * it covered the "Salva" button, and a new expense is not what a form needs (block 6 review).
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatIconModule],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly items = NAV_ITEMS;
  protected readonly newExpensePath = NEW_EXPENSE_PATH;
  protected readonly settingsPath = SETTINGS_PATH;

  private readonly router = inject(Router);
  /** The page shown is a form (`data: { form: true }` on its route). */
  protected readonly formPage = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => {
        let route = this.router.routerState.snapshot.root;
        while (route.firstChild) {
          route = route.firstChild;
        }
        return route.data['form'] === FORM_PAGE.form;
      }),
    ),
    { initialValue: false },
  );
}
