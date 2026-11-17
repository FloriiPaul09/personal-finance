import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError, SessionStore } from '../../../core';
import { SettingsPage } from './settings-page';

describe('SettingsPage', () => {
  let fixture: ComponentFixture<SettingsPage>;
  let changePassword: ReturnType<typeof vi.fn>;
  let logout: ReturnType<typeof vi.fn>;
  let open: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    changePassword = vi.fn().mockResolvedValue(undefined);
    logout = vi.fn().mockResolvedValue(undefined);
    open = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: SessionStore,
          useValue: {
            user: signal({ id: '1', email: 'dev@example.com' }),
            changePassword,
            logout,
          },
        },
        { provide: MatSnackBar, useValue: { open } },
      ],
    });
    fixture = TestBed.createComponent(SettingsPage);
    await fixture.whenStable();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const inputs = () => Array.from(el().querySelectorAll<HTMLInputElement>('form input'));

  async function fill(current: string, next: string, again: string): Promise<void> {
    for (const [field, value] of inputs().map((f, i) => [f, [current, next, again][i]] as const)) {
      field.value = value;
      field.dispatchEvent(new Event('input'));
      field.dispatchEvent(new Event('blur'));
    }
    await fixture.whenStable();
  }

  async function submit(): Promise<void> {
    el()
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  const errors = () =>
    Array.from(el().querySelectorAll('mat-error')).map((e) => e.textContent!.trim());

  it('shows the email of the signed-in user', () => {
    expect(el().querySelector('[data-testid="account-email"]')!.textContent!.trim()).toBe(
      'dev@example.com',
    );
  });

  it('logs out', async () => {
    el().querySelector<HTMLButtonElement>('[data-testid="logout"]')!.click();
    await fixture.whenStable();

    expect(logout).toHaveBeenCalled();
  });

  it('checks length and confirmation before calling the API', async () => {
    await fill('old-password', 'short', 'different');
    await submit();

    expect(errors()).toEqual(['Usa almeno 12 caratteri.', 'Le due password non coincidono.']);
    expect(changePassword).not.toHaveBeenCalled();
  });

  it('changes the password, confirms it and empties the form', async () => {
    await fill('old-password', 'a-new-long-password', 'a-new-long-password');
    await submit();

    expect(changePassword).toHaveBeenCalledWith({
      currentPassword: 'old-password',
      newPassword: 'a-new-long-password',
    });
    expect(open).toHaveBeenCalledWith('Password aggiornata.');
    expect(inputs().map((i) => i.value)).toEqual(['', '', '']);
  });

  it('shows a wrong current password on its field', async () => {
    changePassword.mockRejectedValue(new ApiError(400, 'auth.password_incorrect'));
    await fill('wrong-password', 'a-new-long-password', 'a-new-long-password');
    await submit();

    expect(errors()).toEqual(['La password attuale non è corretta.']);
    expect(open).not.toHaveBeenCalled();
  });

  it('shows a refused new password on its field, also when the API names the field', async () => {
    changePassword.mockRejectedValue(
      new ApiError(400, 'auth.password_rejected', { newPassword: ['Too weak.'] }),
    );
    await fill('old-password', 'a-new-long-password', 'a-new-long-password');
    await submit();

    expect(errors()).toEqual(['La password deve avere almeno 12 caratteri.']);
  });
});
