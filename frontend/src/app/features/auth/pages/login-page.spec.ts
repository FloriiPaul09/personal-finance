import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { ApiError, provideAppIcons, SessionStore } from '../../../core';
import { LoginPage } from './login-page';

describe('LoginPage', () => {
  let fixture: ComponentFixture<LoginPage>;
  let login: ReturnType<typeof vi.fn>;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    login = vi.fn().mockResolvedValue({ id: '1', email: 'dev@example.com' });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAppIcons(),
        { provide: SessionStore, useValue: { login } },
      ],
    });
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    fixture = TestBed.createComponent(LoginPage);
    await fixture.whenStable();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const input = (type: string) =>
    el().querySelector<HTMLInputElement>(`input[autocomplete="${type}"]`)!;

  async function type(field: HTMLInputElement, value: string): Promise<void> {
    field.value = value;
    field.dispatchEvent(new Event('input'));
    field.dispatchEvent(new Event('blur'));
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

  function texts(selector: string): string[] {
    return Array.from(el().querySelectorAll(selector)).map((e) => e.textContent!.trim());
  }

  it('asks for email and password without calling the API', async () => {
    await submit();

    expect(texts('mat-error')).toEqual(["Inserisci l'email.", 'Inserisci la password.']);
    expect(login).not.toHaveBeenCalled();
  });

  it('refuses an invalid email', async () => {
    await type(input('username'), 'not-an-email');
    await type(input('current-password'), 'secret');
    await submit();

    expect(texts('mat-error')).toEqual(['Email non valida.']);
    expect(login).not.toHaveBeenCalled();
  });

  it('signs in, staying signed in by default, and opens the page to return to', async () => {
    fixture.componentRef.setInput('returnUrl', '/accounts');
    await type(input('username'), 'dev@example.com');
    await type(input('current-password'), 'dev-only-password');
    await submit();

    expect(login).toHaveBeenCalledWith({
      email: 'dev@example.com',
      password: 'dev-only-password',
      rememberMe: true,
    });
    expect(navigateByUrl).toHaveBeenCalledWith('/accounts');
  });

  it('opens the dashboard when there is no page to return to, or an unsafe one', async () => {
    fixture.componentRef.setInput('returnUrl', 'https://evil.example');
    await type(input('username'), 'dev@example.com');
    await type(input('current-password'), 'dev-only-password');
    await submit();

    expect(navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('can sign in only for this browser session', async () => {
    await type(input('username'), 'dev@example.com');
    await type(input('current-password'), 'dev-only-password');
    el().querySelector<HTMLInputElement>('mat-checkbox input')!.click();
    await submit();

    expect(login).toHaveBeenCalledWith(expect.objectContaining({ rememberMe: false }));
  });

  it.each([
    ['auth.invalid_credentials', 401, 'Email o password non corretti.'],
    ['request.too_many', 429, 'Troppi tentativi. Attendi un minuto e riprova.'],
  ])('shows %s on the form and stays on the page', async (code, status, message) => {
    login.mockRejectedValue(new ApiError(status, code));
    await type(input('username'), 'dev@example.com');
    await type(input('current-password'), 'wrong');
    await submit();

    expect(texts('[role="alert"]')).toEqual([message]);
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('shows and hides the password', async () => {
    const toggle = el().querySelector<HTMLButtonElement>('button[aria-pressed]')!;
    expect(input('current-password').type).toBe('password');
    expect(toggle.getAttribute('aria-label')).toBe('Mostra la password');

    toggle.click();
    await fixture.whenStable();

    expect(input('current-password').type).toBe('text');
    expect(toggle.getAttribute('aria-label')).toBe('Nascondi la password');
  });
});
