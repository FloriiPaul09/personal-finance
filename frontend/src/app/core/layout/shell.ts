import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NAV_ITEMS, NEW_EXPENSE_PATH } from './nav-items';

/**
 * Layout of every signed-in page: M3 navigation bar at the bottom on phones,
 * navigation rail on the left from 600px, and the "new expense" button always in reach.
 * The switch is pure CSS (shell.scss), so there is no layout state to keep in sync.
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
}
