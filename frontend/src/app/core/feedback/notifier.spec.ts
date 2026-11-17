import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBarHarness } from '@angular/material/snack-bar/testing';
import { SNACK_BAR_PANEL_CLASS } from '../layout/snack-bars';
import { Notifier } from './notifier';

@Component({ template: '' })
class Host {}

describe('Notifier', () => {
  let notifier: Notifier;

  beforeEach(() => (notifier = TestBed.inject(Notifier)));

  function snackBar(): Promise<MatSnackBarHarness> {
    const loader = TestbedHarnessEnvironment.documentRootLoader(TestBed.createComponent(Host));
    return loader.getHarness(MatSnackBarHarness);
  }

  function lastContainer(): Element | null {
    const all = document.querySelectorAll('.mat-mdc-snack-bar-container');
    return all.item(all.length - 1);
  }

  // Regression: on phones the snackbar covered the bottom navigation bar. The class lifts it
  // (styles.scss); the layout itself is checked in the browser (docs/frontend/testing.md).
  it('shows a message with the app class, loading the snackbar on first use', async () => {
    await notifier.show('Conto creato.');
    const bar = await snackBar();

    expect(await bar.getMessage()).toBe('Conto creato.');
    expect(await bar.hasAction()).toBe(false);
    expect(lastContainer()?.classList).toContain(SNACK_BAR_PANEL_CLASS);
  });

  it('calls back when the action is pressed', async () => {
    const onAction = vi.fn();
    await notifier.show('Errore', { action: 'Copia riferimento', onAction, duration: 8000 });
    const bar = await snackBar();

    expect(await bar.getActionDescription()).toBe('Copia riferimento');
    await bar.dismissWithAction();
    expect(onAction).toHaveBeenCalledOnce();
  });

  it('offers "Annulla" for an action that did not ask first', async () => {
    const undo = vi.fn();
    await notifier.undoable('Conto «Contanti» archiviato.', undo);
    const bar = await snackBar();

    expect(await bar.getActionDescription()).toBe('Annulla');
    expect(lastContainer()?.classList).toContain(SNACK_BAR_PANEL_CLASS);
    await bar.dismissWithAction();
    expect(undo).toHaveBeenCalledOnce();
  });
});
