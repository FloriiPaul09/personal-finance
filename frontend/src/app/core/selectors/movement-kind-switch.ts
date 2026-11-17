import { Component, input } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';

/** What a new movement is: the two kinds of transaction, or a transfer between accounts. */
export type MovementKind = 'expense' | 'income' | 'transfer';

interface KindLink {
  readonly kind: MovementKind;
  readonly label: string;
  readonly path: string;
  readonly query: Record<string, string> | null;
}

const LINKS: readonly KindLink[] = [
  {
    kind: 'expense',
    label: $localize`:Kind of a new movement:Uscita`,
    path: '/transactions/new',
    query: null,
  },
  {
    kind: 'income',
    label: $localize`:Kind of a new movement:Entrata`,
    path: '/transactions/new',
    query: { kind: 'income' },
  },
  {
    kind: 'transfer',
    label: $localize`:Kind of a new movement:Trasferimento`,
    path: '/transfers/new',
    query: null,
  },
];

/**
 * "Uscita / Entrata / Trasferimento" at the top of the forms of a new movement. Expenses and
 * income are a form of `transactions`, transfers one of `transfers` (one feature per backend
 * module): each tab is a link to its form, replacing the current page in the history, so the
 * back button still leaves the form (docs/frontend/ui-components.md#quick-entry). The form goes
 * inside: `<app-movement-kind-switch current="expense"><form>…</form></app-movement-kind-switch>`.
 */
@Component({
  selector: 'app-movement-kind-switch',
  imports: [MatTabsModule, RouterLink],
  template: `
    <nav
      mat-tab-nav-bar
      mat-stretch-tabs
      [tabPanel]="panel"
      aria-label="Tipo di movimento"
      i18n-aria-label="Tabs of the kind of a new movement"
    >
      @for (link of links; track link.kind) {
        <a
          mat-tab-link
          [routerLink]="link.path"
          [queryParams]="link.query"
          replaceUrl
          [active]="link.kind === current()"
          [attr.data-testid]="'kind-' + link.kind"
          >{{ link.label }}</a
        >
      }
    </nav>
    <mat-tab-nav-panel #panel class="panel"><ng-content /></mat-tab-nav-panel>
  `,
  styles: `
    :host {
      display: block;
    }

    nav {
      max-width: 480px;
    }

    .panel {
      display: block;
      padding-top: 16px;
    }
  `,
})
export class MovementKindSwitch {
  readonly current = input.required<MovementKind>();
  protected readonly links = LINKS;
}
