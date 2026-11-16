import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBarHarness } from '@angular/material/snack-bar/testing';
import { ApiError } from '../api/api-error';
import { ErrorNotifier } from './error-notifier';

@Component({ template: '' })
class Host {}

describe('ErrorNotifier', () => {
  async function snackBar(): Promise<MatSnackBarHarness> {
    const loader = TestbedHarnessEnvironment.documentRootLoader(TestBed.createComponent(Host));
    return loader.getHarness(MatSnackBarHarness);
  }

  afterEach(() => vi.unstubAllGlobals());

  it('shows the Italian message of the code and lets the user copy the trace id', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } });

    TestBed.inject(ErrorNotifier).show(new ApiError(500, 'server.error', {}, '00-trace-01'));
    const bar = await snackBar();

    expect(await bar.getMessage()).toBe('Errore del server. Riprova più tardi.');
    expect(await bar.getActionDescription()).toBe('Copia riferimento');
    await bar.dismissWithAction();
    expect(writeText).toHaveBeenCalledWith('00-trace-01');
  });

  it('shows a generic message, without action, for errors that are not API errors', async () => {
    TestBed.inject(ErrorNotifier).show(new Error('boom'));
    const bar = await snackBar();

    expect(await bar.getMessage()).toBe('Si è verificato un errore. Riprova.');
    expect(await bar.hasAction()).toBe(false);
  });
});
