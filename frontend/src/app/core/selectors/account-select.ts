import { Component, computed, inject, input } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { AmountPipe } from '../../shared/money';
import { AccountsDirectory, messageOf } from '..';
import { SelectControl } from './select-control';

/**
 * The account of a movement: `<app-account-select label="Conto" [formField]="form.accountId" />`.
 *
 * Lists the active accounts with their balance, from `AccountsDirectory`. An archived account
 * appears only when it is the current value (a movement recorded before archiving), so editing
 * it does not lose the account. `excludeId` leaves one account out (the other side of a
 * transfer).
 */
@Component({
  selector: 'app-account-select',
  imports: [AmountPipe, MatFormFieldModule, MatSelectModule],
  template: `
    <mat-form-field>
      <mat-label>{{ label() }}</mat-label>
      <mat-select
        panelClass="pf-select-panel"
        [value]="value()"
        [disabled]="disabled()"
        [required]="required()"
        [errorStateMatcher]="errorStateMatcher"
        (selectionChange)="choose($event.value)"
        (openedChange)="onOpenedChange($event)"
      >
        <mat-select-trigger>{{ selected()?.name }}</mat-select-trigger>
        @for (account of options(); track account.id) {
          <mat-option [value]="account.id">
            <span class="option">
              <span class="name">
                {{ account.name }}
                @if (account.isArchived) {
                  <span class="archived" i18n="Label of an archived account in a selector"
                    >(archiviato)</span
                  >
                }
              </span>
              <span class="pf-amount balance">{{
                account.balance | amount: account.currency
              }}</span>
            </span>
          </mat-option>
        }
      </mat-select>
      @if (error(); as error) {
        <mat-error>{{ error.message }}</mat-error>
      } @else if (loadError(); as message) {
        <mat-hint>{{ message }}</mat-hint>
      } @else if (empty()) {
        <mat-hint i18n="Hint of the account selector without accounts"
          >Nessun conto attivo: aggiungilo in Conti.</mat-hint
        >
      } @else if (hint(); as hint) {
        <mat-hint>{{ hint }}</mat-hint>
      }
    </mat-form-field>
  `,
  styles: `
    :host {
      display: block;
    }

    mat-form-field {
      width: 100%;
    }

    .option {
      display: flex;
      justify-content: space-between;
      gap: 16px;
    }

    .archived,
    .balance {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class AccountSelect extends SelectControl {
  private readonly accounts = inject(AccountsDirectory);

  /** An account not to offer, e.g. the source account when choosing the destination. */
  readonly excludeId = input<string | null>(null);

  protected readonly selected = computed(() => this.accounts.byId(this.value()));
  protected readonly options = computed(() => {
    const active = (this.accounts.active() ?? []).filter(
      (account) => account.id !== this.excludeId(),
    );
    const selected = this.selected();
    return selected?.isArchived ? [...active, selected] : active;
  });
  protected readonly empty = computed(
    () => this.accounts.active() !== undefined && this.options().length === 0,
  );
  protected readonly loadError = computed(() => {
    const error = this.accounts.error();
    return error ? messageOf(error) : null;
  });
}
