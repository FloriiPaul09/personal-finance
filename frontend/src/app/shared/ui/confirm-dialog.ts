import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';

/** Texts of a confirmation, already translated by the page. */
export interface ConfirmDialogData {
  readonly title: string;
  readonly message: string;
  /** Text of the button that confirms, e.g. "Elimina". */
  readonly confirm: string;
  /** Text of the button that gives up, e.g. "Annulla". */
  readonly cancel: string;
}

/**
 * Asks before an action that cannot be undone (deleting): resolves `true` only when the person
 * presses the confirm button. Escape, a click outside and the cancel button resolve `false`.
 * Actions that can be undone (archiving) do not ask: they offer "Annulla" afterwards.
 */
export function confirmAction(dialog: MatDialog, data: ConfirmDialogData): Promise<boolean> {
  const ref = dialog.open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
    data,
    role: 'alertdialog',
    autoFocus: 'dialog',
    maxWidth: '400px',
  });
  return firstValueFrom(ref.afterClosed()).then((confirmed) => confirmed === true);
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>{{ data.message }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" [mat-dialog-close]="false">{{ data.cancel }}</button>
      <button matButton="filled" type="button" class="confirm" [mat-dialog-close]="true">
        {{ data.confirm }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .confirm {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}
