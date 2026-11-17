import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { RouterLink } from '@angular/router';
import { AccountResponse, ErrorState, Notifier } from '../../../core';
import { accountTypeInfo } from '../../../core/selectors';
import { AmountPipe } from '../../../shared/money';
import { confirmAction, EmptyState } from '../../../shared/ui';
import { AccountsStore } from '../data/accounts-store';

/**
 * The accounts with their balance (from the API, never added up here). A row opens the form;
 * its menu archives, restores or deletes. Archiving does not ask: the snackbar offers "Annulla".
 * Deleting asks, and is offered only for an account without movements
 * (docs/frontend/ui-components.md#archive-and-delete).
 */
@Component({
  selector: 'app-accounts-page',
  imports: [
    AmountPipe,
    EmptyState,
    ErrorState,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    RouterLink,
  ],
  templateUrl: './accounts-page.html',
  styleUrl: './accounts-page.scss',
})
export class AccountsPage {
  protected readonly store = inject(AccountsStore);
  private readonly notifier = inject(Notifier);
  private readonly dialog = inject(MatDialog);

  protected readonly showArchived = signal(false);
  protected readonly hasArchived = computed(() => !!this.store.all()?.some((a) => a.isArchived));
  protected readonly visible = computed(() =>
    (this.store.all() ?? []).filter((account) => this.showArchived() || !account.isArchived),
  );
  protected readonly typeOf = accountTypeInfo;

  /** Name of the menu button of a row, for screen readers. */
  protected actionsLabel(account: AccountResponse): string {
    return $localize`:Button that opens the actions of an account:Azioni per ${account.name}:name:`;
  }

  protected async archive(account: AccountResponse): Promise<void> {
    await this.store.archive(account);
    void this.notifier.undoable(
      $localize`:Snackbar after archiving an account:Conto «${account.name}:name:» archiviato.`,
      () => void this.store.unarchive(account),
    );
  }

  protected async unarchive(account: AccountResponse): Promise<void> {
    await this.store.unarchive(account);
    void this.notifier.show(
      $localize`:Snackbar after restoring an account:Conto «${account.name}:name:» ripristinato.`,
    );
  }

  protected async delete(account: AccountResponse): Promise<void> {
    const confirmed = await confirmAction(this.dialog, {
      title: $localize`:Title of the delete confirmation:Eliminare «${account.name}:name:»?`,
      message: $localize`:Message of the account delete confirmation:Il conto non ha movimenti. L'eliminazione non si può annullare.`,
      confirm: $localize`:Button that confirms a delete:Elimina`,
      cancel: $localize`:Button that closes a confirmation:Annulla`,
    });
    if (!confirmed) {
      return;
    }
    await this.store.delete(account);
    void this.notifier.show(
      $localize`:Snackbar after deleting an account:Conto «${account.name}:name:» eliminato.`,
    );
  }
}
