import { Component, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { safeReturnUrl } from '../auth/return-url';

/**
 * Shown when the API cannot be reached while checking the session (offline, API down).
 * "Riprova" goes back to the page the user asked for, which checks the session again.
 */
@Component({
  selector: 'app-unavailable-page',
  imports: [MatButtonModule, MatIconModule],
  template: `
    <main class="pf-centered">
      <mat-icon class="icon" svgIcon="cloud_off" />
      <h1 class="pf-page-title" i18n="Page title">Il server non risponde</h1>
      <p i18n="Explanation on the page shown when the API cannot be reached">
        Controlla la connessione e riprova tra poco.
      </p>
      <button matButton="filled" type="button" (click)="retry()" i18n="Button">Riprova</button>
    </main>
  `,
  styles: `
    .icon {
      width: 48px;
      height: 48px;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class UnavailablePage {
  private readonly router = inject(Router);
  /** Page to open again (query parameter, bound by the router). */
  readonly returnUrl = input<string>();

  protected retry(): void {
    void this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
  }
}
