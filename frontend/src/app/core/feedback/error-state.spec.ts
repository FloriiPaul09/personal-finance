import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatIcon } from '@angular/material/icon';
import { MatIconTestingModule } from '@angular/material/icon/testing';
import { By } from '@angular/platform-browser';
import { ApiError } from '../api/api-error';
import { ErrorState } from './error-state';

@Component({
  imports: [ErrorState],
  template: `<app-error-state
    [error]="error()"
    [retryable]="retryable()"
    (retry)="retries = retries + 1"
  />`,
})
class Host {
  readonly error = signal<unknown>(new ApiError(500, 'server.error', {}, '00-abc-01'));
  readonly retryable = signal(true);
  retries = 0;
}

describe('ErrorState', () => {
  let fixture: ComponentFixture<Host>;
  let writeText: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    TestBed.configureTestingModule({
      imports: [MatIconTestingModule],
    });
    fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
  });

  const element = () => fixture.nativeElement as HTMLElement;
  const buttons = () => Array.from(element().querySelectorAll('button'));

  it('says what went wrong in Italian, as an alert', () => {
    expect(element().querySelector('.message')!.textContent).toContain(
      'Errore del server. Riprova più tardi.',
    );
    expect(element().querySelector('app-error-state')!.getAttribute('role')).toBe('alert');
  });

  it('asks the page to load again', async () => {
    buttons()
      .find((b) => b.textContent?.includes('Riprova'))!
      .click();

    expect(fixture.componentInstance.retries).toBe(1);
  });

  it('hides "Riprova" when retrying makes no sense', async () => {
    fixture.componentInstance.retryable.set(false);
    await fixture.whenStable();

    expect(buttons().some((b) => b.textContent?.includes('Riprova'))).toBe(false);
  });

  it('shows the reference of the request and copies it', async () => {
    expect(element().querySelector('code')!.textContent).toBe('00-abc-01');

    buttons()
      .find((b) => b.getAttribute('aria-label') === 'Copia riferimento')!
      .click();
    await fixture.whenStable();

    expect(writeText).toHaveBeenCalledWith('00-abc-01');
    expect(buttons().some((b) => b.getAttribute('aria-label') === 'Riferimento copiato')).toBe(
      true,
    );
  });

  it('shows a cloud when the server cannot be reached, without a reference', async () => {
    fixture.componentInstance.error.set(new ApiError(0, 'network.unavailable'));
    await fixture.whenStable();

    expect(fixture.debugElement.query(By.directive(MatIcon)).componentInstance.svgIcon).toBe(
      'cloud_off',
    );
    expect(element().querySelector('.message')!.textContent).toContain(
      'Impossibile contattare il server',
    );
    expect(element().querySelector('code')).toBeNull();
  });

  it('shows a generic message for errors that are not from the API', async () => {
    fixture.componentInstance.error.set(new TypeError('x is undefined'));
    await fixture.whenStable();

    expect(element().querySelector('.message')!.textContent).toContain(
      'Si è verificato un errore. Riprova.',
    );
  });
});
