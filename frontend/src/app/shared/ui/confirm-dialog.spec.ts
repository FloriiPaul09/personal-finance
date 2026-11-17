import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { confirmAction } from './confirm-dialog';

@Component({ template: '' })
class Host {
  readonly dialog = inject(MatDialog);
}

describe('confirmAction', () => {
  let loader: HarnessLoader;
  let dialog: MatDialog;

  beforeEach(() => {
    const fixture = TestBed.createComponent(Host);
    dialog = fixture.componentInstance.dialog;
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  });

  const data = {
    title: 'Eliminare «Contanti»?',
    message: "L'operazione non si può annullare.",
    confirm: 'Elimina',
    cancel: 'Annulla',
  };

  async function press(text: string): Promise<boolean> {
    const answer = confirmAction(dialog, data);
    const harness = await loader.getHarness(MatDialogHarness);
    expect(await harness.getTitleText()).toBe(data.title);
    expect(await harness.getContentText()).toBe(data.message);
    expect(await harness.getRole()).toBe('alertdialog');
    const buttons = Array.from(
      document.querySelectorAll<HTMLButtonElement>('mat-dialog-actions button'),
    );
    buttons.find((b) => b.textContent?.trim() === text)!.click();
    return answer;
  }

  it('resolves true when the action is confirmed', async () => {
    expect(await press('Elimina')).toBe(true);
  });

  it('resolves false when the person gives up', async () => {
    expect(await press('Annulla')).toBe(false);
  });

  it('resolves false when the dialog is closed with Escape', async () => {
    const answer = confirmAction(dialog, data);
    const harness = await loader.getHarness(MatDialogHarness);
    await harness.close();
    expect(await answer).toBe(false);
  });
});
